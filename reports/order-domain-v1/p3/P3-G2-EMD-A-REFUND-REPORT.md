# P3-G2 — EMD-A Refund After Reissue

Closing report for P3-G2, the second slice of P3-G ancillary servicing: making
`AncillaryExchangeDisposition.Refund` fully executable.

Companion documents, all in this folder:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, now carrying `ICC-P3-EMD-REFUND`.
* [P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md](P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md) — the frozen G1 baseline this builds on.
* [P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md](P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md) — the frozen P3-F exchange baseline.
* [P3-PHASE-PLAN.md](P3-PHASE-PLAN.md) — the P3-G deliverables and exit gate.

---

## 1. Starting State

```text
P3-G2 baseline          702f30c01e7d4dd676f00715cda1050172e84b86  P3-F / P3-G1 — FINAL TWO-BLOCKER FREEZE CORRECTION
Working tree at start   clean
```

Inherited frozen baseline: every P3-F exchange shape; the accepted-plan rail with per-stage durable evidence;
recover-first with `WasDispatched` on every provider rail; the G1 EMD-A association lifecycle with
`DisassociatedByReissue` / `Reassociated` provenance; and the post-document truth invariant.

---

## 2. The Governing Model

The commercial model is the heart of this slice, and it was the one thing the first implementation got wrong.

```text
OrderChange        = the servicing-operation envelope
PriceChangeSet     = an independently committed financial consequence

one Exchange operation may therefore have multiple PriceChangeSets
without having multiple OrderChanges.
```

Concretely:

```text
one Exchange servicing operation
    -> one OrderChange                        (committed at materialization, G1-frozen)
    -> one initial Exchange PriceChangeSet
    -> zero..N later ancillary-refund PriceChangeSets
```

A confirmed ancillary refund is a **dependent commercial consequence of the same Exchange operation**, not a
second servicing operation.

### Why the first attempt failed

The first implementation reached for `Order.CommitPriceChange`, which always mints a **new** `OrderChange`.
That collided with a P1 invariant enforced in the database:

```text
OrderChanges         UNIQUE (OrderId, OperationId) WHERE OperationId IS NOT NULL
```

The exchange already owns that slot from materialization, so nine flow tests failed on
`Cannot insert duplicate key row ... IX_OrderChanges_OrderId_OperationId`.

The model was already right; the API call was wrong. `OrderPriceChangeSet` was never constrained to one per
change:

```text
OrderPriceChangeSets  ChangeId is NOT unique
                      UNIQUE (OrderId, FinancialSequence)
```

So the fix is a new, narrow domain capability that **appends** a price consequence to an existing change —
not a relaxed index, not a child servicing operation, not a delayed materialization.

### The new domain capability

`Order.CommitDependentPriceChange(AcceptedDependentPriceChangeArgs, IIdGenerator, IClock)` in
[Order.DependentPricing.cs](../../../src/AeroTech.Ordering.Domain/OrderAggregate/Order.DependentPricing.cs):

1. Resolves the host change and **fails closed** unless exactly one change exists for
   `(this order, this operation)` and its `ChangeType` is the expected one — `DependentPriceChangeHostNotResolvable`
   (20308, 409).
2. Stages a new `OrderPriceChangeSet` against `host.Id` with the next `FinancialSequence` and
   `ExpectedCommercialVersion` = the commercial version immediately before this consequence.
3. Attaches it, advances `CommercialVersion` exactly once, and raises exactly one `OrderPricingChanged`
   carrying the **existing** `OrderChange.Id` and the **new** `PriceChangeSet.Id`.

`AttachPriceChange` was split so the shared attach/commit/recompute path is reused verbatim:

```csharp
AttachPriceChange(staged, now)        => _changes.Add(staged.Change); AttachPriceConsequence(staged, now);
AttachPriceConsequence(staged, now)   => the existing attach, commit, recompute and obligation-version logic
```

Nothing about the original exchange `PriceChangeSet`, its pricing lines, or the `OrderChange` metadata is
touched.

---

## 3. The Corrected Commit Sequence

```text
document exchange confirmed
 -> persist confirmation evidence (+ coupled residual settle & EMD-S materialize)
 -> MaterializeAsync: order change, predecessor Exchanged, successor ticket, lineage,
    DisassociatedByReissue for EVERY executable affected EMD-A            [committed]
 -> if (plan.IsResidualRejected) Reconcile
 -> monetary settlement (capture -> refund-due / external residual)
 -> ancillary stage, per coupon:
      reassociation                                       -> provider act, then local move
      refund   -> document act confirmed
                  -> [ EMD coupon refund truth
                     + ancillary refund PriceChangeSet
                     + RefundPriceChangeSetId on the plan row ]   ONE checkpoint
                  -> value movement
 -> CompleteAsync
```

The commercial consequence is committed **with the document truth**, before the value movement — the same
shape the frozen Refund capability already uses. It is no longer deferred to completion, which is what made
the earlier design both wrong and unrecoverable.

### No rollback, ever

If the value movement returns `Pending`, `Unknown`, `Rejected` or contradictory evidence:

| | |
| --- | --- |
| EMD coupon | stays `Refunded` |
| ancillary refund `PriceChangeSet` | stays committed |
| predecessor ticket | stays `Exchanged` |
| successor ticket | stays authoritative |
| operation | `AwaitingExternal` or `NeedsReconciliation` |

---

## 4. Disassociation Now Covers Refunds

**Defect found and fixed in this slice.** `DisassociateAncillariesAsync` iterated `plan.Reassociations`, so a
coupon destined for refund was never given its `DisassociatedByReissue` provenance — its association history
showed only the issue-time `Associated` record even though the predecessor coupon it documented had become
`Exchanged`.

It now iterates `plan.ExecutableAncillaries`. Every affected ancillary the exchange executes is detached inside
the materializing transaction, before any downstream stage, regardless of its downstream disposition. That is
both the truthful state and the benchmarked precondition of EMD-A refund.

`EmdCoupon.Refund` still clears `AssociatedTicketCouponId` defensively; after this fix it is already null.

---

## 5. The Source Owns the Economics

An ancillary refund is executed only from a source-approved decision. `AncillaryRefundTerms` now carries:

```csharp
public sealed record AncillaryRefundTerms(
    decimal ApprovedAmount,
    int CurrencyId,
    string ApprovedDisposition,
    string SourceReference,
    PricingSource PricingSource,
    IReadOnlyList<AcceptedRefundPricingLine> PricingLines);
```

`ExchangeAncillaryPlanner.EnsureRefundIsExecutable` refuses, before any irreversible act, a decision missing
any of: economics, a positive amount, a currency, a currency matching the document, a disposition, a source
reference, pricing evidence, or an **external** pricing source. All refusals are
`AncillaryRefundEconomicsMissing` (20307, 422). `RefundConservationPolicy.EnsureReconciles` additionally
requires the approved lines to reconcile to the approved amount.

Ordering never uses `PricingSource.OrderingDerived`, the EMD issue value, the order-service price or any
pricing allocation as a refund formula. The committed pricing lines are exactly the source-approved lines
persisted on the accepted plan.

**A source-approved reversal link is carried, not dropped, and it may only reverse value the document itself
carries.** `AcceptedRefundPricingLine.ReversesPricingLineId` now maps through to
`AcceptedPricingLineArgs.OriginalPricingLineId`, so the ledger keeps the link between a refund line and the
line it reverses; `SettlementPartyRef` and `SettlementCategory` are carried for the same reason.
`EnsureReversalsStayWithinTheDocument` refuses a line naming a reversed line other than the EMD coupon's own
declared `PricingLineId` — `RefundReversalOutsideDocumentScope` (20227, 422), the same rule and the same code
the frozen document-refund path already uses. The domain's `EnsureReversalIsWellFormed` then applies the full
reversal invariants (the original exists, opposes it, preserves conversion provenance and does not exceed
outstanding value) exactly as for every other reversal in the ledger.

---

## 6. Idempotency Proven From Persisted State

The accepted plan's ancillary row is keyed `(OperationId, EmdCouponId)` and carries the accepted refund
decision plus the new `RefundPriceChangeSetId`. That row is the durable correlation: the specific

```text
exchange operation + EMD document + EMD coupon + accepted refund decision
```

provably already owns its `PriceChangeSet`. No in-memory flag participates, and no generic financial
sub-operation framework was introduced.

Crash boundaries, and what a restart does:

| # | Crash point | Restart behaviour |
| --- | --- | --- |
| 1 | document host confirmed, local transaction lost | recovery reports `WasDispatched`, the act is not repeated, the EMD refund and the consequence are committed exactly once |
| 2 | EMD refund + `PriceChangeSet` saved, value not dispatched | plan reloads with `RefundDocumentOutcome = Confirmed` and `RefundPriceChangeSetId` set; the flow resumes at the value act, appends nothing |
| 3 | value dispatched, response lost | value recovery reports `WasDispatched`, no second movement, no second consequence |
| 4 | one refund coupon completed, another pending | each coupon owns its own keys, its own row and its own consequence; the completed one is never revisited |

---

## 7. Read-Model Correction

`OrderViewBuilder.BuildChanges` projected a single `PriceChangeSetId` per change with
`FirstOrDefault(set => set.ChangeId == change.Id)`, which became non-deterministic once an exchange change can
own several sets. Both that site and the two `ExchangeService` lookups that used `.Single(...)` now go through

```csharp
order.OriginatingPriceConsequenceOf(orderChangeId)   // lowest FinancialSequence for that change
```

so a change view keeps naming its **originating** consequence. `BuildPricingHistory` already enumerated every
committed set, so no pricing history is lost.

---

## 8. Persistence and Migrations

One additive migration, `P3G2AncillaryRefundConsequence`, on top of G2's earlier two:

| Migration | Columns added to `Order.AcceptedExchangePlanAncillaries` | Destructive ops in `Up` |
| --- | --- | --- |
| `P3G2AncillaryRefund` | 17 | none |
| `P3G2AncillaryRefundEvidence` | 2 | none |
| `P3G2AncillaryRefundConsequence` | `RefundPricingSource` (int, null), `RefundPriceChangeSetId` (bigint, null) | none |

No existing historical migration was edited. No index was weakened — in particular
`IX_OrderChanges_OrderId_OperationId` is untouched.

---

## 9. Frozen Invariants Preserved

| Invariant | Status |
| --- | --- |
| `UNIQUE (OrderId, OperationId)` on `OrderChanges` | unchanged |
| G1 post-document materialization timing | unchanged |
| Exchange successor / lineage semantics | unchanged |
| Exclusive Order operation claim model | unchanged |
| `ReassociateExisting` behaviour | unchanged |
| P3-F monetary sequencing | unchanged |
| Existing historical migrations | unchanged |

Not created: a child `Refund` servicing operation, a parent/child claim framework, a generic ancillary
workflow engine, or a second `OrderChange` for the same exchange operation.

---

## 10. EMD Document Lifecycle

`ElectronicMiscDocument` and `EmdCoupon` gained the refund transition, mirroring the existing void lifecycle
rather than inventing a parallel one.

```csharp
public bool IsFullyRefunded => _coupons.Count > 0 && _coupons.All(coupon => coupon.IsRefunded);

public bool PermitsRefund(int emdCouponNumber, long operationId)
    => StatusSummary != ElectronicMiscDocumentStatus.Voided
       && _coupons.SingleOrDefault(coupon => coupon.CouponNumber == emdCouponNumber) is { } candidate
       && (candidate.IsOpenForUse || candidate.IsRefundedBy(operationId));

public void RefundCoupon(EmdCouponRefund refund, IClock clock)   // idempotent per operation
```

| Rule | Where |
| --- | --- |
| a `Voided` document is never refunded | `RefundCoupon`, `ElectronicMiscDocumentCouponIsNotRefundable` (20306, 409) |
| a coupon that is not `OpenForUse` is never refunded | `RefundCoupon`, 20306 |
| a second refund by the same operation is a no-op | `IsRefundedBy(operationId)` early return |
| coupon-level partial refund | only the approved coupon transitions; the document becomes `Refunded` only when **every** coupon is |
| a refunded coupon carries no association | `EmdCoupon.Refund` sets `AssociatedTicketCouponId = null`, `Status = Refunded` |
| a refunded coupon is never reassociated or refunded again | `Associable` excludes a `Refunded` document; `PermitsRefund` / `PermitsReassociation` return false |
| refund provenance is durable | `EmdCouponRefundRecord` — owned entity, `Refund*` column prefix, mirroring `DocumentVoidRecord` |

`IsDisassociationSettledBy` was added so a replay that re-enters materialization treats a coupon this
operation already disassociated, reassociated **or refunded** as settled, rather than throwing. The guard order
inside `DisassociateCouponByReissue` was corrected for the same reason: "already settled by this operation" is
now checked before the `IsOpenForUse` gate.

---

## 11. Cross-Stage Edge-Case Matrix

| Case | Document act | Value act | EMD coupon | `PriceChangeSet` | Ticket | Operation |
| --- | --- | --- | --- | --- | --- | --- |
| both confirmed | `Confirmed` | `Confirmed` | `Refunded` | committed | `Exchanged` | `Completed` |
| document `Pending` / `Unknown` | unresolved | never dispatched | `OpenForUse` | none | `Exchanged` | `AwaitingExternal` |
| document `Rejected` | `Rejected` | never dispatched | `OpenForUse` | none | `Exchanged` | `NeedsReconciliation` |
| document confirmed, contradictory echo | contradiction | never dispatched | `OpenForUse` | none | `Exchanged` | `NeedsReconciliation` |
| document confirmed, value `Pending` / `Unknown` | `Confirmed` | unresolved | `Refunded` | **committed** | `Exchanged` | `AwaitingExternal` |
| document confirmed, value `Rejected` | `Confirmed` | `Rejected` | `Refunded` | **committed** | `Exchanged` | `NeedsReconciliation` |
| document confirmed, contradictory value echo | `Confirmed` | contradiction | `Refunded` | **committed** | `Exchanged` | `NeedsReconciliation` |
| refund + reassociation in one exchange | per coupon | per coupon | per coupon | one set for the refund only | `Exchanged` | `Completed` |
| two refunded coupons | two acts | two acts | both `Refunded` | two sets, distinct financial sequences | `Exchanged` | `Completed` |
| multi-coupon document, one coupon approved | one act, one coupon | one act | only that coupon | one set | `Exchanged` | `Completed` |
| source economics missing / self-derived | never dispatched | never dispatched | `OpenForUse` | none | untouched, no reissue | refused at acceptance (20307) |
| crash after document dispatch | recovered, never repeated | dispatched once | `Refunded` | committed once | `Exchanged` | `Completed` |
| crash before document dispatch | never dispatched, retried once | dispatched once | `Refunded` | committed once | `Exchanged` | `Completed` |
| completed replay | not repeated | not repeated | unchanged | unchanged | unchanged | `Completed`, `IsReplay` |

The invariant that runs down every row: **no row rolls the ticket back, and no row commits the commercial
consequence without a confirmed, uncontradicted document refund.**

---

## 12. Regression Results

Every figure below is from an actual run on this working tree.

| Suite | Result |
| --- | --- |
| `AncillaryRefundFlowTests` (G2 focused) | 35 passed, 0 failed, 0 skipped |
| `EmdReassociationFlowTests` (G1 reassociation) | 29 passed, 0 failed, 0 skipped |
| `AncillaryDispositionGateTests` (G1 disposition gate) | 23 passed, 0 failed, 0 skipped |
| `PostDocumentTruthFreezeGateTests` (G1 freeze gate) | 11 passed, 0 failed, 0 skipped |
| `AeroTech.Ordering.Domain.Tests` (full) | 511 passed, 0 failed, 0 skipped (7 s) |
| `Contracts/DocumentRefund/` (new port contract kit) | 17 passed, 0 failed, 0 skipped |
| `Contracts/RefundValue/` | 11 passed, 0 failed, 0 skipped |
| `Contracts/EmdAssociation/` | 15 passed, 0 failed, 0 skipped |
| `Contracts/AncillaryDisposition/` | 20 passed, 0 failed, 0 skipped |
| `ExchangeFlowTests` (P3-F) | 32 passed, 0 failed, 0 skipped |
| `RefundDueExchangeFlowTests` (P3-F) | 25 passed, 0 failed, 0 skipped |
| `ResidualDocumentCouplingTests` (P3-F) | 15 passed, 0 failed, 0 skipped |
| `ResidualEvidenceFreezeGateTests` (P3-F) | 12 passed, 0 failed, 0 skipped |
| `DocumentRefundFlowTests` (P3-D) | 28 passed, 0 failed, 0 skipped |
| **`AeroTech.Ordering.Persistence.Tests` (full)** | **1048 passed, 0 failed, 0 skipped** (9.5 min) |
| `dotnet build AeroTech.Ordering.sln` | Build succeeded, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." |

Every suite above is part of the single full Persistence run; the rows are broken out so each item the freeze
gate names has its own figure. `Failed: 0` on the whole project means no suite outside this slice regressed
either.

---

## 13. Requirement → Code → Test Traceability

| Brief | Requirement | Code | Test |
| --- | --- | --- | --- |
| §2.1 | refund executes only from a source-approved decision | `ExchangeAncillaryPlanner.EnsureRefundIsExecutable` | `G2S1` (7 shapes) |
| §2.1 | a reversal may only reverse value the document carries | `EnsureReversalsStayWithinTheDocument` | `G2S2` |
| §2.2 | the refund acts on a disassociated coupon | `ExchangeService.DisassociateAncillariesAsync` over `ExecutableAncillaries` | `G2F1` |
| §2.3 | document authority owns `'R'` | `IDocumentRefundPort`, `RefundAncillaryDocumentAsync` | `G2H1`, `G2D1`–`G2D5` |
| §2.4 | coupon-level partial refund | `EmdCoupon.Refund`, `IsFullyRefunded` | `G2H2` |
| §2.5 | value follows the document, never leads it | `MoveAncillaryRefundValueAsync` is only reachable from a confirmed document act | `G2D1`, `G2D2`, `G2D5` |
| §2.6 | no rollback of document or ticket truth | `RefundAncillaryDocumentAsync`, `MoveAncillaryRefundValueAsync`, `ReconcileAsync` | `G2V1`–`G2V3`, `G2C5` |
| §2.7 | a refunded coupon is final | `Associable`, `PermitsRefund`, `PermitsReassociation` | `G2F2` |
| §2.8 | refund and reassociation settle independently | `ExecutableAncillaries`, per-coupon keys | `G2H3`, `G2C4` |
| §3 | the accepted plan carries the approved economics durably | `AcceptedExchangeAncillaryDisposition`, `AcceptedExchangePlanAncillaryRow` | `G2D3`, `G2C6` |
| §4 | EMD document lifecycle | `ElectronicMiscDocument.RefundCoupon`, `EmdCouponRefundRecord` | `G2H1`, `G2H2`, `G2F2` |
| §5 | reuse `IDocumentRefundPort`, no `IEmdRefundPort` | `Ports/DocumentRefund/` unchanged in shape | `Contracts/DocumentRefund/` |
| §6 | reuse `IRefundValuePort` for value movement | `Ports/RefundValue/` unchanged | `Contracts/RefundValue/` |
| §7 | exactly one pricing change per committed consequence | `Order.CommitDependentPriceChange` | `G2C1`, `G2C3`, `G2C4` |
| §7 | no duplicate consequence on replay | `RefundPriceChangeSetId` on the plan row | `G2C2`, `G2C5`, `G2C6` |
| §8 | orchestration sequence and commit checkpoint | `RefundAncillaryDocumentAsync` | `G2C1`, `G2C5` |
| §9 | edge-case matrix | §11 above | the whole `G2*` set |
| §10 | additive persistence only | `P3G2AncillaryRefundConsequence` | `has-pending-model-changes` |
| E.C1–C6 | the Option E commercial model | `Order.DependentPricing.cs` | `G2C1`–`G2C6` |

---

## 14. Defects Found And Fixed In This Slice

Three, all caught by tests in this working tree, none pre-existing in a frozen report:

1. **Refund dispositions were never disassociated.** `DisassociateAncillariesAsync` iterated
   `plan.Reassociations`, so a coupon bound for refund kept only its issue-time `Associated` provenance while
   the predecessor coupon it documented had become `Exchanged`. Now iterates `ExecutableAncillaries`.
   Caught by `G2F1`.
2. **A replay through materialization threw on a refunded coupon.** `MaterializeAsync` re-runs
   `DisassociateAncillariesAsync` on the already-materialized path (a G1 crash affordance). Once a coupon was
   refunded, `Associable` excluded it and the precheck raised `ElectronicMiscDocumentAssociationMoved`. Fixed
   with `IsDisassociationSettledBy` and by checking "already settled by this operation" before the
   `IsOpenForUse` gate. Caught by `G2V1`.
3. **A source-approved reversal link was silently dropped.** `AncillaryRefundPricingLine` mapped every field
   of the approved line except `ReversesPricingLineId`, `SettlementPartyRef` and `SettlementCategory`. The
   effect was fail-closed rather than wrong — the domain would have refused a `Reversal`-role line with
   `ReversalRequiresOriginalLine` — but dropping approved evidence is not acceptable. Now mapped, and scoped
   by `EnsureReversalsStayWithinTheDocument`.
4. **Two sites assumed one price change set per change.** `AlreadyMaterializedAsync` and
   `ReplayCompletedAsync` used `.Single(set => set.ChangeId == ...)`, and `OrderViewBuilder.BuildChanges` used
   `FirstOrDefault` with no ordering. All three now use `Order.OriginatingPriceConsequenceOf`. Caught by
   `G2C2`, `G2F1` and `G2V1`.

---

## 15. Exception Codes

One new code, allocated as the next free number, keeping 20000–29999 contiguous:

| Code | Factory | HTTP | Meaning |
| --- | --- | --- | --- |
| 20308 | `DependentPriceChangeHostNotResolvable` | 409 | no single order change for this operation and change type can carry a dependent price consequence |

Reused without change: 20306 `ElectronicMiscDocumentCouponIsNotRefundable`, 20307
`AncillaryRefundEconomicsMissing`, 20298 `AncillaryDispositionNotExecutable`, 20302
`ElectronicMiscDocumentAssociationMoved`, 20227 `RefundReversalOutsideDocumentScope`.

Highest allocated code is now **20308**. No duplicates, no gaps.

---

## 16. Files Changed

The earlier part of this slice — the EMD refund lifecycle, the accepted-plan refund evidence, the two provider
rails, `AncillaryRefundFlowTests`, and migrations `P3G2AncillaryRefund` and `P3G2AncillaryRefundEvidence` —
is already in the repository. This section lists the Option E resolution and the three defect fixes on top of it.

**New**

| File | Purpose |
| --- | --- |
| `src/AeroTech.Ordering.Domain/OrderAggregate/Arguments/AcceptedDependentPriceChangeArgs.cs` | the accepted dependent consequence |
| `src/AeroTech.Ordering.Domain/OrderAggregate/Order.DependentPricing.cs` | `CommitDependentPriceChange`, `PriceConsequencesOf`, `OriginatingPriceConsequenceOf` |
| `src/AeroTech.Ordering.Persistence/Migrations/20260912103714_P3G2AncillaryRefundConsequence.cs` | two additive nullable columns |
| `src/AeroTech.Ordering.Providers.Deterministic/DeterministicDocumentRefundOperation.cs` | the remembered document-refund intent |
| `tests/.../Contracts/DocumentRefund/DocumentRefundPortContract.cs` | the reusable nine-invariant kit |
| `tests/.../Contracts/DocumentRefund/DocumentRefundPortFixture.cs` | the kit's request, recovery and conflicting-intent fixtures |
| `tests/.../Contracts/DocumentRefund/DeterministicDocumentRefundPortTests.cs` | the deterministic adapter bound to the kit, plus six adapter-specific cases |
| `tests/.../Contracts/DocumentRefund/UnconfiguredDocumentRefundProviderTests.cs` | the 501 fail-closed refusal on all three acts |

**Modified**

| File | Change |
| --- | --- |
| `Domain/OrderAggregate/Order.Pricing.cs` | `AttachPriceChange` split so `AttachPriceConsequence` is shared |
| `Domain/ElectronicMiscDocumentAggregate/ElectronicMiscDocument.cs` | `IsDisassociationSettledBy`, corrected guard order in `DisassociateCouponByReissue` |
| `Domain/Ports/AncillaryDisposition/AncillaryRefundTerms.cs` | the source declares its `PricingSource` |
| `Domain/Servicing/Plans/AcceptedExchangeAncillaryDisposition.cs` | `RefundPricingSource`, `RefundPriceChangeSetId`, `IsRefundConsequenceCommitted` |
| `Domain/Servicing/Plans/AcceptedExchangePlan.cs` | removed the now-unused `SettledAncillaryRefunds` |
| `Domain/Servicing/Plans/Contracts/IAcceptedExchangePlanStore.cs` | `RecordAncillaryRefundConsequenceAsync` |
| `Domain/_Shared/Resources/ExceptionFactory.cs`, `ExceptionMessages.cs` | code 20308 |
| `Application/.../Exchange/ExchangeAncillaryPlanner.cs` | refuses a self-derived refund pricing source and an out-of-document reversal; carries the source through |
| `Application/.../Exchange/ExchangeService.cs` | the consequence commits with the document truth; disassociation covers every executable ancillary; originating-consequence lookups |
| `Persistence/Servicing/AcceptedExchangePlanAncillaryRow.cs`, `AcceptedExchangePlanStore.cs` | the two new columns and the consequence recording |
| `Providers.Deterministic/DeterministicAncillaryDispositionAdapter.cs` | declares the pricing source; adds the self-derived and out-of-document-reversal refusal shapes |
| `Providers.Deterministic/DeterministicDocumentRefundAdapter.cs` | durable-key shape: a repeated key returns the remembered result, a conflicting intent on a known key fails closed, an unresolved outcome resolves on read-back and a resolved one is never rewritten |
| `Synchronizer/OrderAggregate/OrderViewBuilder.cs` | deterministic originating consequence per change view |
| `tests/.../P3/AncillaryRefundFlowTests.cs` | 35 cases, including G2C1–C6 and G2S2 |

---

## 17. Benchmark Traceability

Narrow, refund-specific only. Primary IATA sources plus two GDS implementations.

| Finding | Verdict | Effect |
| --- | --- | --- |
| `'R'` is a final EMD coupon status, reachable only from `'O'` / `'A'` / `'Y'` | MUST NOW | `PermitsRefund` requires `IsOpenForUse`; `Associable` excludes a `Refunded` document |
| disassociation is an explicit precondition of EMD-A refund | MUST NOW | disassociation covers every executable ancillary at materialization (§4) |
| the validating carrier adjudicates the refund | MUST NOW | the disposition source owns amount, currency, disposition and pricing lines; Ordering derives none of them |
| partial coupon refund is **provider-specific**, not universal — IATA guidance permits it, Travelport forbids it, Sabre lets the carrier choose | MUST NOW | per-coupon refund is implemented; a host that forbids it rejects the act, which reconciles rather than corrupts |
| `'R'` has a carrier-side *Refund Cancel* before the revenue lift | DEFER | out of G2 scope; Ordering never un-refunds a coupon |
| "a refunded coupon cannot be reassociated" | INFERENTIAL | not literally stated by any consulted source; enforced because `'R'` is final |
| EMD-S residual issuance | NOT APPLICABLE | already frozen in P3-F |

---

## 18. Deferred — Not Approximated

* Refund Cancel of a refunded EMD coupon.
* `ExchangeToNewEmd`, `RetainAsResidual`, `Cancel`, `ManualReview` dispositions.
* An operator remediation command for a `NeedsReconciliation` ancillary refund.
* Positive service-level refund marking for an ancillary that survives a reissue — see the ICC entry's known
  gap; no shape in the current model produces one.
* A **positive** `Reversal`-role ancillary refund line. The link and the scope rule are implemented and the
  refusal is covered, but no positive case is asserted: in the current harness an EMD coupon's declared
  `PricingLineId` is a ticket fare line the reissue itself already reverses, so a full reversal of it at the
  ancillary stage would legitimately exceed the outstanding value. A faithful positive case needs an EMD whose
  charge line is its own, which no current shape produces.
* The remaining `ExchangeService` stage decomposition (still ~2100 lines). The stages are mutually recursive
  across eleven call sites, so extraction is a control-flow redesign, not a move. Flagged, not started.

---

## 19. ICC Changes

`ICC-P3-EMD-REFUND` added to
[P3-integration-capability-catalog.md](P3-integration-capability-catalog.md), with the commercial model, both
reused ports, the operation-key shape, the idempotency proof, the deterministic coverage, five
`BLOCKED_INTEGRATION` items and five known semantic gaps. The catalog's entry index and capability-scope
preamble were updated.

---

## 20. Freeze Verdict

```text
P3-G2 READY TO FREEZE: YES
```

`AncillaryExchangeDisposition.Refund` is fully executable. The commercial consequence follows the Option E
model — one Exchange `OrderChange`, one Exchange `PriceChangeSet`, and one appended `PriceChangeSet` per
independently confirmed ancillary refund — with durable per-coupon idempotency, recover-first on both provider
rails, and no rollback of document, ticket or commercial truth on any downstream failure.

No frozen invariant was weakened. No index was relaxed, no child servicing operation was created, no claim
framework was added, and no second `OrderChange` exists for an exchange operation.

Four defects were found and fixed inside this slice, each caught by a test in this working tree: the missing
disassociation of refund dispositions, the replay that threw on a refunded coupon, the dropped reversal link,
and three sites that assumed one price change set per change. One gap in the freeze gate itself was closed:
`IDocumentRefundPort` had no contract test kit and its unconfigured provider had no test — both now exist.

Everything deliberately not implemented is listed in §18 and in the ICC entry's known gaps. Nothing is a
placeholder: each is either refused explicitly with a code, or recorded as unreachable with the reason.
