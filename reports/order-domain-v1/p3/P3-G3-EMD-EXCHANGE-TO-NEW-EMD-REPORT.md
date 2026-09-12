# P3-G3 — EMD-A Exchange / Reissue To A New EMD

Closing report for P3-G3, the third slice of P3-G ancillary servicing: making
`AncillaryExchangeDisposition.ExchangeToNewEmd` fully executable.

Companion documents, all in this folder:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, now carrying `ICC-P3-EMD-EXCHANGE`.
* [P3-G2-EMD-A-REFUND-REPORT.md](P3-G2-EMD-A-REFUND-REPORT.md) — the frozen G2 baseline whose Option E commercial model this slice reuses.
* [P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md](P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md) — the frozen G1 association lifecycle.
* [P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md](P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md) — the frozen P3-F ticket exchange baseline.

---

## 1. Starting State

```text
Actual starting HEAD    ad61e757b26bbea91ff68d12646967ae4fa61b0a  P3-G2 — RESUME AND FINISH
Brief's frozen baseline ad61e757b26bbea91ff68d12646967ae4fa61b0a
Delta                   none — HEAD matched the brief exactly
Working tree at start   clean
```

Inherited frozen baseline: every P3-F exchange shape; the accepted-plan rail with per-stage durable evidence;
recover-first with `WasDispatched` on every provider rail; the G1 EMD-A association lifecycle; the G2 refund
lifecycle; and the G2 Option E commercial model (`OrderChange` = servicing-operation envelope,
`OrderPriceChangeSet` = independently committed financial consequence).

After this slice the executable ancillary dispositions are `ReassociateExisting`, `Refund` and
`ExchangeToNewEmd`. `RetainAsResidual`, `Cancel` and `ManualReview` stay refused with
`AncillaryDispositionNotExecutable` (20298).

---

## 2. Source / Authority Model

`ExchangeToNewEmd` is never executable from the enum alone. The ancillary disposition result now carries
`AncillaryEmdExchangeTerms`:

```csharp
public sealed record AncillaryEmdExchangeTerms(
    string ExchangeGroupRef,
    ElectronicMiscDocumentType SuccessorType,
    string SuccessorReasonForIssuanceCode,
    int CurrencyId,
    IReadOnlyList<AncillaryEmdExchangeSuccessorCoupon> SuccessorCoupons,
    string SourceReference,
    PricingSource PricingSource,
    IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
    AcceptedAddCollect? AddCollect = null,
    AcceptedRefundDue? RefundDue = null,
    AcceptedResidual? Residual = null,
    string? FundingMethodRef = null);
```

`ExchangeAncillaryPlanner.EnsureEmdExchangeIsExecutable` refuses, **before the ticket document exchange is
dispatched**, a decision missing any of: terms, a group reference, a successor RFIC, a defined successor type,
a currency, a source reference, an external pricing source, successor coupons, a per-coupon RFISC, a
non-negative per-coupon value, a per-coupon currency matching the group, an order service for a `Service`
purpose coupon, an external value reference for a `Deposit`/`ResidualValue` purpose coupon, or a funding
method when the group collects. All of those are `AncillaryExchangeTermsMissing` (20311, 422).

Nothing is derived from the predecessor EMD — not the value, not the RFIC/RFISC, not refundability, not the
monetary shape. `PricingSource.OrderingDerived` is refused outright.

### Grouping

The source decides that one or more of its coupons are exchanged in **one** accountable-document transaction.
That decision is first-class and durable:

```text
one accepted exchange group = one IEmdExchangePort operation = one successor document = one consequence
```

`ExchangeAncillaryPlanner.AcceptExchangeGroups` groups the accepted dispositions by `ExchangeGroupRef` and
refuses, with `AncillaryExchangeGroupMalformed` (20312, 422), a group that spans more than one source
document, whose members carry conflicting successor terms, whose associated successor coupon names no target
ticket coupon, whose target is historical used context, or whose target is outside the accepted successor
scope. Every executable exchange coupon belongs to exactly one group, and group membership round-trips
through `AcceptedExchangePlanAncillaryExchangeGroups`.

**Group coherence is a structural comparison, not record equality.** `AncillaryEmdExchangeTerms` holds list
members, and C# record equality compares those by reference — a real source sending separate-but-equal lists
per member would have been rejected. `SameGroupTerms` compares the successor coupons and pricing lines with
`SequenceEqual` and the scalars by value.

---

## 3. `IEmdExchangePort`

`src/AeroTech.Ordering.Domain/Ports/EmdExchange/` — `ExchangeAsync` plus `RecoverAsync`, the same durable
two-method shape as every other P3 rail.

`IEmdIssuancePort` is deliberately **not** reused. Issuing a document and then locally flipping the
predecessor to `Exchanged` cannot prove the atomic accountable-document exchange that the `E` status depends
on; only the authority can.

`ExchangeCoupledResidualRequest` and `ResidualDocumentIdentity` **are** reused from `Ports/DocumentExchange/`:
"an exchange-coupled residual obligation" and "the accountable residual document that answered it" mean the
same thing for a ticket and for an EMD, so duplicating them for naming symmetry was declined per §4.

**The request carries no successor document number.** The confirmed result supplies it and it is
authoritative. The contract kit asserts this directly.

| Request binds | Confirmed result returns |
| --- | --- |
| operation key, order id, servicing operation id | provider reference |
| accepted exchange-group reference | successor document number, type, issuer carrier, issuing office, authority, RFIC, currency |
| source document number + source coupon scope | one coupon identity per accepted successor coupon: number, purpose, RFISC, value, currency |
| beneficiary/traveller | the associated ticket coupon number for an `Associated` successor |
| successor type, RFIC, currency, accepted successor coupons | the associated ticket document number for an `Associated` successor |
| successor ticket document number for `Associated` | the coupled residual document identity, when one was requested |
| source decision + pricing references, optional coupled residual obligation | |

Operation keys are server-derived and stable per group, so ticket monetary acts, different EMD groups and the
G2 refund rails can never collide:

```text
emd-exchange:{ExchangeGroupRef}
emd-exchange-guarantee:{ExchangeGroupRef}
emd-exchange-capture:{ExchangeGroupRef}
emd-exchange-residual:{ExchangeGroupRef}
```

`UnconfiguredEmdExchangeProvider` fails closed with `EmdExchangeSourceNotConfigured` (20313, 501).
`DeterministicEmdExchangeAdapter` preserves request identity by operation key: a repeated key returns the
remembered result, and a conflicting immutable intent on a known key throws rather than overwriting.

---

## 4. Predecessor Lifecycle And Explicit Lineage

Enum values were **appended only**, never renumbered:

```text
EmdCouponStatus.Exchanged = 4
ElectronicMiscDocumentStatus.Exchanged = 4
```

`ElectronicMiscDocument.ExchangeCoupons(IReadOnlyList<EmdCouponExchange>, IClock)` is the group-level
transition. Its rules:

| Rule | Behaviour |
| --- | --- |
| a `Voided` document is never exchanged | `ElectronicMiscDocumentCouponIsNotExchangeable` (20309, 409) |
| a coupon that is not `OpenForUse` is never exchanged | 20309 |
| an exact replay by the same operation | no-op, no second version bump |
| a **conflicting** successor for a coupon this operation already exchanged | `ElectronicMiscDocumentExchangeConflict` (20310, 409) |
| one predecessor coupon exchanged twice | impossible — the second attempt is either the idempotent no-op or 20310 |
| partial exchange of a multi-coupon EMD | only the approved coupons transition; the document summary becomes `Exchanged` only when **every** coupon is |
| `DocumentVersion` | advances exactly once for one locally committed exchange mutation of the group, and not again on replay |

Lineage is durable and queryable in **both** directions without reconstructing anything:

```text
predecessor -> successor   EmdCoupon.ExchangeRecord
                           (OperationId, SuccessorElectronicMiscDocumentId, SuccessorDocumentNumber,
                            SuccessorCouponNumber, DecisionReference, ProviderReference, ExchangedAt)

successor -> predecessor   EmdCoupon.PredecessorElectronicMiscDocumentId
                           EmdCoupon.PredecessorDocumentNumber
                           EmdCoupon.PredecessorCouponNumber
```

No predecessor history is deleted or overwritten. The G1 `DisassociatedByReissue` record stays exactly once,
and an exchanged coupon is excluded from reassociation, refund and further exchange by any other operation.

---

## 5. Successor Materialization And Association At Issuance

A successor is materialized only after an **uncontradicted** `Confirmed` result.

For an `Associated` successor, each successor coupon is created already bound to the approved successor ticket
coupon and records that binding as its issuance association history. `IEmdAssociationPort` is **not** called —
representing one accountable act as an issuance plus a separate reassociation would be a lie about what
happened. For a `Standalone` successor no ticket association is fabricated, and a provider that echoes one is
a contradiction.

The successor type comes from the accepted terms and the confirmed result, never from the disposition name.

**Existing document number.** An exact replay of the same provider-confirmed successor is a no-op. If the
number exists locally but immutable identity differs, `ElectronicMiscDocumentIdentityPolicy.Conflict` names
the first differing facet — type, currency, issuer carrier, issuing office, authority, RFIC, coupon count,
exchange lineage, or a per-coupon purpose/RFISC/value/currency — and the operation becomes
`NeedsReconciliation`. No second document number is ever generated.

---

## 6. Commercial Consequence — Option E, Unchanged

```text
one Exchange servicing operation
  -> one Exchange OrderChange                       (unchanged, G1-frozen)
  -> originating ticket-exchange PriceChangeSet
  -> G2 ancillary-refund PriceChangeSet(s), if any
  -> G3 ancillary-EMD-exchange PriceChangeSet(s), if any
```

Each independently confirmed group appends exactly one dependent `PriceChangeSet` through the frozen
`Order.CommitDependentPriceChange`, with `Reason = Exchange`, `Source` = the source-approved external pricing
authority, `SourcePricingRef` = the accepted ancillary exchange reference, and exactly the source-approved
lines. Each gets one new `FinancialSequence`, advances `CommercialVersion` exactly once, emits exactly one
`OrderPricingChanged` carrying the **existing** `OrderChange.Id` and the new `PriceChangeSet.Id`, never
touches the originating ticket-exchange set, and never duplicates on replay — proven from
`AcceptedExchangePlanAncillaryExchangeGroups.PriceChangeSetId`.

**No zero-value lines were invented.** `StagePriceChange` refuses an empty line set, so a consequence is
committed only when the source supplies lines. An even exchange is represented the way a line-grain ledger
represents one: a credit withdrawing the source value and a debit granting the successor value, netting zero.
A group whose monetary obligation is non-zero but which supplies no lines is refused at acceptance.

**A dedicated conservation rule.** `RefundConservationPolicy` is sign-correct for a refund and wrong for a
collection, so `AncillaryExchangeConservationPolicy` was added: expected net customer credit is
`RefundDue + Residual − AddCollect`, and zero for an even exchange. A mismatch is
`AncillaryExchangeAmountDoesNotReconcile` (20314, 422).

---

## 7. Monetary Sequencing

Monetary treatment is not collapsed into a signed delta. Per group:

```text
funding guarantee (when the group collects)
 -> IEmdExchangePort exchange act
 -> ONE local checkpoint
 -> capture
 -> external residual (only when the source says the residual is non-document value)
```

`IExchangeFundingPort` and `IExchangeResidualValuePort` are reused with group-scoped keys rather than
duplicated. Their P3-F request field names (`QuotedExchangeId`, `PredecessorDocumentNumber`,
`SuccessorDocumentNumber`) carry the exchange-group reference and the EMD document numbers — a naming
imprecision in a frozen contract, not a semantic one; renaming a frozen port is out of scope under §16.

### The atomic local checkpoint

One unit of work, in this order:

```text
1. the G3 dependent PriceChangeSet                (first, so a Fee-purpose successor coupon can bind its line)
2. the successor EMD, with association at issuance and predecessor lineage on every coupon
3. the group's successor identity on the accepted plan
4. every source coupon -> Exchanged, with successor lineage
5. the coupled residual EMD-S, when the accepted obligation is document-coupled
6. SaveChangesAsync
```

Only after this may capture or external value movement run. A crash before the save is recovered from the
provider operation and materializes the same truth exactly once; a crash after it resumes past materialization
and never exchanges or issues again.

### Nothing rolls back

If capture or external residual later returns `Pending`, `Unknown`, `Rejected` or contradictory evidence:

| | |
| --- | --- |
| predecessor EMD coupons | stay `Exchanged` |
| successor EMD | stays authoritative |
| lineage | stays |
| G3 `PriceChangeSet` | stays committed |
| predecessor ticket | stays `Exchanged` |
| successor ticket | stays authoritative |
| operation | `AwaitingExternal` or `NeedsReconciliation` |

---

## 8. Coupled Residual Handling

A source-approved residual fulfilled as an accountable EMD-S coupled to this exchange is requested **and
recovered in the same `IEmdExchangePort` operation**, and materialized in the same local checkpoint. No second
document or value issuance is ever dispatched for that obligation — the P3-F ticket-residual defect is not
repeated.

| Case | Behaviour |
| --- | --- |
| required coupled residual returned correctly | materialized in the same checkpoint; `IExchangeResidualValuePort` never called |
| required coupled residual missing | contradiction; no exchange re-dispatch; `NeedsReconciliation` |
| coupled residual returned when nothing is owed | contradiction; `NeedsReconciliation`; no later residual or value dispatch |
| residual document number exists locally and is not that residual | contradiction; `NeedsReconciliation` |
| source says the residual is external/non-document value | `IExchangeResidualValuePort` runs, but only after document truth is durable |

---

## 9. Provider Contradiction Handling

A `Confirmed` response is never accepted blindly. `AncillaryExchangeEvidencePolicy.Contradiction` checks:
provider reference present; successor identity present; document number present; successor type, currency and
RFIC match the accepted group; coupon count matches; and per coupon the purpose, RFISC, value, currency and —
for an `Associated` successor — the associated ticket coupon and document.

**The echoed association is compared against the successor ticket coupon number**, resolved from the accepted
target predecessor coupon through the reissue's own coupon mapping. Comparing it against the predecessor
number would have been wrong for every reissue whose successor renumbers coupons.

On contradiction: the predecessor ticket stays `Exchanged`, the successor ticket stays authoritative, the G1
disassociation stays durable, the exchange port is **not** called again, no local successor truth is
fabricated, the evidence is persisted, the operation becomes `NeedsReconciliation`, and no funding capture or
value act runs for that group. Local truth already durably materialized before a later-stage contradiction is
never rolled back.

---

## 10. Pending / Unknown / Rejected

| Outcome | Source coupon | Successor | G3 consequence | Money | Operation |
| --- | --- | --- | --- | --- | --- |
| `Pending` / `Unknown` | `OpenForUse`, disassociated | none | none | none | `AwaitingExternal`, claim retained, restart recovers first |
| `Rejected` | `OpenForUse`, disassociated, **not** marked Exchanged | none | none | none | `NeedsReconciliation` |

A provider rejection is never reinterpreted as a successful cancellation or refund of the ancillary.

---

## 11. Deterministic Processing With Mixed Dispositions

`AcceptedExchangePlan.NextUnsettledAncillary` is the single deterministic selector. For a reassociation or
refund it asks the disposition; for an exchange it asks the **group**, because the group is the unit of work.
A `Pending`/`Unknown` stage stops further irreversible ancillary dispatch for the operation until replay
resolves it, and completed dispositions are never replayed. No affected ancillary is left implicitly untouched.

---

## 12. Mandatory Edge-Case Matrix → Test

| # | Brief case | Test |
| --- | --- | --- |
| 1 | one EMD-A → associated replacement EMD-A | `G3H1` |
| 2 | EMD-A → source-approved EMD-S, no ticket association | `G3H2` |
| 3 | multiple source coupons, one group, one act, one consequence | `G3H3` |
| 4 | two independent groups → two acts, two distinct sequences | `G3H4` |
| 5 | refund + reassociation + EMD exchange in one reissue | `G3H5` |
| 6 | multi-coupon source, only approved coupons Exchanged | `G3H6` |
| 7 | incomplete authoritative terms fail before ticket exchange | `G3S1` (7 shapes, 20311) |
| 8 | duplicate / unrelated group coupon fails first | `G3S1` conflicting-group-terms, `G3S3` (20312) |
| 9 | EMD-A successor with missing/wrong target coupon | `G3S1` no-target-coupon, unrelated-target-coupon (20312) |
| 10 | self-derived exchange economics fail closed | `G3S1` ordering-derived (20311) |
| 11 | malformed monetary shape fails before dispatch | `G3S2` (20314), `G3S1` no-pricing-evidence |
| 12 | `Pending` → AwaitingExternal, nothing materialized | `G3P1` |
| 13 | `Unknown` → same | `G3P1` |
| 14 | `Rejected` → ticket retained, detached, reconciliation | `G3P2` |
| 15 | throw-before-dispatch → dispatched once after recovery | `G3P3` |
| 16 | throw-after-dispatch → same successor, no second act | `G3P4` |
| 17 | confirmed with missing/wrong evidence → reconciliation | `G3P5` (8 shapes) |
| 18 | same key, conflicting intent → adapter fails closed | `G3P6`, `Contracts/EmdExchange` |
| 19 | exact confirmed replay creates nothing twice | `G3R1` |
| 20 | existing document number, conflicting truth | `G3R2` |
| 21 | predecessor already Refunded/Void/Exchanged fails closed | `G3R3`, `G3R4` |
| 22 | AddCollect: guarantee → exchange → materialize → capture | `G3M1` |
| 23 | capture `Pending`/`Unknown` retains all truth | `G3M2` |
| 24 | capture `Rejected` retains truth, reconciles | `G3M2` |
| 25 | required coupled residual materialized in one checkpoint | `G3M3` |
| 26 | coupled residual missing → no redispatch, reconciliation | `G3M4` |
| 27 | unexpected coupled residual → reconciliation, no value | `G3M5` |
| 28 | predecessor ETKT stays Exchanged on every ancillary failure | `G3P2`, `G3P5`, `G3M2`, `G3R3` |
| 29 | successor ETKT never recreated | `G3F1`, `G3R1` |
| 30 | G1 `DisassociatedByReissue` remains exactly once | `G3R1`, `G3P2` |
| 31 | G2 refund behaviour unchanged | `AncillaryRefundFlowTests` 35/35 |
| 32 | G1 reassociation unchanged | `EmdReassociationFlowTests` 29/29 |
| 33 | revalidation unchanged | full Persistence run |
| 34 | completed replay moves no document and no money | `G3F1` |

`G3R3` was rewritten during this pass: a coupon that is *already* terminal before quoting is not an affected
ancillary at all (G1 scope requires `IsOpenForUse`), so the fail-closed guard is only reachable when the
coupon turns terminal **after** acceptance. `G3R3` now crashes inside the ancillary stage, turns the coupon
terminal, and resumes — which is the real case — and `G3R4` asserts the separate truth that an already-terminal
coupon is never an affected ancillary and reaches no provider.

Also covered beyond the brief: `G3F2` proves lineage in both directions and that `DocumentVersion` does not
move again on replay; `G3F3` proves an exchanged coupon is refused for reassociation, refund and re-exchange
by any other operation while staying idempotent for its own.

---

## 13. Persistence And Migration Summary

One additive migration, `P3G3AncillaryEmdExchange`:

| Operation kind in `Up` | Count |
| --- | --- |
| `CreateTable` | 1 — `Order.AcceptedExchangePlanAncillaryExchangeGroups` |
| `AddColumn` | 11 — 7 `Exchange*`/`Predecessor*` on `Order.EmdCoupons`, 1 `ExchangeGroupRef` on `Order.AcceptedExchangePlanAncillaries`, 3 owned-record scalars |
| `CreateIndex` | 5 |
| destructive operations | **0** |

No historical migration was edited. No enum value was renumbered. Indexes were added only for the identity,
replay and lineage queries this slice actually performs: the group's source and successor document, the group
reference on the ancillary row, and the successor coupon's predecessor document.

```text
dotnet ef migrations has-pending-model-changes
  -> No changes have been made to the model since the last migration.
```

---

## 14. Requirement → Code → Test Traceability

| Brief | Requirement | Code | Test |
| --- | --- | --- | --- |
| §1 | `ExchangeToNewEmd` becomes executable | `ExchangeAncillaryPlanner.EnsureExecutable`, `ExchangeService.AncillaryExchange.cs` | `G3H1`, `AncillaryDispositionGateTests.P` |
| §2.2 | a successful exchange creates a new EMD and moves the predecessor to `E` | `ElectronicMiscDocument.ExchangeCoupons`, `MaterializeSuccessorEmdAsync` | `G3H1`, `G3F2` |
| §2.3 | disassociation precedes exchange | `DisassociateAncillariesAsync` over `ExecutableAncillaries` (G1) | `G3R1`, `G3P2` |
| §2.4 | all four A/S exchange shapes are permitted | `SuccessorType` carried from the source, never inferred | `G3H1`, `G3H2` |
| §2.5 | association is part of successor issuance | `SuccessorCoupons(...)` + `RecordIssuedAssociation` | `G3H1` (`EmdAssociations` untouched) |
| §2.6 | the authority decides eligibility | no local eligibility inference anywhere on this rail | `G3P2`, `G3P5` |
| §2.7 | Ordering never generates a provider document number | `EmdExchangeRequest` has no such field | `Contracts/EmdExchange` |
| §2.8 | a coupled residual belongs to the same transaction | `ExchangeCoupledResidualRequest` on the exchange request | `G3M3`, `G3M4` |
| §3 | authoritative terms sufficient to replay | `AncillaryEmdExchangeTerms` → `AcceptedExchangeAncillaryExchangeGroup` → its own table | `G3P4`, `G3R1` |
| §3 | stable grouping semantics | `AcceptExchangeGroups`, `SameGroupTerms` | `G3H3`, `G3H4`, `G3S1`, `G3S3` |
| §4 | capability-oriented provider-neutral port | `Ports/EmdExchange/` | `Contracts/EmdExchange` 18/18 |
| §4 | unconfigured fails closed | `UnconfiguredEmdExchangeProvider` | `UnconfiguredEmdExchangeProviderTests` |
| §4 | deterministic key identity | `DeterministicEmdExchangeAdapter.Remember` | `G3P6` |
| §5 | appended enum values, explicit lineage | `EmdCouponStatus.Exchanged`, `EmdCouponExchangeRecord`, `Predecessor*` | `G3F2`, `G3F3` |
| §5 | one version bump, no-op on replay | `ExchangeCoupons` | `G3F2` |
| §6 | successor materialization and identity collision | `MaterializeSuccessorEmdAsync`, `ElectronicMiscDocumentIdentityPolicy` | `G3H1`, `G3H2`, `G3R2` |
| §7 | one dependent `PriceChangeSet` per group | `CommitAncillaryExchangeConsequenceAsync` | `G3H3`, `G3H4`, `G3R1` |
| §8 | monetary families and sequencing | `ExchangeService.AncillaryExchangeMonetary.cs` | `G3M1`, `G3M2`, `G3M6` |
| §8 | coupled residual never fulfilled twice | materialized in the checkpoint; `IExchangeResidualValuePort` not called | `G3M3` |
| §9 | atomic local checkpoint | `MaterializeAncillaryExchangeAsync` + one `SaveChangesAsync` | `G3P4`, `G3M2` |
| §10 | contradiction handling | `AncillaryExchangeEvidencePolicy` | `G3P5` (8 shapes), `G3M4`, `G3M5` |
| §11 | Pending/Unknown/Rejected semantics | `DispatchAncillaryExchangeAsync` | `G3P1`, `G3P2` |
| §12 | deterministic mixed dispositions | `AcceptedExchangePlan.NextUnsettledAncillary` | `G3H5` |
| §14 | additive persistence only | `P3G3AncillaryEmdExchange` | `has-pending-model-changes` |

---

## 15. Defects Found And Fixed In This Pass

Four, all in code written during this slice, each caught by a test in this working tree:

1. **The completion hand-off fell through.** A settled exchange group returned into the reassociation
   dispatcher, whose "nothing pending" branch reconciles — so a fully successful exchange reported
   `NeedsReconciliation`. It now mirrors the G2 pattern: complete when `plan.IsAncillarySettled`, otherwise
   continue to the next ancillary.
2. **The contradiction policy compared the wrong coupon number.** The echoed successor association was
   compared against the accepted *predecessor* ticket coupon number. It happened to pass for a single-coupon
   ticket and failed for every reissue that renumbers coupons. The service now resolves the expected
   **successor** coupon number once and uses it for both the request and the check.
3. **Group coherence used record equality.** C# records compare list members by reference, so a source
   sending separate-but-equal successor coupon or pricing line lists per group member would have been rejected
   as "conflicting terms". Replaced with a structural comparison.
4. **The conservation check had the wrong sign for a collection.** `RefundConservationPolicy` expects net
   customer *credit*; an add-collect is a debit. `AncillaryExchangeConservationPolicy` was added rather than
   bending the refund rule.

---

## 16. Exception Codes

Six new codes, allocated as the next free numbers, keeping 20000–29999 contiguous:

| Code | Factory | HTTP | Meaning |
| --- | --- | --- | --- |
| 20309 | `ElectronicMiscDocumentCouponIsNotExchangeable` | 409 | the document or coupon state forbids exchange |
| 20310 | `ElectronicMiscDocumentExchangeConflict` | 409 | an already-exchanged coupon cannot name a different successor |
| 20311 | `AncillaryExchangeTermsMissing` | 422 | the source-approved exchange terms are incomplete |
| 20312 | `AncillaryExchangeGroupMalformed` | 422 | the accepted exchange group is not internally coherent |
| 20313 | `EmdExchangeSourceNotConfigured` | 501 | no miscellaneous document exchange authority is wired |
| 20314 | `AncillaryExchangeAmountDoesNotReconcile` | 422 | the approved lines do not net to the approved obligation |

Highest allocated code is now **20314**. No duplicates, no gaps. Reused without change: 20298
`AncillaryDispositionNotExecutable`, 20302 `ElectronicMiscDocumentAssociationMoved`.

---

## 17. Files Changed

**New (26)**

| Area | Files |
| --- | --- |
| Port | `Domain/Ports/EmdExchange/` — `IEmdExchangePort`, `EmdExchangeRequest`, `EmdExchangeSuccessorCouponRequest`, `EmdExchangeRecoveryRequest`, `EmdExchangeResult`, `EmdExchangeRecovery`, `SuccessorEmdIdentity`, `SuccessorEmdCouponIdentity` |
| Source terms | `Domain/Ports/AncillaryDisposition/AncillaryEmdExchangeTerms.cs`, `AncillaryEmdExchangeSuccessorCoupon.cs` |
| Domain lifecycle | `Domain/ElectronicMiscDocumentAggregate/Arguments/EmdCouponExchange.cs`, `Entities/EmdCouponExchangeRecord.cs` |
| Servicing plan | `Domain/Servicing/Plans/AcceptedExchangeAncillaryExchangeGroup.cs`, `AcceptedExchangeAncillarySuccessorCoupon.cs` |
| Policies | `Domain/Servicing/Plans/Policies/AncillaryExchangeEvidencePolicy.cs`, `ElectronicMiscDocumentIdentityPolicy.cs`, `Domain/OrderAggregate/Policies/AncillaryExchangeConservationPolicy.cs` |
| Orchestration | `Application/.../Exchange/ExchangeService.AncillaryExchange.cs`, `ExchangeService.AncillaryExchangeMaterialization.cs`, `ExchangeService.AncillaryExchangeMonetary.cs` |
| Persistence | `Persistence/Servicing/AcceptedExchangePlanAncillaryExchangeGroupRow.cs`, `...GroupConfiguration.cs`, migration `20260912*_P3G3AncillaryEmdExchange` |
| Providers | `Providers.Deterministic/DeterministicEmdExchangeAdapter.cs`, `DeterministicEmdExchangeOperation.cs`, `Providers/Unconfigured/UnconfiguredEmdExchangeProvider.cs` |
| Tests | `tests/.../P3/EmdExchangeToNewEmdFlowTests.cs`, `tests/.../Contracts/EmdExchange/` (4 files) |

`ExchangeService` was made `partial` so the three G3 stage files sit beside it rather than growing a
2100-line file further. No unrelated port or file was refactored; the one-type-per-port-file layout from
before G2 is untouched.

**Modified (16)**: the two enums; `AncillaryCouponDisposition`; `EmdCoupon`; `ElectronicMiscDocument`;
`AcceptedExchangeAncillaryDisposition`; `AcceptedExchangePlan`; `IAcceptedExchangePlanStore`;
`ExceptionFactory` + `ExceptionMessages`; `ExchangeAncillaryPlanner`; `ExchangeService`;
`ExchangeOperationKeys`; `AcceptedExchangePlanRow` + its configuration; `AcceptedExchangePlanAncillaryRow` +
its configuration; `AcceptedExchangePlanStore`; `ElectronicMiscDocumentConfiguration`;
`DeterministicAncillaryDispositionAdapter`; `Providers/DependencyInjection`; `OrderSliceHarness`;
`ExchangeScenarios`; `AncillaryDispositionGateTests` (the `ExchangeToNewEmd` row removed from the
not-executable theory, as §1 mandates).

---

## 18. Regression Results

Every figure is from an actual run on this working tree.

| Suite | Result |
| --- | --- |
| `EmdExchangeToNewEmdFlowTests` (G3 focused) | **48 passed, 0 failed, 0 skipped** |
| `Contracts/EmdExchange/` (new port contract kit) | **18 passed, 0 failed, 0 skipped** |
| `AncillaryRefundFlowTests` (G2) | 35 passed, 0 failed, 0 skipped |
| `EmdReassociationFlowTests` (G1) | 29 passed, 0 failed, 0 skipped |
| `AncillaryDispositionGateTests` | 22 passed, 0 failed, 0 skipped |
| `PostDocumentTruthFreezeGateTests` | 11 passed, 0 failed, 0 skipped |
| `ResidualDocumentCouplingTests` (P3-F coupled residual) | 15 passed, 0 failed, 0 skipped |
| `ResidualEvidenceFreezeGateTests` (P3-F) | 12 passed, 0 failed, 0 skipped |
| `MixedExchangeFlowTests` (P3-F) | 53 passed, 0 failed, 0 skipped |
| `ExchangeFlowTests` (P3-F) | 32 passed, 0 failed, 0 skipped |
| `RefundDueExchangeFlowTests` (P3-F) | 25 passed, 0 failed, 0 skipped |
| `AddCollectFundingRecoveryTests` (crash/recovery identity) | 22 passed, 0 failed, 0 skipped |
| `Contracts/ExchangeFunding/` | 24 passed, 0 failed, 0 skipped |
| `Contracts/ExchangeResidual/` | 12 passed, 0 failed, 0 skipped |
| `Contracts/DocumentRefund/` | 17 passed, 0 failed, 0 skipped |
| `Contracts/RefundValue/` | 11 passed, 0 failed, 0 skipped |
| `Contracts/EmdAssociation/` | 15 passed, 0 failed, 0 skipped |
| `Contracts/DocumentExchange/` | 7 passed, 0 failed, 0 skipped |
| `Contracts/AncillaryDisposition/` | 20 passed, 0 failed, 0 skipped |
| **`AeroTech.Ordering.Domain.Tests` (full)** | **511 passed, 0 failed, 0 skipped** |
| **`AeroTech.Ordering.Persistence.Tests` (full)** | **1113 passed, 0 failed, 0 skipped** (11.3 min) |
| `dotnet build AeroTech.Ordering.sln` | Build succeeded, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." |

The per-suite rows are broken out of the single full Persistence run so every item the freeze gate names has
its own figure. `Failed: 0` across the whole project means nothing outside this slice regressed.

---

## 19. Benchmark Traceability

| Finding (IATA Airline Guide to EMD Implementation §5.2.2; Travelport EMD reference) | Verdict | Effect |
| --- | --- | --- |
| EMD exchange is an accountable-document exchange, not ordinary issuance | MUST NOW | dedicated `IEmdExchangePort`; `IEmdIssuancePort` explicitly not reused |
| a successful exchange creates a new EMD and moves the predecessor coupon to final `E` | MUST NOW | `ExchangeCoupons` + `EmdCouponStatus.Exchanged` |
| an EMD-A must be disassociated before exchange | MUST NOW | G1 disassociation covers every executable ancillary at materialization |
| A→A, A→S, S→A, S→S are all industry-valid when the authority permits | MUST NOW | successor type is carried from the source, never inferred or restricted in Domain |
| association to the target ETKT is part of successor issuance | MUST NOW | association at issuance; `IEmdAssociationPort` not called |
| the validating/document authority decides acceptance | MUST NOW | no local eligibility inference on this rail |
| the successor EMD number is provider evidence | MUST NOW | no such field on the request; the result is authoritative |
| a residual EMD-S produced by the exchange belongs to the exchange transaction | MUST NOW | coupled residual in the same operation and the same checkpoint |
| provider products may restrict specific shapes | NOT APPLICABLE to Domain | such a restriction belongs to the provider result or ACL; recorded as `BLOCKED_INTEGRATION` item 2 |
| an exchange has no documented pre-revenue reversal equivalent to Refund Cancel | DEFER | Ordering never un-exchanges a coupon |

---

## 20. Explicitly Out Of Scope

Not implemented, per §16, and refused or absent rather than approximated: `RetainAsResidual`, `Cancel`,
`ManualReview`, Refund Cancel, wallet / travel-bank / credit-shell ownership, any generic accountable-document
hierarchy, any generic ancillary workflow engine, a public standalone EMD-exchange API, involuntary servicing,
DCS/disruption integration, EMD-S fee or penalty documentation unrelated to this exchange result, and MCO
lifecycle.

---

## 21. Freeze Verdict (superseded by §22 — see the consolidated correction below)

The first delivery reported `YES`. That verdict is **withdrawn**: a consolidated review found ten production
blockers, listed and fixed in §22. The binding verdict is §23.

`ExchangeToNewEmd` is fully executable for every supported shape. No Ordering-owned defect and no placeholder
remains. Every supported shape is replay-safe and proven from persisted state. No provider document number is
locally invented. No confirmed EMD exchange can be double-dispatched. Predecessor/successor lineage is durable
and queryable in both directions. Mixed G1/G2/G3 ancillary dispositions are deterministic. A coupled residual
cannot be fulfilled twice. Every frozen ticket and EMD truth survives downstream money failure.

---

## 22. Final Consolidated Freeze Correction

```text
Correction baseline    9f0cb7b68d6f79b61a523fe970da36c8aaae7038  P3-G3-EMD Exchange
Delta to the brief     none — HEAD matched exactly
Working tree at start  clean
```

Ten production blockers were found in the first G3 delivery and fixed in this single correction pass. Every
G1, G2 and P3-F semantic is unchanged.

### 22.1 RefundDue is now executed, not merely accepted

The first delivery persisted `RefundDueAmount/Currency/Disposition` on the group and never called a value
rail — a source-approved refund obligation was silently dropped. `IRefundValuePort` is now reused with a
group-scoped stable key:

```text
emd-exchange-refund:{ExchangeGroupRef}
```

`SettleAncillaryExchangeRefundDueAsync` is recover-first, persists `RefundDueOutcome/Reference/Detail`, and the
group exposes `RequiresRefundDue`, `IsRefundDueSettled`, `IsRefundDueRejected`. `IsSettled` now requires
`(!RequiresRefundDue || IsRefundDueSettled)`, and `IsRejected` includes a refused refund.

```text
RefundDue only:            EMD exchange -> local EMD truth + lineage + PriceChangeSet -> RefundValue -> complete
AddCollect + RefundDue:    guarantee -> EMD exchange -> local truth -> capture -> RefundValue -> complete
```

`AncillaryExchangeEvidencePolicy.RefundDueContradiction` requires a value movement reference, an exact amount,
an exact currency, and an exact disposition when echoed. `Pending`/`Unknown` hold the operation at
`AwaitingExternal`; `Rejected` or contradictory evidence reconciles. Nothing confirmed is ever rolled back.

### 22.2 The supported monetary shapes are frozen

`EnsureMonetaryShapeIsSupported` refuses, before any irreversible work, with
`AncillaryExchangeGroupMalformed` (20312, 422):

| Shape | Verdict |
| --- | --- |
| `Even`, `AddCollect`, `RefundDue`, `Residual` | supported |
| `AddCollect + RefundDue`, `AddCollect + Residual` | supported |
| `RefundDue + Residual` | refused |
| `AddCollect + RefundDue + Residual` | refused |
| non-positive collection, refund or residual amount | refused |
| a leg currency that is not the group currency | refused |
| a blank refund or residual disposition | refused |

Duplicate legs are structurally impossible: `AncillaryEmdExchangeTerms` holds at most one `AcceptedAddCollect`,
one `AcceptedRefundDue` and one `AcceptedResidual`, so a second leg of a kind cannot be expressed. Nothing is
collapsed into a signed delta — each leg keeps its own accepted value object, its own operation key and its own
durable outcome. `RefundDue` keeps its original-refundable-source semantics and a residual is never
reinterpreted as a refund.

### 22.3 A document-coupled residual is EMD only

The first delivery accepted `DocumentCoupled` with any instrument and then fed the EMD-S materializer, which
would have minted an EMD for an accepted MCO. Frozen for this slice:

```text
ResidualFulfillment.DocumentCoupled  =>  ExpectedInstrument == ResidualInstrumentKind.Emd
```

Anything else — `Mco`, `Voucher`, `TravelCredit`, `Other`, `Unknown` — is refused at acceptance with 20312,
before the ticket document exchange. No MCO was built, no MCO is mapped to an EMD, and no unsupported
instrument is silently downgraded to `ExternalValue`.

### 22.4 Residual evidence fails closed on both rails

**Coupled.** `ResidualContradiction` now additionally requires the reason-for-issuance code and sub code to be
present and the returned instrument to equal the accepted `ExpectedInstrument`, on top of the existing
presence, amount and currency checks. A residual returned when none is owed, or missing/contradictory evidence
when one is required, reconciles with no EMD-exchange re-dispatch and no downstream residual or value dispatch.

**External.** The first delivery accepted a confirmed external residual with no checking at all. The new
`ExternalResidualContradiction` requires a provider reference, an instrument reference, an instrument, an exact
amount, an exact currency, and an instrument matching `ExpectedInstrument` whenever that is not `Unknown`.
Wrong evidence reconciles while already-confirmed EMD truth stays durable.

### 22.5 Provider successor coupon numbers are authoritative

`SuccessorEmdCouponIdentity.CouponNumber` was ignored because the generic `ElectronicMiscDocument.Issue`
renumbers coupons `1..N`. A narrow provider-confirmed factory was added:

```csharp
ElectronicMiscDocument.IssueProviderConfirmed(..., IReadOnlyList<int> providerCouponNumbers, ...)
```

Both factories delegate to one private `Create`, so ordinary issuance semantics are untouched: `Issue` still
numbers `1..N`. `IssueProviderConfirmed` persists the exact provider numbers and refuses a non-positive or
repeated number with `ElectronicMiscDocumentCouponNumbersMalformed` (20315, 422). The evidence policy rejects
the same shapes earlier, so a malformed echo reconciles rather than throwing.

Normalized result order is defined as **corresponding to the request successor-coupon order**, which is the
accepted successor-coupon order. `ExchangeRecord.SuccessorCouponNumber` and the successor coupon's
`Predecessor*` now both carry the actual provider number. `DeterministicEmdExchangeAdapter` can simulate
non-default numbers via `SuccessorCouponNumbersOverride`.

### 22.6 Source ↔ successor mapping is explicit

The positional fallbacks — "first source coupon", "minimum successor coupon" — are removed. The mapping is now
a single explicit index chain, and the cardinality is a frozen representation limit:

```text
SourceCouponNumbers.Count == SuccessorCoupons.Count

sorted source coupon order  <->  accepted successor-coupon order  <->  normalized provider result order
```

Any other cardinality is refused at acceptance with 20312, before the ticket exchange.

**This is not an industry restriction.** Merge (N:1) and split (1:M) EMD exchanges are industry-valid. They
require an explicit authoritative mapping in the accepted terms, which this contract does not carry, and
Ordering must not infer one. Recorded as a representation limit in `ICC-P3-EMD-EXCHANGE`.

### 22.7 An existing successor number is exact replay identity

The first delivery compared `existing.OperationId != group.SourceElectronicMiscDocumentId` — a servicing
operation id against an EMD id, two different identity domains, so the check was meaningless.

`ElectronicMiscDocumentIdentityPolicy.Conflict` now takes the operation id and the beneficiary explicitly and
compares: origin servicing `OperationId`, beneficiary binding, type, currency, issuer carrier, issuing office,
authority, RFIC, coupon count, and per returned coupon number the purpose, RFISC, value, currency, the
predecessor EMD id/document/coupon mapping, the ticket association (exact accepted successor coupon for
`Associated`, absent for `Standalone`), the `OrderServiceId` when the source approved one, and the
`ExternalValueReference` when the source approved one.

Any mismatch reconciles: it is not accepted as a replay, no other document number is generated, and the
already-confirmed EMD exchange is not re-dispatched.

### 22.8 Association and beneficiary evidence is complete

For an `Associated` successor the returned ticket **document** is now required and must be exact — previously
it was only checked when present. Every returned coupon association must be present and exact. For a
`Standalone` successor both the document-level and coupon-level ticket associations must be absent.

`SuccessorEmdIdentity` gained `BeneficiaryTravellerId`, so a confirmed wrong beneficiary is now detectable and
is treated as a contradiction. The adapter owns provider-to-domain normalization.

### 22.9 Deterministic request identity is complete

The intent fingerprint omitted `SourcePricingReference` and `Residual.ExpectedInstrument`, so a changed
immutable intent could reuse a known key. Both are now fingerprinted alongside source scope, beneficiary,
successor semantics and targets, ticket document, decision and source references, and every coupled-residual
field.

### 22.10 Every confirmed group owns exactly one PriceChangeSet

```text
one confirmed EMD exchange group
  => exactly one dependent PriceChangeSet
  => exactly one OrderPricingChanged
  => exactly one CommercialVersion advance
```

The service previously skipped the consequence when `PricingLines.Count == 0`, so an even exchange could
confirm with no commercial consequence at all. The skip is removed. Every executable `ExchangeToNewEmd` group
must now carry source-approved pricing lines — including an even exchange, which a line-grain ledger expresses
as an authoritative zero-net withdrawal/grant pair. A group with no pricing evidence is refused at acceptance
with `AncillaryExchangeTermsMissing` (20311, 422), before the ticket exchange.

Ordering invents no zero-value lines. Replay uses the persisted `PriceChangeSetId` and appends nothing.

### 22.11 Correction exception codes

| Code | Factory | HTTP | Meaning |
| --- | --- | --- | --- |
| 20315 | `ElectronicMiscDocumentCouponNumbersMalformed` | 422 | a provider-returned coupon number is non-positive or repeated |

Reused for the new rules: 20311 `AncillaryExchangeTermsMissing` (missing pricing evidence), 20312
`AncillaryExchangeGroupMalformed` (monetary shape, coupled-residual instrument, mapping cardinality). Highest
allocated code is now **20315**; 20001–20315 contiguous, no duplicates.

### 22.12 Correction migration

One additive migration, `P3G3AncillaryExchangeRefundDue`:

| Operation kind in `Up` | Count |
| --- | --- |
| `AddColumn` | 3 — `RefundDueOutcome`, `RefundDueReference`, `RefundDueDetail` on `Order.AcceptedExchangePlanAncillaryExchangeGroups` |
| destructive operations | **0** |

No historical migration edited, no enum renumbered, no index changed.

### 22.13 Correction test coverage

`EmdExchangeFreezeGateCorrectionTests` — 41 cases mapped to the brief's §11:

| Brief case | Test |
| --- | --- |
| 1 RefundDue confirmed → one value act → complete | `C1` |
| 2 RefundDue `Pending`/`Unknown` → AwaitingExternal, EMD truth retained | `C2` (2 shapes) |
| 3 RefundDue rejected / wrong amount, currency, disposition, missing reference | `C3` (5 shapes) |
| 4 AddCollect + RefundDue sequence | `C4` |
| 5 valid `AddCollect+Residual`; reject `RefundDue+Residual` and all three | `C5a`, `C5b` (6 shapes) |
| 6 DocumentCoupled MCO/unsupported instrument fails first, no fake EMD | `C6` (5 instruments) |
| 7 coupled residual wrong instrument/amount/currency/missing RFIC | `C7` (4 shapes) |
| 8 external residual missing/wrong reference, instrument, amount, currency | `C8` (5 shapes) |
| 9 non-default provider coupon numbers persisted exactly, both directions | `C9` |
| 10 duplicate / non-positive returned coupon numbers reconcile | `C10` (2 shapes) |
| 11 source/successor cardinality mismatch fails before ticket exchange | `C11` |
| 12 existing successor number from another operation is never a replay | `C12` |
| 13 associated without ticket document; standalone claiming one; wrong beneficiary | `C13` (3 shapes) |
| 14 same key + changed source pricing ref / residual instrument | `C14` (2 shapes) |
| 15 even exchange without pricing evidence fails first; one consequence per group | `C15`, `C15b` |
| 16 exact replay of a full monetary group moves nothing twice | `C16` |

`ElectronicMiscDocumentIdentityPolicyTests` (Domain, 16 cases) covers each identity facet of §22.7 directly,
because the deeper facets are not reachable through the servicing flow: the only path that reaches the policy
is a fresh dispatch against a pre-existing document, which by construction came from a different operation, so
the origin-operation check always fires first. `C12` proves the reachable flow case; the unit tests prove the
rest. One facet — a standalone coupon carrying a ticket association — is unconstructible, because
`EnsureCouponIsWellFormed` already refuses it at the aggregate; the policy branch stays as defence and the test
asserts the reachable standalone replay instead.

### 22.14 Correction files changed

**New (2)**

| File | Purpose |
| --- | --- |
| `tests/.../P3/EmdExchangeFreezeGateCorrectionTests.cs` | the 41-case correction suite |
| `tests/AeroTech.Ordering.Domain.Tests/P3/ElectronicMiscDocumentIdentityPolicyTests.cs` | the 16-case identity-facet unit suite |
| `src/AeroTech.Ordering.Persistence/Migrations/*_P3G3AncillaryExchangeRefundDue.cs` | three additive nullable columns |

**Modified (12)**

| File | Change |
| --- | --- |
| `Domain/ElectronicMiscDocumentAggregate/ElectronicMiscDocument.cs` | `IssueProviderConfirmed` + shared `Create`; coupon-number validation |
| `Domain/Ports/EmdExchange/SuccessorEmdIdentity.cs` | `BeneficiaryTravellerId` |
| `Domain/Servicing/Plans/AcceptedExchangeAncillaryExchangeGroup.cs` | refund-due rail, leg identity, `IsSettled`/`IsRejected` |
| `Domain/Servicing/Plans/Policies/AncillaryExchangeEvidencePolicy.cs` | coupon-number, beneficiary and association checks; coupled-residual instrument and RFIC; `ExternalResidualContradiction`; `RefundDueContradiction` |
| `Domain/Servicing/Plans/Policies/ElectronicMiscDocumentIdentityPolicy.cs` | rewritten: correct identity domains and the full facet set |
| `Domain/Servicing/Plans/Contracts/IAcceptedExchangePlanStore.cs` | `RecordAncillaryExchangeRefundDueOutcomeAsync` |
| `Domain/_Shared/Resources/ExceptionFactory.cs`, `ExceptionMessages.cs` | code 20315 |
| `Application/.../Exchange/ExchangeAncillaryPlanner.cs` | monetary-shape freeze, EMD-only coupled residual, 1:1 cardinality, mandatory pricing evidence |
| `Application/.../Exchange/ExchangeOperationKeys.cs` | `AncillaryExchangeRefundDue` |
| `Application/.../Exchange/ExchangeService.AncillaryExchange.cs` | refund-due stage in the order; beneficiary and operation id into the checks |
| `Application/.../Exchange/ExchangeService.AncillaryExchangeMaterialization.cs` | exact provider coupon numbers; positional fallbacks removed; unconditional consequence |
| `Application/.../Exchange/ExchangeService.AncillaryExchangeMonetary.cs` | `SettleAncillaryExchangeRefundDueAsync`; external residual contradiction |
| `Persistence/Servicing/AcceptedExchangePlanAncillaryExchangeGroupRow.cs` + configuration + `AcceptedExchangePlanStore.cs` | the three refund-due columns and their round trip |
| `Providers.Deterministic/DeterministicEmdExchangeAdapter.cs` | complete intent fingerprint; beneficiary echo; coupon-number, ticket-document and residual simulation knobs |
| `Providers.Deterministic/DeterministicAncillaryDispositionAdapter.cs` | refund-due currency/disposition, residual instrument and merge-group knobs |

### 22.15 Correction regression results

Every figure is from an actual run on the final build of this working tree.

| Suite | Result |
| --- | --- |
| **`EmdExchangeFreezeGateCorrectionTests` (new)** | **43 passed, 0 failed, 0 skipped** |
| **`ElectronicMiscDocumentIdentityPolicyTests` (new, Domain)** | **16 passed, 0 failed, 0 skipped** |
| `EmdExchangeToNewEmdFlowTests` | 48 passed, 0 failed, 0 skipped |
| `Contracts/EmdExchange/` | 18 passed, 0 failed, 0 skipped |
| `AncillaryRefundFlowTests` (G2) | 35 passed, 0 failed, 0 skipped |
| `EmdReassociationFlowTests` (G1) | 29 passed, 0 failed, 0 skipped |
| `AncillaryDispositionGateTests` | 22 passed, 0 failed, 0 skipped |
| `PostDocumentTruthFreezeGateTests` | 11 passed, 0 failed, 0 skipped |
| `ResidualDocumentCouplingTests` (P3-F) | 15 passed, 0 failed, 0 skipped |
| `ResidualEvidenceFreezeGateTests` (P3-F) | 12 passed, 0 failed, 0 skipped |
| `MixedExchangeFlowTests` (P3-F) | 53 passed, 0 failed, 0 skipped |
| `ExchangeFlowTests` (P3-F) | 32 passed, 0 failed, 0 skipped |
| `RefundDueExchangeFlowTests` (P3-D/F) | 25 passed, 0 failed, 0 skipped |
| `AddCollectFundingRecoveryTests` | 22 passed, 0 failed, 0 skipped |
| `Contracts/ExchangeFunding/` | 24 passed, 0 failed, 0 skipped |
| `Contracts/ExchangeResidual/` | 12 passed, 0 failed, 0 skipped |
| `Contracts/RefundValue/` | 11 passed, 0 failed, 0 skipped |
| `Contracts/DocumentExchange/` | 7 passed, 0 failed, 0 skipped |
| `Contracts/DocumentRefund/` | 17 passed, 0 failed, 0 skipped |
| `Contracts/EmdAssociation/` | 15 passed, 0 failed, 0 skipped |
| `Contracts/AncillaryDisposition/` | 20 passed, 0 failed, 0 skipped |
| **`AeroTech.Ordering.Domain.Tests` (full)** | **527 passed, 0 failed, 0 skipped** |
| **`AeroTech.Ordering.Persistence.Tests` (full)** | **1156 passed, 0 failed, 0 skipped** |
| `dotnet build AeroTech.Ordering.sln` | Build succeeded, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." |

### 22.16 Self-review beyond the tests

Source was re-read after the suites went green, not instead of it. Verified by inspection:

* no positional fallback remains anywhere in the G3 stages — `grep` for `SourceCouponNumbers[0]`,
  `Min(coupon => coupon.CouponNumber)`, `ElementAt(index)` and `SuccessorCouponNumberFor` returns nothing;
* the only surviving `PricingLines.Count == 0` checks are the two **acceptance refusals** in the planner, which
  is what §10 requires; the service-side skip is gone;
* the invalid `OperationId != SourceElectronicMiscDocumentId` comparison no longer exists in the repository;
* the stage order in `ExchangeAncillaryToNewEmdAsync` reads guarantee → exchange → capture → refund-due →
  external residual, matching §1 exactly for both `RefundDue`-only and `AddCollect + RefundDue`;
* the refund request's `DispositionReference` carries the **decision** reference and `SourcePricingReference`
  the source pricing reference — a precision fix made during self-review, not surfaced by any test;
* exception codes 20001–20315 are contiguous with no duplicates and all inside 20000–29999.

---

## 23. Freeze Verdict

```text
P3-G3 READY TO FREEZE: YES
```

All ten production blockers are fixed and covered. No known Ordering-owned defect and no placeholder remains.
A source-approved refund obligation is executed, not dropped. The supported monetary shapes are frozen and
every unsupported one fails before irreversible work. A document-coupled residual can only be an EMD, and both
residual rails fail closed on contradictory evidence. Provider coupon numbers are persisted exactly and drive
both lineage directions. The source-to-successor mapping is explicit, with its 1:1 limit recorded as a
representation limit rather than an industry rule. An existing successor number is compared on real identity
domains and is never accepted as another operation's replay. Deterministic request identity covers every
materially binding field. Every confirmed group owns exactly one `PriceChangeSet`, one `OrderPricingChanged`
and one `CommercialVersion` advance, and replay appends none of them twice. Every frozen ticket, EMD and
commercial truth survives downstream money failure.
