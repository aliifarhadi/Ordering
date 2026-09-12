# P3-G4 — Retain As Residual / Reusable Existing Ancillary Value

Closing report for P3-G4, the fourth slice of P3-G ancillary servicing: making
`AncillaryExchangeDisposition.RetainAsResidual` fully executable.

Companion documents, all in this folder:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, now carrying `ICC-P3-ANCILLARY-RETENTION`.
* [P3-G3-EMD-EXCHANGE-TO-NEW-EMD-REPORT.md](P3-G3-EMD-EXCHANGE-TO-NEW-EMD-REPORT.md) — the frozen G3 baseline this slice must not be confused with.
* [P3-G2-EMD-A-REFUND-REPORT.md](P3-G2-EMD-A-REFUND-REPORT.md) — the frozen G2 refund lifecycle.
* [P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md](P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md) — the frozen G1 association lifecycle.

---

## 1. Starting State

```text
Actual starting HEAD    3df6e8f02caae4db3edf31000fac8668024fcd0d  P3-G3 Freeze
Brief's frozen baseline 3df6e8f02caae4db3edf31000fac8668024fcd0d
Delta                   none — HEAD matched the brief exactly
Working tree at start   clean
```

After this slice the executable ancillary dispositions are `ReassociateExisting`, `Refund`,
`ExchangeToNewEmd` and `RetainAsResidual`. `Cancel` and `ManualReview` stay refused with
`AncillaryDispositionNotExecutable` (20298) before any irreversible ticket work.

---

## 2. The Governing Distinction

`RetainAsResidual` means one thing and nothing else:

> the authoritative servicing source says the **existing** ancillary accountable value stays reusable for a
> later reshop, refund or use, and **no** new accountable document or value instrument is created now.

That is deliberately not G3. If the source wants `old EMD -> new residual EMD-S`, that is an
accountable-document exchange and must use the frozen `ExchangeToNewEmd` + `IEmdExchangePort` path.

G4 issues no EMD-S, refunds no money, creates no voucher, wallet, travel bank or credit shell, calls neither
`IExchangeResidualValuePort` nor `IRefundValuePort`, does not turn the existing EMD into another document, and
never invents a residual balance. **The existing source EMD coupon remains the accountable value carrier.**

| | `ExchangeToNewEmd` (G3) | `RetainAsResidual` (G4) |
| --- | --- | --- |
| new EMD | yes, provider-numbered | none |
| source coupon status | `Exchanged` | stays `OpenForUse` |
| association | successor EMD bound at issuance | stays detached |
| provider act | `IEmdExchangePort` | none |
| money now | possible collection / refund / residual | none |
| `PriceChangeSet` | exactly one per group | none |
| durable evidence | exchange group row + document lineage | retention fields on the disposition |

Their persistence and state machines are kept separate; nothing is shared between them.

---

## 3. Document Truth Entering And Leaving G4

The frozen sequence is untouched:

```text
ticket exchange confirmed
 -> predecessor ETKT Exchanged
 -> successor ETKT + lineage durable
 -> every executable affected EMD-A coupon DisassociatedByReissue
 -> ticket monetary settlement
 -> ancillary servicing
```

A retained coupon ends in exactly this state, which `G4H1` asserts field by field:

```text
status                       OpenForUse
association                  null
DisassociatedByReissue       exactly once
Reassociated history         none
RefundRecord                 null
ExchangeRecord               null
document StatusSummary       Issued
new EMD                      none
```

The EMD-A is not converted to EMD-S, no coupon purpose becomes `ResidualValue`, and `DocumentVersion` is not
moved to record a commercial disposition — the only version move on that document is G1's disassociation.

---

## 4. Retention Source Contract

The enum alone is never sufficient. `AncillaryCouponDisposition` gained `Retention`:

```csharp
public sealed record AncillaryRetentionTerms(
    string RetentionReference,
    string SourceReference,
    AncillaryRetentionMode RetentionMode);
```

`AncillaryRetentionMode` lives in `Contracts/AeroTech.Messages/Ordering/Enums/` with every shape a source can
express, so an unexecutable request can be *stated* and therefore *refused* rather than being inexpressible:

```text
ExistingEmdCouponReusable = 1   <- the only executable mode in G4
NewMiscellaneousDocument  = 2   <- refused; belongs to ExchangeToNewEmd
Voucher                   = 3   <- refused
StoredValue               = 4   <- refused
ExternalInstrument        = 5   <- refused
CreditShell               = 6   <- refused
```

`ExchangeAncillaryPlanner.EnsureRetentionIsExecutable` refuses, **before the reservation change and before the
ticket document exchange**:

| Refusal | Code |
| --- | --- |
| no retention terms | `AncillaryRetentionTermsMissing` (20316, 422) |
| blank retention reference | 20316 |
| blank source reference | 20316 |
| undefined retention mode | 20316 |
| retention carrying an immediate monetary or exchange consequence | 20316 |
| any mode other than `ExistingEmdCouponReusable` | `AncillaryRetentionModeNotExecutable` (20317, 422) |

The existing decision reference, version and context fingerprint remain binding — retention is an addition to
the accepted decision, not a replacement for it.

**No reusable amount exists anywhere in G4.** There is no amount field on the terms, on the accepted
disposition, or in persistence. Nothing is derived from the EMD issuance value, a pricing allocation, used
value or fees. The exact future value is re-evaluated by an authoritative source when the retained coupon is
next used, which is out of scope here.

---

## 5. Commercial Service Consequence

A retained residual means the old dependent ancillary service is no longer deliverable on the replaced
itinerary. Neither existing mechanism was usable, and using one would have written a falsehood:

| Existing method | Why it is wrong for G4 |
| --- | --- |
| `MarkCancelled()` | sets `FinancialStatus = Refunded` — nothing was refunded; the value is retained |
| `MarkSupersededByExchange()` | sets `DocumentStatus`/`CommercialStatus` to `Exchanged` — no document was exchanged |

So one narrow method was added, `OrderService.MarkSupersededByRetainedResidual()`:

```text
Status            -> Cancelled
CommercialStatus  -> Cancelled
DeliveryStatus    -> Unused
DocumentStatus    -> untouched   (the EMD is still an issued, open accountable document)
FinancialStatus   -> untouched   (nothing was refunded)
returns           -> whether anything actually changed
```

`Order.RetainAncillaryResidual(orderServiceIds, clock)` applies it, recomputes the commercial summary, and
advances `CommercialVersion` **exactly once — and only when a service actually transitioned**. A retained
coupon with no `OrderServiceId` invents no service and moves no version, which `G4H3` proves arithmetically:
`after.CommercialVersion == exchangeSet.ExpectedCommercialVersion + 1`, i.e. only the ticket exchange itself
advanced it. `G4H2` proves the delivering-service case is `+ 2`.

The ticket Exchange servicing operation stays the only envelope: one Exchange `OrderChange`, no child
servicing operation, no second `OrderChange`. Prior sold and history facts are preserved — nothing is deleted
or rewritten.

---

## 6. No Pricing, No Value Movement

G4 is neither an immediate money movement nor a fixed residual amount, so:

```text
0 dependent PriceChangeSet
0 OrderPricingChanged
0 invented zero-value pricing lines
0 IRefundValuePort calls
0 IExchangeResidualValuePort calls
0 IEmdExchangePort calls
0 IDocumentRefundPort calls
0 IEmdAssociationPort calls
```

`G4R2` asserts every one of those against a retention-only exchange: the operation's change owns exactly one
price consequence — the ticket exchange's own — and no pricing event carries the retention source reference.

If the source returns retention **together with** an immediate monetary consequence, that is the wrong shape
and it fails closed at acceptance (20316) rather than being silently executed as something else.

---

## 7. Durable Retention Evidence

Retention must be distinguishable from an ancillary that was merely left detached by accident, and it must
survive restart. Four additive fields on the existing accepted ancillary disposition — no generic
servicing-consequence table, no EMD history mutation, no in-memory flag:

```text
RetentionReference
RetentionSourceReference
RetentionMode
RetentionSettledAt        <- the durable proof that retention was actually settled
```

`IsRetentionSettled => RetentionSettledAt is not null` is what `IsSettled` and `State` read for a retention
disposition, so the plan itself carries the answer across a restart. `RecordAncillaryRetentionSettledAsync`
writes it with `??=`, so a replay never moves the timestamp — asserted in `G4R1`.

---

## 8. Settlement Semantics

When retention becomes the next unsettled disposition, `ExchangeService.AncillaryRetention.cs`:

1. loads the exact EMD by id;
2. gates on frozen post-reissue truth via `ElectronicMiscDocument.PermitsResidualRetention`;
3. applies the local commercial service consequence if the coupon names one;
4. persists the retention timestamp;
5. saves once;
6. continues to the next ancillary, or completes.

There is **no external mutation in G4**, so no `Pending`, `Unknown`, `Recover` or `WasDispatched` rail was
invented for retention. That is why retention has no operation key: there is no provider operation to key.

### Fail-closed state rules

```csharp
public bool PermitsResidualRetention(int emdCouponNumber, long operationId)
    => !IsTerminal
       && _coupons.SingleOrDefault(coupon => coupon.CouponNumber == emdCouponNumber)
           is { IsOpenForUse: true } candidate
       && candidate.CarriesNoAssociation
       && candidate.ExchangeRecord is null
       && candidate.RefundRecord is null
       && candidate.IsDisassociatedByReissue(operationId);
```

One predicate covers every mandated conflict: refunded, exchanged, voided or otherwise non-open fails on
`IsOpenForUse`; associated elsewhere fails on `CarriesNoAssociation`; not detached by *this* exchange fails on
`IsDisassociatedByReissue(operationId)`. A failure reconciles — ticket truth stays authoritative, no document
is mutated, no value moves, and the coupon is never pulled back from another association.

---

## 9. Mixed-Disposition Sequencing

`AcceptedExchangePlan.NextUnsettledAncillary` remains the single deterministic selector; retention joins
`ExecutableAncillaries` and answers `IsSettled` from its own evidence. `G4H4` runs all four outcomes in one
reissue — reassociation, refund, EMD exchange and retention — and asserts each lands independently with
exactly one provider act on each of the three external rails and none for retention.

Settlement order is the frozen `AffectedAncillaries` order: document number ordinal, then coupon number.
Previously settled dispositions are never repeated, and an unresolved G1/G2/G3 stage still stops later
irreversible work. Retention itself dispatches nothing, so it can never leave an external operation dangling.

---

## 10. Mandatory Edge Matrix → Test

| # | Brief case | Test |
| --- | --- | --- |
| 1 | single EMD-A retained; open, detached, one disassociation, no provider act, durable evidence | `G4H1` |
| 2 | coupon with `OrderServiceId` → old ancillary service non-deliverable | `G4H2` |
| 3 | coupon with no `OrderServiceId` → no synthetic service, no version move | `G4H3` |
| 4 | mixed G1 + G2 + G3 + G4 settle independently | `G4H4` |
| 5 | two retained coupons each get evidence; unaffected coupon unchanged | `G4H5`, `G4H6` |
| 6 | no retention terms | `G4S1` no-terms (20316) |
| 7 | blank retention reference | `G4S1` blank-retention-reference (20316) |
| 8 | blank source reference | `G4S1` blank-source-reference (20316) |
| 9 | unsupported retention mode | `G4S1` ×5 modes (20317) |
| 10 | new EMD-S requested through G4 | `G4S1` new-miscellaneous-document (20317) |
| 11 | wallet / voucher / external-value creation requested | `G4S1` voucher, stored-value, external-instrument, credit-shell (20317) |
| 12 | an affected ancillary with no disposition | `G4S2` (20296) |
| 13 | coupon refunded before G4 resumes | `G4C1` Refunded |
| 14 | coupon exchanged | `G4C1` Exchanged |
| 15 | coupon moved elsewhere | `G4C2` |
| 16 | coupon voided / non-open | `G4C1` Void |
| 17 | completed replay: no second transition, version, evidence, disassociation, provider call or redispatch | `G4R1` |
| 18 | crash around local persistence settles exactly once | `G4R3` |
| 19 | zero PriceChangeSet, event, refund-value, residual, EMD exchange, EMD refund | `G4R2` |
| 20 | no reusable amount is ever derived | structural — no amount field exists anywhere (§4) |
| 21–25 | G1, G2, G3, G3 residual rails and P3-F unchanged | full-suite figures in §13 |

**Case 15 behaves differently from the brief's expectation, and the code is right.** A coupon moved onto a
coupon this exchange did not detach it from hits the **frozen G1 association guard** during re-materialization
and fails closed with `ElectronicMiscDocumentAssociationMoved` (20302, 409) *before* retention is ever
considered. `G4C2` asserts that actual behaviour — refusal, ticket still `Exchanged`, retention unsettled, no
document mutation, one successor ticket. Forcing this into `NeedsReconciliation` would have meant weakening a
frozen G1 invariant, which the brief forbids. Note also that a coupon re-associated back to *this* exchange's
own predecessor coupon is legitimately repaired by G1 re-disassociation and then retained normally — that is
correct, not a leak.

---

## 11. Discrimination Proof

Green tests are not evidence that a guard works. All three G4 guards were temporarily reverted — the retention
state predicate reduced to "the coupon exists", `EnsureRetentionIsExecutable` emptied, and the
`if (!transitioned) return false` idempotence removed — and the suite re-run:

```text
with the guards reverted:  23 total, 10 passed, 13 failed
```

The 13 failures were exactly all nine `G4S1` source-validation shapes, all three `G4C1` post-ticket conflicts,
and `G4H3` (the version would have moved with no service transition). The real implementations were then
restored and the suite re-run green. No G4 test is decorative.

---

## 12. Persistence And Migration

One additive migration, `P3G4AncillaryRetention`:

| Operation kind in `Up` | Count |
| --- | --- |
| `AddColumn` | 4 — `RetentionReference`, `RetentionSourceReference`, `RetentionMode`, `RetentionSettledAt` on `Order.AcceptedExchangePlanAncillaries` |
| destructive operations | **0** |

No historical migration was edited, no enum value renumbered, no index changed, and **no EMD schema change was
needed** — retention is a commercial disposition, not document state.

```text
dotnet ef migrations has-pending-model-changes
  -> No changes have been made to the model since the last migration.
```

---

## 13. Regression Results

Every figure is from an actual run on the final build of this working tree.

| Suite | Result |
| --- | --- |
| **`RetainAsResidualFlowTests` (new)** | **23 passed, 0 failed, 0 skipped** |
| `AncillaryDispositionGateTests` | 21 passed, 0 failed, 0 skipped |
| `EmdReassociationFlowTests` (G1) | 29 passed, 0 failed, 0 skipped |
| `AncillaryRefundFlowTests` (G2) | 35 passed, 0 failed, 0 skipped |
| `EmdExchangeToNewEmdFlowTests` (G3) | 48 passed, 0 failed, 0 skipped |
| `EmdExchangeFreezeGateCorrectionTests` (G3) | 43 passed, 0 failed, 0 skipped |
| `EmdExchangeFreezeGuardTests` (G3) | 16 passed, 0 failed, 0 skipped |
| `PostDocumentTruthFreezeGateTests` | 11 passed, 0 failed, 0 skipped |
| `ResidualDocumentCouplingTests` (P3-F) | 15 passed, 0 failed, 0 skipped |
| `ResidualEvidenceFreezeGateTests` (P3-F) | 12 passed, 0 failed, 0 skipped |
| `MixedExchangeFlowTests` (P3-F) | 53 passed, 0 failed, 0 skipped |
| `Contracts/AncillaryDisposition/` | 20 passed, 0 failed, 0 skipped |
| `Contracts/EmdExchange/` | 18 passed, 0 failed, 0 skipped |
| `Contracts/DocumentRefund/` | 17 passed, 0 failed, 0 skipped |
| `Contracts/RefundValue/` | 11 passed, 0 failed, 0 skipped |
| `Contracts/ExchangeResidual/` | 12 passed, 0 failed, 0 skipped |
| **`AeroTech.Ordering.Domain.Tests` (full)** | **544 passed, 0 failed, 0 skipped** |
| **`AeroTech.Ordering.Persistence.Tests` (full)** | **1194 passed, 0 failed, 0 skipped** |
| `dotnet build AeroTech.Ordering.sln` | Build succeeded, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." |

---

## 14. Requirement → Code → Test Traceability

| Brief | Requirement | Code | Test |
| --- | --- | --- | --- |
| §1 | `RetainAsResidual` becomes executable | `ExchangeAncillaryPlanner.EnsureExecutable`, `ExchangeService.AncillaryRetention.cs` | `G4H1`, `AncillaryDispositionGateTests.P` |
| §2 | retention is not an accountable-document exchange | no port, no document mutation on this path | `G4R2` |
| §3 | frozen document truth is preserved exactly | `PermitsResidualRetention`, G1 disassociation reused | `G4H1`, `G4R1` |
| §4 | authoritative terms required | `AncillaryRetentionTerms`, `EnsureRetentionIsExecutable` | `G4S1` (9 shapes) |
| §4 | only `ExistingEmdCouponReusable` executes | `AncillaryRetentionModeNotExecutable` | `G4S1` (5 modes) |
| §4 | no reusable amount is required or derived | no amount field exists | structural |
| §5 | the old ancillary service becomes non-deliverable | `OrderService.MarkSupersededByRetainedResidual`, `Order.RetainAncillaryResidual` | `G4H2` |
| §5 | one envelope, one `OrderChange`, no child operation | the ticket Exchange operation is reused unchanged | `G4H2`, `G4R2` |
| §5 | `CommercialVersion` advances exactly once, only on a real transition | `if (!transitioned) return false` | `G4H2`, `G4H3`, `G4R1` |
| §6 | no pricing consequence, no value movement | no `CommitDependentPriceChange` on this path | `G4R2` |
| §7 | durable retention evidence | four fields + `RecordAncillaryRetentionSettledAsync` | `G4H1`, `G4H5`, `G4R3` |
| §8 | settlement order and no invented provider rail | `RetainAncillaryResidualAsync` | `G4H1`, `G4R3` |
| §9 | fail-closed state rules | `PermitsResidualRetention` | `G4C1` (3), `G4C2` |
| §10 | deterministic mixed dispositions | `NextUnsettledAncillary` | `G4H4` |
| §11 | G3 and G4 state machines stay separate | separate fields, separate stage file, no shared persistence | full-suite figures |
| §13 | additive persistence only | `P3G4AncillaryRetention` | `has-pending-model-changes` |

---

## 15. Exception Codes

| Code | Factory | HTTP | Meaning |
| --- | --- | --- | --- |
| 20316 | `AncillaryRetentionTermsMissing` | 422 | the source-approved retention terms are incomplete or carry a monetary consequence |
| 20317 | `AncillaryRetentionModeNotExecutable` | 422 | the requested retention mode is not executable by this capability |

Highest allocated code is now **20317**; 20001–20317 contiguous, no duplicates, all inside 20000–29999.
Reused unchanged: 20296 `AncillaryDispositionMissing`, 20298 `AncillaryDispositionNotExecutable`, 20302
`ElectronicMiscDocumentAssociationMoved`.

---

## 16. Files Changed

**New (5)**

| File | Purpose |
| --- | --- |
| `Contracts/AeroTech.Messages/Ordering/Enums/AncillaryRetentionMode.cs` | every retention shape a source can state |
| `Domain/Ports/AncillaryDisposition/AncillaryRetentionTerms.cs` | the source-approved retention terms |
| `Domain/OrderAggregate/Order.RetainedResidual.cs` | the commercial service consequence |
| `Application/.../Exchange/ExchangeService.AncillaryRetention.cs` | the retention settlement stage |
| `src/AeroTech.Ordering.Persistence/Migrations/*_P3G4AncillaryRetention.cs` | four additive nullable columns |
| `tests/.../P3/RetainAsResidualFlowTests.cs` | the 23-case G4 suite |

**Modified (10)**

| File | Change |
| --- | --- |
| `Domain/Ports/AncillaryDisposition/AncillaryCouponDisposition.cs` | `Retention` terms |
| `Domain/Servicing/Plans/AcceptedExchangeAncillaryDisposition.cs` | four retention fields; `IsSettled`/`IsRejected`/`State` branch on retention |
| `Domain/Servicing/Plans/AcceptedExchangePlan.cs` | retention joins `ExecutableAncillaries`; `AncillaryRetentions`, `WithAncillaryRetention` |
| `Domain/ElectronicMiscDocumentAggregate/ElectronicMiscDocument.cs` | `PermitsResidualRetention` |
| `Domain/OrderAggregate/Entities/OrderService.cs` | `MarkSupersededByRetainedResidual` |
| `Domain/Servicing/Plans/Contracts/IAcceptedExchangePlanStore.cs` | `RecordAncillaryRetentionSettledAsync` |
| `Domain/_Shared/Resources/ExceptionFactory.cs`, `ExceptionMessages.cs` | codes 20316, 20317 |
| `Application/.../Exchange/ExchangeAncillaryPlanner.cs` | retention acceptance and validation |
| `Application/.../Exchange/ExchangeService.cs` | dispatcher routes retention |
| `Persistence/Servicing/AcceptedExchangePlanAncillaryRow.cs` + configuration + `AcceptedExchangePlanStore.cs` | the four columns and their round trip |
| `Providers.Deterministic/DeterministicAncillaryDispositionAdapter.cs` | retention terms and the refusal shapes |
| `tests/.../P3/ExchangeScenarios.cs` | `AssociateAncillaryCouponAsync` |
| `tests/.../P3/AncillaryDispositionGateTests.cs` | `RetainAsResidual` removed from the not-executable theory, as §1 mandates |

No unrelated file was refactored.

---

## 17. Explicitly Out Of Scope

Not implemented, per §15, and absent rather than approximated: the `Cancel` disposition, `ManualReview`
execution, new EMD-S residual issuance (that is G3), external voucher creation, wallet / travel bank / credit
shell, residual redemption, future reshop of retained value, any generic stored-value subsystem or residual
engine, a standalone public EMD residual API, EMD-S fee or penalty documentation, involuntary servicing, and
DCS/disruption integration.

---

## 18. Freeze Verdict

```text
P3-G4 READY TO FREEZE: YES
```

Retention is explicit and durable: it settles only from source-approved terms and leaves a persisted
`RetentionSettledAt`, so no retained ancillary is merely left detached by accident. No document or value
instrument is fabricated — G4 makes zero provider calls of any kind. No residual amount is derived, because no
amount field exists anywhere in the slice. An old ancillary service the retained coupon delivered cannot
remain implicitly deliverable, and that transition advances `CommercialVersion` exactly once and never on
replay. G1, G2, G3, the G3 residual rails and P3-F all remain frozen.

Each guard is proven load-bearing: reverting the three of them makes 13 of the 23 new cases fail (§11).
