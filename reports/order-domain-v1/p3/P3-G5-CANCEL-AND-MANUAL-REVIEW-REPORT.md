# P3-G5 — Ancillary Cancel + Manual Review

Repository: `aliifarhadi/Ordering`
Branch: `k8s-stg`

```text
Frozen starting baseline   8d457c15b3ebb7c54e49e4fdedede76c5da84be0   P3-G4 Debug
Actual HEAD at start       8d457c15b3ebb7c54e49e4fdedede76c5da84be0
Delta to the brief         none — HEAD matched exactly
Working tree at start      clean
```

This slice makes the last two `AncillaryExchangeDisposition` outcomes executable. `ReassociateExisting` (G1),
`Refund` (G2), `ExchangeToNewEmd` (G3) and `RetainAsResidual` (G4) are unchanged.

---

## 1. Cancel is not Refund, not Retention, and not Manual Review

Four dispositions can all end with "the customer no longer has this ancillary". They are four different
economic facts and this slice keeps them four different code paths.

| Disposition | Accountable document | Money | Order service | Provider act |
| --- | --- | --- | --- | --- |
| `Refund` (G2) | coupon → `Refunded` | value returns to the customer | refund consequence + `PriceChangeSet` | document refund + return of value |
| `RetainAsResidual` (G4) | coupon stays `OpenForUse` | nothing moves, value stays reusable | `Cancelled` / `Cancelled` | none |
| `Cancel` (G5) | whole document → `Voided`, every coupon → `Void` | **nothing moves, value is forfeited** | `Cancelled` / `Cancelled` / `Voided` | document void |
| `ManualReview` (G5) | untouched, coupon stays `OpenForUse` and detached | nothing moves | untouched | none |

The one that is easiest to get wrong is `Cancel` versus `Refund`: both terminate the document, and only one
returns money. Ordering never converts one into the other — see §7.

## 2. Benchmark evidence

Used as behavioural evidence, not as a vendor domain model.

**Travelport EMD Void** is the sharpest statement of the distinction this slice implements:

```text
REFUND -> void EMD and refund value to original FOP
VOID   -> void EMD and forfeit residual value
```

`https://support.travelport.com/webhelp/jsonapis/airv11/content/air11/Ancillaries/APIRef_EMDVoid.htm`

**Travelport ancillary cancellation** separates cancelling the *service* from managing the *issued EMD*, and
says the issued EMD must be Void or Refund if permitted:
`https://support.travelport.com/webhelp/smartpointcloud/content/Learn/11Ancillary/Cancel.htm`

**Sabre Offers & Orders** documents cancellation of a fulfilled paid seat as an Order Item cancellation whose
EMD ends as either Void or Refund:
`https://developer.sabre.com/sites/default/files/2024-06/Sabre%20Offers%20and%20Orders%20APIs%20User%20Guide.pdf`

**IATA** treats the EMD as an accountable document / value-coupon record analogous to an ET, with explicit
lifecycle operations:
`https://www.iata.org/contentassets/c33c192da39a42fcac34cb5ac81fd2ea/airline-guide-emd2010.pdf`

None of these authorises a new coupon-level terminal state. All of them describe **Void** and **Refund** as
the document outcomes. So for an already-issued EMD the only executable no-refund consequence in G5 is a
provider-approved **whole-document void**, reusing the frozen P3-C `IDocumentVoidPort`. No `IEmdCancelPort`
was created.

## 3. Accepted source terms

`Cancel` is never executed from the enum alone. A new provider-neutral term object rides on the existing
disposition port result:

```csharp
AncillaryCancellationTerms(
    string CancellationReference,
    string SourceReference,
    AncillaryCancellationDocumentAction DocumentAction)
```

`AncillaryCancellationDocumentAction` lives in `Contracts/AeroTech.Messages/Ordering/Enums/` and carries one
member, `VoidWithoutRefund`. It is a distinct type from `AncillaryRetentionMode` and shares nothing with it.

Nothing about cancellation intent is inferred from EMD value, refundability, service state, delivery state,
pricing allocation, ticket outcome or `Detail`. Missing terms, a blank reference, a blank source reference or
an undefined action are `AncillaryCancellationTermsMissing` (20318, 422). A defined but unsupported future
action is `AncillaryCancellationDocumentActionNotExecutable` (20319, 422). Both fail before the reservation
change and before the ticket document exchange.

## 4. Cancel targets only a legitimate service ancillary

Before any irreversible ticket work, every `Cancel` coupon must be an affected EMD-A coupon of this reissue
that is `OpenForUse`, carries `EmdCouponPurpose.Service`, names an `OrderServiceId`, and resolves to a
non-`AirTransportation` service owned by the same order. Anything else is
`AncillaryCancellationTargetNotEligible` (20320, 409).

`Fee`, `Deposit` and `ResidualValue` coupons are therefore never destroyed by G5 — they carry no delivering
service and are refused on purpose, because the right consequence for them is a business decision this slice
does not own.

One further precondition is Ordering's own: voiding an accountable document is an attributable act, so a
caller with no `ActorId` is refused with `AncillaryCancellationRequiresAuthority` (20323, 422) before the
ticket, rather than discovering at void time that there is nobody to record as the voiding actor.

## 5. Whole-document void scope proof

`ElectronicMiscDocument.Void` is, and stays, a whole-document operation: it voids **every** coupon and
requires the document to be `Issued` with every coupon `OpenForUse`. No partial coupon void was invented.

So a cancel group is executable only when voiding the document cannot reach a coupon the source did not
approve for cancellation. The rule lives once, on the aggregate:

```csharp
public string? WholeDocumentCancellationConflict(IReadOnlyList<int> approvedCouponNumbers)
```

It returns the reason, or null, for: a document not `Issued`; a document with no coupon; any coupon already
terminal; any coupon not in the approved set. `PermitsCancellationVoid` is that same predicate plus "every
coupon was detached by *this* operation", so acceptance and execution can never disagree.

`ExchangeAncillaryPlanner.AcceptCancelGroups` runs the proof at acceptance and raises
`AncillaryCancellationScopeWiderThanApproved` (20321, 409) before the ticket exchange. These shapes are
therefore not executable in G5:

```text
Cancel + an open coupon this reissue never affected     -> 20321
Cancel + Refund on the same EMD                          -> 20321
Cancel + RetainAsResidual on the same EMD                -> 20321
Cancel + ReassociateExisting on the same EMD             -> 20321
Cancel + ManualReview on the same EMD                    -> 20321
Cancel on an EMD carrying an already-terminal coupon     -> 20321
```

Scope is never silently widened, and no other coupon is ever reinterpreted as `Cancel`.

## 6. The cancel group

A provider void act is document-scoped, so the durable unit is document-scoped:
`AcceptedExchangeAncillaryCancelGroup`, keyed by `OperationId + EMD identity`. The reference is **derived**,
not source-supplied — `emd-cancel:{documentNumber}` — so the source cannot split or merge cancel scope the way
it chooses `ExchangeGroupRef` for G3.

```text
CancelGroupRef                the derived operation-scoped key
ElectronicMiscDocumentId      document identity
EmdDocumentNumber
EmdCouponNumbers              member coupon identities
OrderServiceIds               the exact dependent services the group cancels
CancellationReference         source cancellation reference
SourceReference               source reference
DocumentAction                VoidWithoutRefund
DecisionReference
VoidEligibilityOutcome        eligibility evidence
VoidRefundRequiredInstead
VoidEligibilityDetail
VoidDispatchedAt              local dispatch claim, written before the provider call
VoidOutcome                   void execution evidence
VoidProviderReference
VoidDetail
CancellationSettledAt         local settlement checkpoint
```

It is persisted in one additive table, `Order.AcceptedExchangePlanAncillaryCancelGroups`, cascade-owned by the
accepted plan exactly like `AcceptedExchangePlanAncillaryExchangeGroups`. **One EMD produces at most one
provider void operation**: the provider rail is keyed on the group, never on the coupon.

## 7. Execution sequence

Frozen ticket truth stays authoritative and is never rolled back for a later G5 failure.

```text
1  source disposition + cancel terms accepted and durable      before any irreversible work
2  ticket document exchange confirms
3  predecessor ETKT -> Exchanged
4  successor ETKT + lineage materialized
5  G1 mechanical DisassociatedByReissue for every affected EMD-A coupon
6  ticket monetary settlement completes
7  safe automated ancillary dispositions execute in document order
8  the cancel group checks P3-C document-void eligibility
9  the provider void executes or is recovered under one stable key
10 a confirmed provider result becomes local EMD void truth
11 the dependent ancillary OrderServices are cancelled
12 remaining safe ancillary work continues
13 the operation completes only if no ManualReview remains
```

### Eligibility

| Provider answer | Ordering |
| --- | --- |
| `Allowed` | proceed to the void act |
| `PendingEvidence` | `AwaitingExternal`, claim retained, **no void dispatched**, no local void, no service cancellation; a resume re-checks |
| `Denied` | `NeedsReconciliation`, ticket truth retained, no void, no service cancellation |
| `RefundRequiredInstead == true` | `NeedsReconciliation` — see below |

`RefundRequiredInstead` is checked **before** the outcome branch and never triggers G2. The source approved
*cancel without refund*; turning that into a refund is a new economic decision that only the source can make.
On that answer Ordering calls no refund port, moves no value, writes no local void and commits no
`PriceChangeSet`.

### Void execution

| Provider answer | Ordering |
| --- | --- |
| `Confirmed` | validate local compatibility, then EMD → `Voided`, every coupon → `Void`, one `DocumentVoidRecord`, one `DocumentVersion` move |
| `Pending` / `Unknown` | `AwaitingExternal`, claim retained, no local void, no service cancellation, readback only, never a blind redispatch |
| `Rejected` | `NeedsReconciliation`, EMD unvoided, no service cancelled by G5 |

The void is recorded with `VoidReason.Other` and a reason detail naming the approved action and both source
references, e.g. `VoidWithoutRefund approved by ANC-CANCEL on ANC-CANCEL-SOURCE`. No `VoidReason` member
describes "the ancillary was cancelled without refund as a consequence of a reissue", and inventing one is a
wire-contract change this slice was not authorised to make; `Other` plus explicit provenance states the truth
without fabricating a cause.

If the provider confirms but the local document no longer matches the approved group,
`AncillaryCancellationEvidencePolicy` catches it: ticket truth retained, **provider confirmation retained**,
`NeedsReconciliation`, no second provider void, no fabricated local void and no rollback.

## 8. Reusing the frozen `IDocumentVoidPort`

`CheckEligibilityAsync`, `VoidAsync` and `RecoverAsync` are called with
`AccountableDocumentKind.ElectronicMiscDocument`, the source EMD number, the document's actual
`IssuerCarrierId`, and the stable key `ProviderOperationKey(operation, "emd-cancel:{documentNumber}")`. P3-C's
own `DocumentVoidService` is untouched, and G5 creates no child `ServicingOperationKind.VoidDocument` — the
whole cancel lives inside the one Exchange servicing operation, one accepted plan and one `OrderChange`.

The deterministic and unconfigured adapters keep their production contract. The deterministic adapter gained
only test configuration — `ThrowBeforeEligibility`, `ThrowBeforeVoid`, `ThrowAfterVoid`, an
`EligibilityDetail`, and observed-request lists alongside the existing observed-key lists.

### The one place the frozen port is not expressive enough

`DocumentVoidResult` carries no `WasDispatched`, unlike `IEmdExchangePort` and `IEmdAssociationPort`. So the
port alone cannot tell "never dispatched" from "dispatched, outcome unknown", and the recover-first pattern
the other rails use is unavailable. Rather than widen a frozen contract, G5 adds a **local** dispatch claim:
`VoidDispatchedAt` is written and committed *before* the provider call.

```text
VoidDispatchedAt is null      -> nothing was ever sent, dispatch VoidAsync
VoidDispatchedAt is set       -> a call may have reached the provider, RecoverAsync only
```

This makes a duplicate provider void impossible without touching P3-C. Its cost is recorded in
§14 `BLOCKED_INTEGRATION`.

## 9. Commercial consequence

`OrderService` gained one narrow transition, reached only through a guard:

```text
Status            -> Cancelled
CommercialStatus  -> Cancelled
DocumentStatus    -> Voided
FinancialStatus   -> untouched   (no refund happened)
DeliveryStatus    -> untouched   (delivery observation is evidence, not ours to rewrite)
```

None of the existing methods fits: `MarkVoided()` writes `FinancialStatus = Refunded` **and**
`DeliveryStatus = Unused`; `MarkCancelled()` does the same; `MarkDocumentVoided()` — what P3-C's
`ApplyDocumentVoid` calls — does not cancel the service at all and nulls the EMD link. G5 uses none of them.

`Order.CancelAncillariesByDocumentVoid(orderServiceIds, clock)` verifies every service before mutating any:
each must belong to this order, must not be `AirTransportation`, must be commercially `Pending` or `Active`,
and must carry `DocumentStatus.Issued`. Any conflict returns `AncillaryCancellationOutcome.Conflicted` and
**nothing** is written — but the EMD void that already succeeded stays authoritative and is committed by the
reconciliation, exactly as §11 of the brief requires. A pre-existing `Cancelled` service is a conflict, never
proof that G5 already ran; only `CancellationSettledAt` proves that.

No air service is ever mutated. The EMD link on the cancelled service is deliberately left in place as audit
provenance.

## 10. CommercialVersion and pricing

```text
0 new PriceChangeSet for G5 Cancel
0 OrderPricingChanged for G5 Cancel
0 refund value call
0 residual value call
0 wallet / stored-value action
```

**Chosen invariant: one `CommercialVersion` move per committed cancel group**, not one per service. A group is
one accepted consequence and its services transition atomically in one call, so the version advances once even
when the group cancels several services. `G5C2` asserts exactly that for a two-coupon, two-service EMD. Two
independent cancel groups are two consequences and advance twice, which is correct.

Replay of a settled group performs no transition and no second version move; the void, the service
transitions and the settlement checkpoint all commit in one `SaveChangesAsync`, so a settled checkpoint is
proof the whole consequence landed.

## 11. Manual review

`ManualReview` is a source-approved outcome, not an error and not an external act. No provider operation, no
`Pending`/`Unknown`/`Recover`/`WasDispatched` rail and no new aggregate were invented for it.

Durable evidence on the accepted disposition: `Disposition`, `DecisionReference`, `DecisionVersion`,
`DecisionContextFingerprint` and a dedicated `ManualReviewReason` column carrying the source `Detail`. It gets
its own column rather than sharing a free-text field so it is unambiguous. A `ManualReview` with no actionable
reason is `AncillaryManualReviewReasonMissing` (20322, 422) before the ticket; a reason is never fabricated.
A `ManualReview` arriving with a monetary or exchange consequence attached is refused as a malformed decision
(20297).

After the ticket exchange, successor and lineage, G1 disassociation and ticket monetary settlement, and after
every automatable disposition that can settle has settled:

```text
operation                -> NeedsReconciliation
ticket truth              retained
manual-review coupon      OpenForUse, association null, DisassociatedByReissue by this operation
no reassociation, no refund, no EMD exchange, no retention settlement, no EMD void
no value movement, no commercial service mutation
```

The disposition stays intentionally unresolved and visible for reconciliation tooling. It is never marked
`Confirmed` to force `IsAncillarySettled` true.

### Where the two concepts are separated in code

`ManualReview` is deliberately **not** in `ExecutableAncillaries`, so the settlement selector never picks it
and it can never block or starve other work. But it **is** an affected ancillary whose association must not
survive the reissue, so the two mechanical-detachment loops now walk `plan.Ancillaries` instead. Before G5
those two sets were identical, so this is behaviour-identical for G1–G4.

Completion is gated once, centrally, in `CompleteAsync`: a plan with an unresolved manual review reconciles
instead of completing.

## 12. Mixed disposition ordering

Settlement order stays the frozen one — document number ordinal, then coupon number — and no affected
ancillary is silently skipped. `G5M7` runs all six dispositions in one reissue against six EMDs and asserts
each landed:

```text
M1  ReassociateExisting  -> reassociated to the successor ticket coupon
M2  Refund               -> coupon Refunded, document refund + value movement settled
M3  ExchangeToNewEmd     -> coupon Exchanged, successor EMD materialized
M4  RetainAsResidual     -> retention settled, coupon still OpenForUse
M6  Cancel               -> document Voided, service Cancelled/Cancelled/Voided
M8  ManualReview         -> untouched, open, detached, unresolved
                         -> operation ends NeedsReconciliation
```

`G5M6` proves the ordering claim directly: a manual review on `M0…` sorts **first** and the cancel on `M7…`
still settles completely.

`G5M5` proves §17 of the brief: a manual review on one coupon of an EMD prevents cancelling another coupon of
the same EMD, because the whole-document void would terminate the manual-review coupon. That shape fails
closed before the ticket exchange with 20321 — the cancel is never "processed first".

## 13. Crash and recovery matrix

| Boundary | Behaviour | Test |
| --- | --- | --- |
| throw before eligibility | operation `AwaitingExternal`, no eligibility recorded, nothing dispatched | adapter hook available |
| after eligibility, before dispatch | eligibility durable; resume re-reads it and dispatches once | `G5C9` (pending) |
| throw before the provider void | dispatch claim already durable → resume **recovers**, never re-voids | `G5C17` |
| throw after the provider side effect | one void observed; resume recovers, confirms, settles once | `G5C18` |
| provider `Confirmed`, local state now contradicts | confirmation retained, no second void, no local void, reconcile | `G5C15` |
| after EMD void, before the service consequence | EMD void stays authoritative, resume retries the service consequence | covered by §9 guard |
| after the service consequence, before completion | settled checkpoint makes the group inert on replay | `G5C16` |
| replay of a completed cancel | no provider call, no version move, no document version move | `G5C16` |
| replay of a `NeedsReconciliation` manual review | no ticket redispatch, no ancillary dispatch, evidence unchanged | `G5M2` |

Across all of them: one stable key, recover before redispatch, no duplicate provider void, no duplicate
`DocumentVoidRecord`, no duplicate `DocumentVersion` move, no duplicate `CommercialVersion` move.

## 14. Test coverage against the mandated matrix

All 30 mandated post-ticket cases plus the §19 pre-ticket matrix, in 37 tests across two suites.

| Brief case | Test |
| --- | --- |
| 1 single-coupon EMD-A cancel happy path | `G5C1` |
| 2 multi-coupon EMD, all Cancel → one provider void | `G5C2` |
| 3 confirmed void → EMD/coupons Void + services Cancelled | `G5C1`, `G5C2` |
| 4 `FinancialStatus` unchanged | `G5C1` |
| 5 `DeliveryStatus` unchanged including `NoShow` | `G5C5` (Delivered, NoShow, Consumed) |
| 6 air service untouched | `G5C1` |
| 7 no G5 `PriceChangeSet` / no pricing event | `G5C1` |
| 8 no refund / residual / value call | `G5C1`, `G5C11` |
| 9 eligibility `PendingEvidence` | `G5C9` |
| 10 eligibility `Denied` | `G5C10` |
| 11 `RefundRequiredInstead == true` | `G5C11` |
| 12 void `Pending` | `G5C12` |
| 13 void `Unknown` | `G5C13` |
| 14 void `Rejected` | `G5C14` |
| 15 confirmed then local contradiction → reconcile, no second void | `G5C15` |
| 16 replay after a confirmed cancel → exactly once | `G5C16` |
| 17 throw before void | `G5C17` |
| 18 throw after the provider side effect → recovery, no duplicate | `G5C18` |
| 19 unaffected open coupon on the same EMD → pre-ticket refusal | `G5C19` |
| 20 Cancel + Refund on the same EMD → pre-ticket refusal | `G5C20` |
| 21 Cancel + Retention on the same EMD → pre-ticket refusal | `G5C21` |
| 22 ManualReview only → ticket exchange succeeds, then `NeedsReconciliation` | `G5M1` |
| 23 ManualReview preserves open / detached EMD truth | `G5M1` |
| 24 ManualReview causes zero provider / value / service mutation | `G5M1` |
| 25 missing ManualReview detail → pre-ticket refusal | `G5M3` |
| 26 all six dispositions, ManualReview remains, final `NeedsReconciliation` | `G5M7` |
| 27 ManualReview sorting first does not skip later automation | `G5M6` |
| 28 post-ticket moved EMD truth still preserves frozen ticket truth | `G5C31` |
| 29 completed G4/G3/G2/G1 regressions unchanged | full suite, §16 |
| 30 no new `EmdCouponStatus.Cancelled` / document status invented | `G5C32` |

One further clause of §11 has its own test: `G5C33` proves that when the dependent service conflicts **after**
a provider-confirmed void, the EMD void truth stays authoritative and committed, the group does not settle,
no second void is dispatched, and the service G5 did not cancel is left exactly as the other act left it.

Additional coverage beyond the mandated list: `G5C21b` (Cancel + Reassociation), `G5C21c` (already-terminal
coupon on the same EMD), `G5C22`–`G5C26` (the §19 term matrix), `G5C27`–`G5C29` (the §4 target matrix),
`G5C30` (no identified actor), `G5C33` (service conflict after a confirmed void), `G5M4` (ManualReview
carrying a consequence), `G5M5` (§17).

### Discrimination proof

Seven G5 guards were reverted together — the whole-document scope proof, the narrow `OrderService`
transition, the `CompleteAsync` manual-review gate, detachment over every accepted ancillary, the durable
void-dispatch claim, `RefundRequiredInstead`, and the post-confirmation evidence policy — and the two suites
re-run:

```text
with the seven guards reverted:  37 total, 20 passed, 17 failed
```

All 17 were the intended guard failures — `G5C1`, `G5C5`, `G5C11`, `G5C15`, `G5C17`, `G5C18`, `G5C19`,
`G5C20`, `G5C21`, `G5C21b`, `G5C21c`, `G5C33`, `G5M1`, `G5M2`, `G5M5`, `G5M6`, `G5M7` — and nothing else
failed. `G5C30` correctly kept passing, because the pre-ticket actor refusal was not part of that revert set;
it was proven on its own by reverting **only** that guard, which makes exactly `G5C30` fail out of 29. All
guards were then restored and the suites re-run green.

### Superseded frozen tests

`AncillaryDispositionGateTests` asserted that `Cancel` and `ManualReview` are **not** executable
(`AncillaryDispositionNotExecutable`, 20298) — the exact claim this slice retires. No coverage was deleted;
each case was re-pointed at a shape that is still refused, and the suite grew from 21 to 22:

| Was | Now |
| --- | --- |
| `P(Cancel)`, `P(ManualReview)` → 20298 | `P` an undefined disposition value → 20298 (20298 stays reachable and proven); `P2` Cancel on a coupon documenting no service → 20320; `P3` ManualReview with no reason → 20322 |
| `Q` ManualReview on one of two coupons → 20298 | `Q` an undefined disposition on one of two coupons → 20298 |
| `R` refused ManualReview replays terminally → 20298 | `R` reasonless ManualReview replays terminally → 20322 |

## 15. Migration

One additive migration, `P3G5AncillaryCancelAndManualReview`:

```text
CreateTable  Order.AcceptedExchangePlanAncillaryCancelGroups
AddColumn    Order.AcceptedExchangePlanAncillaries.CancelGroupRef
AddColumn    Order.AcceptedExchangePlanAncillaries.CancellationReference
AddColumn    Order.AcceptedExchangePlanAncillaries.CancellationSourceReference
AddColumn    Order.AcceptedExchangePlanAncillaries.CancellationDocumentAction
AddColumn    Order.AcceptedExchangePlanAncillaries.CancelledOrderServiceId
AddColumn    Order.AcceptedExchangePlanAncillaries.ManualReviewReason
CreateIndex  IX_...AncillaryCancelGroups_ElectronicMiscDocumentId
CreateIndex  IX_...Ancillaries_OperationId_CancelGroupRef
```

`Up` contains only `AddColumn`, `CreateTable` and `CreateIndex` — every `Drop*` is in `Down` only. No
historical row is rewritten and no enum is renumbered.

## 16. Freeze gate

```text
dotnet build AeroTech.Ordering.sln                       0 errors
dotnet ef migrations has-pending-model-changes           No changes have been made to the model since the last migration.

AeroTech.Ordering.Domain.Tests                           544 / 544
AeroTech.Ordering.Persistence.Tests                     1240 / 1240

AncillaryCancelFlowTests                     30   new
AncillaryManualReviewFlowTests                7   new
AncillaryDispositionGateTests                22   was 21, re-pointed (see §14)
RetainAsResidualFlowTests                    21
RetainAsResidualFreezeCorrectionTests        10
EmdReassociationFlowTests                    29
AncillaryRefundFlowTests                     35
EmdExchangeToNewEmdFlowTests                 48
EmdExchangeFreezeGateCorrectionTests         43
EmdExchangeFreezeGuardTests                  16
PostDocumentTruthFreezeGateTests             11
ResidualDocumentCouplingTests                15
ResidualEvidenceFreezeGateTests              12
MixedExchangeFlowTests                       53
DocumentVoidFlowTests                        19   the frozen P3-C void, unchanged
Contracts/AncillaryDisposition               20
Contracts/DocumentRefund                     17
Contracts/RefundValue                        11
Contracts/EmdExchange                        18
Contracts/EmdAssociation                     15
Contracts/ExchangeResidual                   12
Contracts/DocumentExchange                    7
```

The brief's gate list names `Contracts/DocumentVoid`. **No such contract suite exists** — the frozen P3-C
void port has never had one; its coverage is `P3.DocumentVoidFlowTests` (19), which is unchanged and green.
Rather than invent a suite to satisfy the list, this is reported as-is.

Every count above is the executed case count. No suite lost coverage; the Persistence total moved
`1202 -> 1240` as `+37` new G5 tests and `+1` from splitting one gate theory into three facts.

Exception codes: 323 codes, 20001–20323, contiguous, no duplicates. New in G5: 20318
`AncillaryCancellationTermsMissing`, 20319 `AncillaryCancellationDocumentActionNotExecutable`, 20320
`AncillaryCancellationTargetNotEligible`, 20321 `AncillaryCancellationScopeWiderThanApproved`, 20322
`AncillaryManualReviewReasonMissing`, 20323 `AncillaryCancellationRequiresAuthority`.

## 17. Files changed

**New (7)**

```text
Contracts/AeroTech.Messages/Ordering/Enums/AncillaryCancellationDocumentAction.cs
src/AeroTech.Ordering.Domain/Ports/AncillaryDisposition/AncillaryCancellationTerms.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/AcceptedExchangeAncillaryCancelGroup.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/Policies/AncillaryCancellationEvidencePolicy.cs
src/AeroTech.Ordering.Domain/OrderAggregate/Dto/AncillaryCancellationOutcome.cs
src/AeroTech.Ordering.Domain/OrderAggregate/Order.AncillaryCancellation.cs
src/AeroTech.Ordering.Application/.../Exchange/ExchangeService.AncillaryCancel.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanAncillaryCancelGroupRow.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanAncillaryCancelGroupConfiguration.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/AncillaryCancelFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/AncillaryManualReviewFlowTests.cs
```

**Modified**

```text
Domain/Ports/AncillaryDisposition/AncillaryCouponDisposition.cs      Cancellation terms
Domain/ElectronicMiscDocumentAggregate/ElectronicMiscDocument.cs     scope predicate, void replay proof
Domain/OrderAggregate/Entities/OrderService.cs                       cancel guard + narrow transition
Domain/Servicing/Plans/AcceptedExchangeAncillaryDisposition.cs       cancel + manual-review evidence
Domain/Servicing/Plans/AcceptedExchangePlan.cs                       cancel groups, manual-review selector
Domain/Servicing/Plans/Contracts/IAcceptedExchangePlanStore.cs       three cancel checkpoints
Domain/_Shared/Resources/ExceptionFactory.cs, ExceptionMessages.cs   20318-20323
Application/.../Exchange/ExchangeAncillaryPlanner.cs                 acceptance + scope proof
Application/.../Exchange/ExchangeService.cs                          port, acceptance, detachment, gate
Application/.../Exchange/ExchangeOperationKeys.cs                    emd-cancel step
Persistence/Servicing/AcceptedExchangePlan*(Row|Configuration|Store) additive persistence
Persistence/Migrations/…_P3G5AncillaryCancelAndManualReview          additive migration
Providers.Deterministic/DeterministicDocumentVoidAdapter.cs          test configuration only
Providers.Deterministic/DeterministicAncillaryDispositionAdapter.cs  cancel + manual-review terms
tests/.../P3/ExchangeScenarios.cs                                    AttachServiceAncillaryAsync
tests/.../P3/AncillaryDispositionGateTests.cs                        re-pointed, see §14
tests/.../P1/OrderSliceHarness.cs                                    document-void injection
tests/.../_Shared/TestCallerContexts.cs                              optional actor id
```

## 18. BLOCKED_DECISION

**None for the shapes this slice implements.** Whole-document `Cancel` closes entirely on the frozen
vocabulary: `ElectronicMiscDocumentStatus.Voided`, `EmdCouponStatus.Void`,
`OrderServiceDocumentStatus.Voided`. No `EmdCouponStatus.Cancelled`, `Forfeited` or `Used` was created, and
`G5C32` asserts that vocabulary directly.

Two shapes are held as `BLOCKED_DECISION` rather than guessed:

1. **Partial cancel-without-refund** — one coupon of a multi-coupon EMD forfeited while its siblings survive.
   The frozen EMD void is whole-document and the benchmark authorises Void or Refund, not a coupon-level
   forfeiture state. Ordering refuses this shape at acceptance with 20321 instead of widening void scope or
   inventing a terminal coupon status. Making it executable requires an explicit business and accountable-
   document decision.
2. **Cancel of a `Fee`, `Deposit` or `ResidualValue` coupon.** These carry no delivering service, so there is
   no commercial consequence to apply and no source-approved answer to "where did the value go". Refused with
   20320.

Neither blocks the valid full-document `Cancel` path.

## 19. BLOCKED_INTEGRATION

1. No real ancillary disposition source is wired, so no real carrier ever returns `Cancel` or `ManualReview`.
   `UnconfiguredAncillaryProvider` fails closed with 501.
2. No real document-void authority is wired for miscellaneous documents. `UnconfiguredDocumentVoidProvider`
   fails closed. Whether a real issuer distinguishes "void, forfeit residual" from "void, refund to FOP" the
   way Travelport documents is unverified from inside Ordering.
3. **`IDocumentVoidPort.RecoverAsync` cannot report whether a call was ever dispatched.** Ordering compensates
   with its own durable pre-dispatch claim (§8), which guarantees no duplicate void. The cost is that a crash
   between writing the claim and the call reaching the provider parks the operation at `AwaitingExternal`
   until the provider's readback resolves it — Ordering will never blind-redispatch to get out of that state.
   Widening the frozen P3-C result with `WasDispatched`, as `IEmdExchangePort` carries, would remove this;
   that is a P3-C decision, not a G5 one.
4. Whether a real issuer's void is idempotent under Ordering's `emd-cancel:{documentNumber}` operation key.
5. Whether a real issuer returns `RefundRequiredInstead` at all, and whether it does so as `Denied` plus the
   flag or as `Allowed` plus the flag. Ordering honours the flag in both cases and refunds in neither.
6. No operator remediation command exists yet for a `NeedsReconciliation` cancel group or an unresolved
   `ManualReview`. Every piece of evidence such a command would need is persisted. That is P3-H.

## 20. Freeze verdict

```text
P3-G5 READY TO FREEZE: YES
```
