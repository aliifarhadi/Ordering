# P3-H — Reconciliation, Consistency Hardening, Involuntary Boundary, Final P3 Audit

## 1. Actual starting HEAD

```text
67b3420dcea746594fdd9aa5e570b7a242cfee35   P3-G6 Fix
```

That is the frozen final P3-G baseline named by the corrected brief. No frozen P3-B..G rail was refactored
for cosmetic reasons.

---

## 2. Phase-plan scope interpretation

The corrected brief and `reports/order-domain-v1/p3/P3-PHASE-PLAN.md` agree on a hardening phase, not a
disruption-recovery phase. P3-H was therefore read as:

```text
IN     cross-P3 reconciliation visibility over evidence that already exists
IN     operator/manual-review evidence and a narrow, auditable recovery surface
IN     ControlStatus reconciliation evidence and its authority boundary
IN     Order vs Inventory consistency reporting for supported shapes
IN     involuntary BOUNDARY only
IN     SSR and no-show negative guarantees
IN     status lifecycle, terminal-consistency, concurrency and idempotency audit
IN     final P3-B..H architecture/correctness audit

OUT    DCS integration
OUT    disruption recovery workflow
OUT    no-show flow
OUT    reaccommodation search / alternative-itinerary selection
OUT    involuntary pricing policy
OUT    any P4 work
```

A real gap was found and closed on the way in: **four P3 rails dispatched irreversible external mutations but
retained no durable record of the provider's answer.** Under §6 ("if a rail does not retain enough evidence to
explain a real `NeedsReconciliation` state, that is a P3 correctness gap — MUST fix the minimum durable
evidence necessary"), that evidence was added additively rather than working around it in the query.

---

## 3. Reconciliation query

### Shape

```text
Query project (CQRS read side)
  GetServicingReconciliationQuery(OperationId)            -> ServicingReconciliationView?
  GetUnresolvedServicingReconciliationQuery(OrderId)      -> IReadOnlyList<ServicingReconciliationView>
     -> ServicingReconciliationComposer
        -> IServicingReconciliationStore     (thin projections)
        -> IServicingExternalEvidenceStore   (durable provider outcomes)
        -> IServicingManualResolutionStore   (operator audit)
        -> IAcceptedExchangePlanStore        (durable servicing plan checkpoints)
```

Both queries live in `src/AeroTech.Ordering.Query/OrderAggregate/Queries/GetServicingReconciliation/` with
MediatR `IRequest`/`IRequestHandler`, per the repository rule that **every query lives in the Query project**.
The composed shape is `src/AeroTech.Ordering.Query/OrderAggregate/View/ServicingReconciliationView.cs`.

### Correction made during this phase

The first cut of the composer injected `IElectronicTicketRepository` and `IElectronicMiscDocumentRepository`
— write-side aggregate repositories, and `OrderRepository` alone carries 29 `Include`s with `AsSplitQuery`.
That violates the CQRS rule the architecture skill states plainly: **reads use thin data access, writes go
through the domain model.** It was rewritten to `AsNoTracking()` projections before anything was frozen.

### What an operator can see

```text
OperationId, OrderId, ServicingOperationKind, live ServicingOperationStatus
ClaimGeneration, ExpectedCommercialVersion, CreatedAt, UpdatedAt
CallerScope, IdempotencyKey, CommandReceiptStatus
UnresolvedStage                       — which capability is actually stuck
ExternalEvidence[]                    — stage, outcome, provider reference, detail, document identity
Documents[]                           — ETKT/EMD kind, number, status, version, predecessor lineage
ControlEvidence[]                     — per coupon control and financial status
ReservationEvidence[]                 — per service observed vs external inventory state
ManualReviewReasons[]                 — retained G5 manual-review reasons
ManualResolutions[]                   — the operator audit trail
RecoveryAction                        — NoneRequired | ReplayCommand | ManualResolutionRequired
```

Derived helpers keep the four states separate rather than collapsing them: `AwaitsExternal`,
`NeedsReconciliation`, `IsRejected`, `IsCompleted`, plus `ConfirmedEvidence`, `UnresolvedEvidence`,
`NonLocalControl` and `UnresolvedReservations`.

No persisted shadow projection was created. No provider truth is synthesised.

---

## 4. Evidence sources

Every field is read from evidence that already existed, except the external-outcome rows named in §5.

| Evidence | Source |
| --- | --- |
| Operation identity, status, claim generation, versions | `Order.ServicingOperations` |
| Caller scope, idempotency key, receipt status | `Order.CommandReceipts` (left join) |
| Servicing plan stage checkpoints, manual reviews | `AcceptedExchangePlan` (P3-F/G) |
| ETKT/EMD truth, version, predecessor lineage | `ElectronicTicket`, `ElectronicMiscDocument` |
| Coupon control and financial status | `TicketCoupon` |
| Inventory observation | `FulfillmentReservation` + `FulfillmentReservationServices` |
| External mutation outcome per stage | `Order.ServicingExternalEvidences` (new, §5) |
| Operator decisions | `Order.ServicingManualResolutions` (new, §7) |

---

## 5. Durable external-outcome evidence — the P3 gap that was closed

### The gap

`DocumentVoidService`, `RefundService`, `CancelRefundService` and `OrderCancelService` each transitioned an
operation to `AwaitingExternal` or `NeedsReconciliation` after an uncertain provider answer, but **nothing
durable recorded what the provider actually said**. An operator inheriting such an operation could see that it
was stuck and not why, and no later reader could distinguish `Pending` from `Unknown` from `Rejected`.

### The fix

```text
Contracts/AeroTech.Messages/Ordering/Enums/ServicingEvidenceStage.cs
  DocumentVoid = 1, DocumentRefund = 2, RefundValue = 3,
  RefundCorrection = 4, RefundValueCorrection = 5, ReservationRelease = 6

Domain/Servicing/Reconciliation/ServicingExternalEvidence.cs
Domain/Servicing/Reconciliation/Contracts/IServicingExternalEvidenceStore.cs
Persistence/Servicing/ServicingExternalEvidence{Row,Configuration,Store}.cs
```

Table `Order.ServicingExternalEvidences`, primary key `(OperationId, Stage)`.

**Confirmed is monotonic.** The store's upsert returns early when the stored outcome is already `Confirmed`,
so a later uncertain readback cannot downgrade confirmed truth. This is the same invariant established in
P3-G6 for `AcceptedExchangeFeeDocument.WithIssuanceAttempt`, now applied at the evidence layer.

The four rails record at their suspend, reconcile, finalize and reject points, threading the provider
reference and recovery detail. No provider payload is copied.

---

## 6. ControlStatus findings

`TicketCouponControlStatus { Local, External, ReleasePending, Unknown }` on `TicketCoupon` is the existing
domain fact. P3-H invented no second control model and changed no guard.

```text
surfaced          ServicingControlEvidence + IsLocallyControlled, and the view's NonLocalControl
fail-closed kept  a non-Local coupon still refuses a protected document action (20205) before dispatch
never inferred    nothing derives control from coupon status, time, absence of usage, order status,
                  free text, or servicing-operation state
never returned    no read path and no operator resolution moves a coupon back to Local
```

Discriminating tests: R9 (fail-closed under `External`), R10/R11/R12 (each non-`Local` state visible), R13
(composing the whole reconciliation view leaves control untouched), R14 (recording an operator resolution
leaves control untouched).

### ControlStatus integration gap

`BLOCKED_INTEGRATION` — see `ICC-P3-CONTROL-RETURN`. No port offers an authoritative control-return or
control-readback capability, so a coupon can be stranded under non-`Local` control indefinitely. That is the
intended fail-closed behaviour. Ordering must not guess, must not time out of `ReleasePending`, and must not
treat `Unknown` as `Local`.

---

## 7. Order vs Inventory consistency

### Authority boundary

```text
Ordering  order, accepted servicing intent, ticket/EMD truth, durable repair intent and evidence
Provider  allocated inventory, availability, external operational fulfilment facts
```

### What is detectable today

| Situation | How it is reported |
| --- | --- |
| Agree | operation `Completed`, no unresolved evidence, `NoneRequired`, order absent from `ListUnresolvedAsync` |
| Confirmed locally, Inventory drifted | document truth unchanged; `ServicingReservationEvidence.IsObservationUnresolved` |
| Repair `Pending` / `Unknown` | `AwaitingExternal`, then `NeedsReconciliation` on replay; `ReservationRelease` evidence |
| Repair `Rejected` | operation `Rejected`, order not cancelled, no fabricated local success |
| Contradiction not automatable | `NeedsReconciliation` with the stage, outcome and reservation evidence attached |

Confirmed accountable-document truth is never rolled back to match stale Inventory (R15).

### Repair

The existing frozen `IReservationPort` release/recover rail is reused unchanged, under the same
`ProviderOperationKey(operation, step)` identity. Replay after an uncertain release calls `Recover` under the
original key and dispatches no second release (R16, R17). **No `IInventoryConsistencyPort` was created** and no
generic inventory reconciliation infrastructure was built.

### Gap

`BLOCKED_INTEGRATION` — see `ICC-P3-INVENTORY-CONSISTENCY`. `RecoverAsync` answers *what happened to my
operation*, not *what does the provider hold now*. A drifted `ObservedStatus` can be reported but not
refreshed.

---

## 8. Supplier disagreement

A disagreement that cannot be automated stays `NeedsReconciliation` and carries, durably: what Ordering
believes (document and control evidence), what the external authority said (stage, outcome, provider
reference, detail), which operation and document are affected, and why automation stopped (the unresolved
stage). No heuristic picks a side, and confirmed document truth is never overwritten.

---

## 9. Operator recovery semantics

Allowed kinds, each admissible only for the recovery action the durable evidence supports:

```text
ResumeFromCheckpoint    only when the evidence says a replay can still resolve it
RecordManualDecision    only when manual resolution is the only remaining action
EscalateExternalAction  only when manual resolution is the only remaining action
```

Not added, and provably absent: `ForceComplete`, `MarkResolved`, `SetCompleted`, `AssumeConfirmed`,
`IgnoreProvider`, `ResetToLocal`, `RetryEverything`. **Recording a resolution changes no operation status, no
aggregate, no document and no coupon control** (R24). A settled operation refuses every resolution (R24b,
20329). A kind that does not match the evidence is refused (R24c, R24d, 20331).

---

## 10. Manual resolution audit model

```text
Order.ServicingManualResolutions
PK (OperationId, ResolutionId)

OperationId
ResolutionId              server-derived: {Kind}:{Reference}:{OperationId}
Kind                      ServicingResolutionKind
Actor                     required, non-blank
Reason                    required, non-blank
Reference                 optional external case reference
EvidenceStage             optional ServicingEvidenceStage
ExpectedClaimGeneration   the concurrency token the caller read
RecordedAt
```

Append-only. The `ResolutionId` is **server-derived and deterministic**, matching the repository rule that
servicing operations take no client idempotency key. That single choice gives all three required behaviours:

```text
same operator, same decision, twice   no-op; one row; original actor and timestamp retained   (R20)
second actor, same decision           no-op; cannot duplicate; first actor retained            (R22)
stale expected generation             20330 / 409; nothing written; status unchanged           (R21)
```

Survives reload with every field intact and appears on the reconciliation view (R23).

No distributed lock is taken, deliberately: the operation being resolved already holds the order claim, so
claiming it again would be wrong. The primary key plus the claim-generation guard are the concurrency control,
which is what §19 asks for ("use existing claim/version patterns; do not add distributed locking unless proven
necessary").

New exception codes, contiguous and unique inside 20000–29999:

```text
20328  ServicingResolutionOperationNotFound      404
20329  ServicingResolutionOperationIsSettled     409
20330  ServicingResolutionClaimGenerationStale   409
20331  ServicingResolutionKindNotApplicable      409
20332  ServicingResolutionAttributionRequired    422
```

Verified: 332 codes, 20001–20332, no gaps, no duplicates.

---

## 11. Involuntary boundary — boundary only

### Zero schema change was required

`OrderChange` already carries everything the boundary needs:

```text
ChangeType        including the reserved Reaccommodation and InvoluntaryChange values
Reason            the decision reference
Source            PricingSource — the authority
ExternalReference the target selection reference
ActorScope/ActorId
OperationId
```

### Authority and reason preservation

The accepted source's authority, reason and reference land on `OrderChange` verbatim and Ordering interprets
none of them (R25b). `Reaccommodation` and `InvoluntaryChange` remain **unreachable from production code** —
no rail writes either, and no rail writes `PriceChangeReason.InvoluntaryChange` (R25, R29b).

### Delegation to frozen rails

The Revalidate-vs-Reissue decision comes from `IDocumentChangeEligibilityPort`, an external authority.
Ordering never infers it:

```text
Revalidate       -> frozen P3-E rail; same document revalidated; no exchange port touched   (R26)
ReissueRequired  -> frozen P3-F rail; no revalidation; no document mutation; no new engine   (R27)
PendingEvidence  -> AwaitingExternal; fail closed; nothing inferred                          (R29)
Denied           -> Rejected, honestly
```

### Proof no disruption recovery was built

No disruption detection, no availability search, no alternative-itinerary selection, no reaccommodation
engine, no right-to-care or compensation logic exists in the diff. The only new decision logic is
`ServicingRecoveryPolicy` (which recovery action the durable evidence supports) and `ServicingResolutionPolicy`
(whether an operator resolution is admissible). Neither touches itineraries or money.

### Proof no involuntary pricing policy was built

The entire P3-H diff contains no arithmetic on money. An accepted action with no source economics moves
nothing: pricing-line count, customer total, financial sequence, obligation version and price-change-set count
are all unchanged, and no funding, refund-value or residual port is dispatched (R28). No local
"mandatory reaccommodation means zero fare difference" rule exists.

### Gap

`BLOCKED_INTEGRATION` — see `ICC-P3-INVOLUNTARY-BOUNDARY`. No authoritative accepted-involuntary source
contract exists, so the reserved enum values stay unreachable.

---

## 12. SSR evidence boundary

**Ordering has no SSR type at all** — zero occurrences across `src/` and `Contracts/`. The nearest carrier of
SSR-shaped free text is `OrderRemark`, which is never parsed, never an authority, and never an input to
servicing eligibility or to control status. Tests add a deliberately SSR-shaped remark
(`"SSR DOCS HK1 / WCHR / PASSENGER DID NOT TRAVEL / NOSHOW"`) and prove servicing eligibility is unchanged
(R32/R33) and coupon control is unchanged (R34).

Authoritative SSR interpretation belongs outside Ordering: `BLOCKED_INTEGRATION`.

---

## 13. No-show negative guarantee

`OrderServiceDeliveryStatus.NoShow` exists in the enum and **is never written by any production path** —
source review confirms only `NotReady` and `Unused` are ever assigned. Tests advance the clock 400 days past
departure and prove:

```text
R30  elapsed departure creates no NoShow; coupons stay Open
R31  an open coupon with no usage evidence creates no NoShow; services stay Unused
R32  SSR-shaped free text creates no NoShow
```

No no-show flow, no no-show penalty, no automatic coupon expiry after departure. A DCS/usage authority is
`BLOCKED_INTEGRATION` and a future phase.

---

## 14. ServicingOperationStatus live/dead audit

Production references in `src/`:

| Value | Refs | Verdict |
| --- | --- | --- |
| `Prepared = 1` | 1 | live (set at prepare) |
| `Executing = 2` | 13 | live |
| `AwaitingExternal = 3` | 40 | live |
| `ReadyToFinalize = 4` | 0 | **historical / dead** |
| `Committed = 5` | 0 | **historical / dead** |
| `Completed = 6` | 27 | live |
| `Rejected = 7` | 32 | live |
| `Compensating = 8` | 0 | **historical / dead** |
| `NeedsReconciliation = 9` | 25 | live |

The three dead values were **left unchanged**: not renumbered, not repurposed, not deleted, and no fake
transition was created to make them look exercised.

Actual lifecycle:

```text
Prepared -> Executing -> Completed                              provider Confirmed
                      -> Rejected                               provider or business Rejected
                      -> AwaitingExternal                       provider Pending / Unknown
AwaitingExternal -> Completed                                   Recover returns Confirmed
                 -> Rejected                                    Recover returns Rejected
                 -> NeedsReconciliation                         Recover still uncertain
NeedsReconciliation -> Completed / Rejected                     only via a legitimate later recovery
```

---

## 15. Terminal consistency audit

| Terminal state | Guarantee | Where enforced |
| --- | --- | --- |
| `Completed` | no required unresolved external mutation, local materialization, ancillary action, G6 document or manual review | the exchange completion gate routes to `ReconcileAsync` when `RequiresMonetarySettlement`, `RequiresFeeDocumentation`, `RequiresAncillaryReassociation` or `HasUnresolvedManualReview` is still open; verified by R40 |
| `AwaitingExternal` | at least one legitimate `Pending`/`Unknown` dependency; resume uses `Recover`, never a blind new dispatch | R17, R29; every rail's `ReplayUnfinishedAsync` calls `RecoverAsync` first |
| `NeedsReconciliation` | an explicit durable contradiction or manual-review reason; confirmed document truth retained | R19, R24e; evidence rows plus `AncillaryManualReviews` |
| `Rejected` | an honest business/provider rejection, never a synonym for not-dispatched or for reconciliation | R7, R18, R27 |

R40 additionally proves a `Completed` operation reports a null unresolved stage, no unresolved evidence, no
manual review, no manual resolution, `NoneRequired`, only confirmed evidence, and is absent from the
order's unresolved list.

---

## 16. Concurrency and idempotency audit

| Requirement | Result |
| --- | --- |
| parallel resume workers | **was broken; fixed in this run (D1, §17b).** Two harnesses racing the same key now produce exactly one provider dispatch, one operation id, one document-version bump and one evidence row (R39) |
| duplicate operator recovery | idempotent no-op, one audit row (R20) |
| stale expected version | 409 with no mutation (R21) |
| second actor, same resolution | cannot duplicate (R22) |
| replay after provider `Confirmed` | no redispatch, no recovery call, status stays `Completed` (R36, R37) |
| replay after local materialization | no duplicate document version, price link, pricing line, order change or commercial version bump (R38) |
| uncertain external mutation | recover under the original key, never a blind retry (R17) |

No new distributed lock was added. The existing claim/version patterns carry the guarantees.

---

## 17. Confirmed-truth audit across P3-B..G

Every mutation port used by a P3 rail was inspected for a `Recover`/readback path:

```text
has Recover   DocumentExchange, DocumentIssuance, EmdIssuance, DocumentRefund,
              DocumentRefundCorrection, DocumentRevalidation, DocumentVoid, EmdExchange,
              ExchangeResidualValue, RefundValue, RefundValueCorrection, Reservation,
              ReservationChange, EmdAssociation, ExchangeFunding (guarantee/capture/release)

no Recover    AncillaryDisposition — correct: DecideAsync is a decision read, not a mutation
```

For each rail: identity is `ProviderOperationKey(operation, step)`, durable checkpoints exist around the
external side effect, `Pending` stays distinct from `Unknown`, `Rejected` stays distinct from not-dispatched,
`Confirmed` is monotonic (now also at the evidence layer, §5), replay is exact, and no duplicate local
history, document, event or version is produced.

Three real defects were found and all three were fixed in this run, each with a discriminating test — see
§17b. The most serious, D1, is a concurrency hole in the shared durable operation rail that let two parallel
workers dispatch the same irreversible provider mutation. It was found by the P3-H test matrix itself, not by
review.

---

## 17b. Correctness defects found and fixed in this run

Three real defects were found — one by the P3-H test matrix itself, two by source review. All three are fixed
here, each with a discriminating test.

### D1 — parallel workers could dispatch the same irreversible provider mutation twice

**Found by** R39, which is exactly the "parallel resume workers" case §19 asks for.

**Symptom.** Two workers carrying the same idempotency key concurrently both reached the provider. The
diagnostic captured before the fix:

```text
dispatched=2; operations=639249231735103183,throw; replay=False,throw; status=Completed,throw
```

One worker completed; the other **also called the provider's void** and only then failed, on the claim
generation guard — after the irreversible side effect.

**Cause.** `OperationClaimStore.AcquireAsync` is deliberately re-entrant for the *same* operation id, because
that is how replay and crash recovery resume a frozen orchestration. It therefore could not distinguish

```text
a sequential replay of a quiescent operation      legitimate, must be allowed immediately
a second worker inside the operation right now    a duplicate mutation
```

The window is wider still: the loser could arrive before the winner had written its `ServicingOperation` row,
so `ReplayUnfinishedAsync` found no prior operation and fell through to a fresh dispatch.

**Fix.** `OrderOperationCoordinator.BeginAsync` now refuses re-entry while another worker is demonstrably
inside the operation, using only fields that already existed:

```csharp
private async Task EnsureNoLiveWorkerAsync(long orderId, long operationId, CancellationToken ct)
{
    var blocking = await _claims.FindBlockingAsync(orderId, ct);

    if (blocking is null
        || blocking.OperationId != operationId
        || blocking.RecoveryLeaseUntil <= _clock.GetDateTime())
        return;

    var prior = await _operations.FindAsync(operationId, ct);

    if (prior is null
        || prior.Status is ServicingOperationStatus.Prepared or ServicingOperationStatus.Executing)
        throw ExceptionFactory.OperationClaimConcurrentlyAcquired(orderId);
}
```

The rule reads: *a live recovery lease plus an operation that is either not yet written or still `Executing`
means a worker is inside — refuse.* Everything else is a quiescent replay or a genuine crash recovery after
the lease expired, and proceeds exactly as before.

This uses the existing claim/version pattern and the already-persisted `RecoveryLeaseUntil`, which until now
was written and never read. **No distributed lock was added**, per §19.

**Discrimination.** Before the fix R39 reported `dispatched=2`; after it, `dispatched=1`, one operation id,
one document-version bump, one evidence row.

### D2 — four rails recorded no durable external outcome

Covered in full in §5. Found by source review while building the reconciliation read path: `DocumentVoid`,
`Refund`, `CancelRefund` and `OrderCancel` suspended or reconciled on an uncertain provider answer without
persisting what the provider said. Fixed with `Order.ServicingExternalEvidences` and monotonic `Confirmed`.

**Discrimination.** R3, R19, R36 and R37 all fail without the evidence rows.

### D3 — the refund rails dropped an available provider reference

**Found by** source review of the D2 fix itself. `RefundService` and `CancelRefundService` had the provider
reference in hand — from `result.ProviderReference` on dispatch and `recovery.ProviderReference` /
`recovery.Detail` on readback — but passed `null` into the evidence record, so the operator would have seen an
outcome with no way to trace it with the issuer.

**Fix.** `providerReference` and `detail` are threaded through `SuspendAsync`, `ReconcileAsync` and
`SettleUnfinishedAsync` on both rails, matching what `DocumentVoidService` already did.

**Not a defect:** `OrderCancelService` records `ReservationRelease` with a null provider reference on purpose.
A cancel releases many reservations, so no single operation-level reference is meaningful; the per-reservation
`ExternalReservationRef` is surfaced instead through `ServicingReservationEvidence`.

---

## 18. New / changed ports

```text
new ports                 NONE
changed ports             NONE
new deterministic adapters NONE
changed deterministic adapters NONE
```

P3-H added read/record seams inside the Domain servicing area, not integration ports, because no new external
capability was required for any supported repair shape. Three external capabilities that *would* be required
for unsupported shapes are documented as `BLOCKED_INTEGRATION` rather than invented.

Unconfigured production adapters remain fail-closed: all 22 throw, backed by 22 `HttpStatus = 501` factory
methods.

---

## 19. Integration catalog

`reports/order-domain-v1/p3/P3-integration-capability-catalog.md` grew from 15 to 21 entries. New:

```text
ICC-P3-SERVICING-RECONCILIATION   the evidence/read surface; monotonic Confirmed; no shadow projection
ICC-P3-OPERATOR-RESOLUTION        the manual resolution audit; deterministic key; no force-complete
ICC-P3-CONTROL-RETURN             BLOCKED_INTEGRATION — no authoritative control return/readback
ICC-P3-INVENTORY-CONSISTENCY      BLOCKED_INTEGRATION — no "read current provider inventory state"
ICC-P3-INVOLUNTARY-BOUNDARY       BLOCKED_INTEGRATION — no accepted-involuntary source contract
ICC-P3-NO-SHOW-EVIDENCE           BLOCKED_INTEGRATION — no DCS/usage authority
```

Each states the missing capability precisely, its authority owner, what Ordering may safely do meanwhile, and
what Ordering must not invent.

---

## 20. Migration

```text
src/AeroTech.Ordering.Persistence/Migrations/20260913154217_P3HServicingReconciliationEvidence.cs
```

Additive only:

```text
CREATE TABLE Order.ServicingExternalEvidences   PK (OperationId, Stage)
CREATE TABLE Order.ServicingManualResolutions   PK (OperationId, ResolutionId)
CREATE INDEX IX_ServicingExternalEvidences_DocumentNumber
```

No destructive `Up`, no enum renumbering, no historical rewrite, no speculative backfill.

---

## 21. Edge-case matrix → executable tests

| # | Requirement | Test |
| --- | --- | --- |
| R1 | exact OperationId/OrderId/kind/status | `ServicingReconciliationQueryTests.R1_…` |
| R2 | unresolved stage exposed | `…R2_…` |
| R3 | durable provider reference/outcome exposed | `…R3_…` |
| R4 | relevant ETKT/EMD confirmed truth exposed | `…R4_…` |
| R5 | relevant coupon `ControlStatus` exposed | `…R5_…` |
| R6 | `AwaitingExternal` ≠ `NeedsReconciliation` | `…R6_…` |
| R7 | `Rejected` ≠ `NeedsReconciliation` | `…R7_…` |
| R8 | completed work is not reported unresolved | `…R8_…`, `…R8b_…` |
| R9 | non-`Local` control stays fail-closed | `…R9_…` |
| R10–R12 | `External` / `ReleasePending` / `Unknown` visible | `…R10_R11_R12_…` (Theory ×3) |
| R13 | no local heuristic returns control to `Local` | `…R13_…` |
| R14 | missing control-return capability is blocked, not guessed | `…R14_…` + `ICC-P3-CONTROL-RETURN` |
| R15 | stale Inventory never rolls back document truth | `ServicingInventoryConsistencyTests.R15_…` |
| R16 | repair reuses the stable operation key | `…R16_…` |
| R17 | `Pending`/`Unknown` never blindly redispatched | `…R17_…` (Theory ×2) |
| R18 | `Rejected` never fabricates local success | `…R18_…` |
| R19 | contradiction yields actionable evidence | `…R19_…`, `…R19b_…` |
| R20 | duplicate operator recovery is idempotent | `ServicingResolutionAuditTests.R20_…` |
| R21 | stale version fails with no mutation | `…R21_…` |
| R22 | second actor cannot duplicate a resolution | `…R22_…` |
| R23 | audit survives reload | `…R23_…` |
| R24 | no generic force-complete path | `…R24_…`, `…R24b_…`, `…R24c_…`, `…R24d_…`, `…R24e_…`, `…R24f_…`, `…R24g_…` |
| R25 | involuntary boundary preserves authority/reason | `InvoluntaryBoundaryTests.R25_…`, `…R25b_…` |
| R26 | `Revalidate` delegates to frozen P3-E | `…R26_…` |
| R27 | `Reissue` delegates to frozen P3-F | `…R27_…` |
| R28 | no source economics ⇒ no invented money | `…R28_…` |
| R29 | insufficient semantics ⇒ fail closed | `…R29_…`, `…R29b_…` |
| R30 | elapsed departure creates no `NoShow` | `NoShowAndRemarkNegativeGuaranteeTests.R30_…` |
| R31 | open coupon + missing usage creates no `NoShow` | `…R31_…` |
| R32–R33 | free text creates no `NoShow`, changes no eligibility | `…R32_R33_…` |
| R34 | free text never alters `ControlStatus` | `…R34_…` |
| R35 | used/terminal coupon stays protected | `ServicingConfirmedTruthReconciliationTests.R35_…` |
| R36 | `Confirmed` never downgraded by recovery | `…R36_…` |
| R37 | provider `Confirmed` never redispatched | `…R37_…` |
| R38 | replay duplicates no local truth | `…R38_…` |
| R39 | parallel workers cannot duplicate a mutation | `…R39_…` |
| R40 | `Completed` retains no unresolved obligation | `…R40_…` |

---

## 22. Test counts

```text
ServicingReconciliationQueryTests                15   (R1–R14; R10/R11/R12 is a Theory ×3)
ServicingResolutionAuditTests                    11   (R20–R24 and its refusal cases)
ServicingInventoryConsistencyTests                7   (R15–R19b; R17 is a Theory ×2)
InvoluntaryBoundaryTests                          7   (R25–R29b)
NoShowAndRemarkNegativeGuaranteeTests             4   (R30–R34)
ServicingConfirmedTruthReconciliationTests        6   (R35–R40)
--------------------------------------------------
new P3-H test cases                              50   (45 methods; two Theories expand to 5 cases)
```

No frozen test was weakened or deleted. The one frozen-test change is additive: `RevalidationFixture` gained a
`ChangeAsync` overload returning the outcome, and `RevalidateAsync` now delegates to it — same behaviour, no
assertion touched.

---

## 23. Gate results

```text
BUILD                 0 errors (AeroTech.Ordering.sln)
EF                    no pending model changes (OrderingDbContext)
DOMAIN                585 / 585 passed
PERSISTENCE           1326 / 1326 passed, 15 m 11 s
```

Baseline was 1276 Persistence tests; 1326 = 1276 frozen + 50 new P3-H cases. Every frozen test passes
unmodified — none was weakened or deleted to reach green.

The gate was run twice. The first full run failed 37 tests on an intermediate tree, and that run is what
surfaced D1. Of those 37, 36 were a single cause — the reconciliation store projected into
`ServicingOperationSnapshot` before filtering, which EF cannot translate — plus four test-authoring mistakes
of mine (an issued order's services are `NotReady`, not `Unused`; `OrderRepository` does not load `Remarks`;
and a cross-harness replay needs a shared caller because the command receipt is caller-scoped). The 37th was
`EmdExchangeFreezeGateCorrectionTests.C2`, which passes on the final code. The numbers above are the final
code.

---

## 24. BLOCKED_DECISION

```text
NONE
```

Every shape P3-H needed to decide was decidable from the ratified design documents and the existing model. The
narrow resolution vocabulary was derived from §10's own allowed-actions list rather than invented.

---

## 25. BLOCKED_INTEGRATION

| Missing capability | Authority owner | Safe meanwhile | Must not invent |
| --- | --- | --- | --- |
| Authoritative control return / readback | controlling carrier or issuer | surface non-`Local` control as evidence; keep every protected action fail-closed | a control state, a `ReleasePending` timeout, or `Unknown` treated as `Local` |
| "Read current provider inventory state" | fulfilment provider | report drift from Ordering's own recorded observation; repair only via the frozen reservation rail under its stable key | a provider inventory answer, or a fake local repair that makes the two sides agree |
| Accepted pre-decided involuntary result | upstream disruption authority | keep `Reaccommodation`/`InvoluntaryChange` reserved and unreachable; execute only what an authority already decided | a disruption decision, a revalidate-vs-reissue choice, or any involuntary economics |
| DCS / usage (no-show) authority | departure control | infer nothing; keep `NoShow` unwritten | a no-show state, a penalty, or coupon expiry after departure |
| Authoritative SSR interpretation | outside Ordering | keep remarks as non-authoritative free text | SSR-derived eligibility, control or no-show |
| Real provider adapters for every P3 rail | each provider | deterministic adapters plus fail-closed unconfigured providers (22 × 501) | a provider result |

---

## 26. Final P3-B..H audit

| Area | Result |
| --- | --- |
| Operation lifecycle | stable identity; transitions correct; the four states stay distinct; no `Completed` with mandatory unresolved work; live/dead values documented honestly (§14) |
| Accountable documents | ETKT/EMD truth never rolled back after confirmation; lineage exact; void/refund/exchange/revalidation exact-once; replay duplicates nothing (R36–R38) |
| External mutation safety | stable keys; no blind retry; recover-first on uncertainty; `Confirmed` monotonic at plan and evidence layers; throw-before/after safe; parallel workers safe (R39) |
| Pricing / value | no Ordering-invented monetary amount — the P3-H diff contains no money arithmetic; source provenance exact; no involuntary pricing policy (R28) |
| Ancillary servicing | G1–G5 semantics unchanged; `ManualReview` still explicit and still gates completion; G6 remains source-instructed; no wallet |
| Operational facts | `ControlStatus` not inferred; `NoShow` not inferred; free text not authority; no Inventory/DCS fact invented |
| Integration | 22 unconfigured adapters fail closed; catalog current at 21 entries; no new port, so no port lacks an ICC; every block explicit |
| Scope | no DCS integration; no disruption recovery; no no-show flow; no reaccommodation engine; no P4 leakage |
| Architecture | all three new enums in `Contracts/AeroTech.Messages/Ordering/Enums/` with `[Display]`; both queries in the Query project with MediatR; read path is thin projection, not write-side repositories; plural table names; one type per file; no code comments; no hardcoded config; exception codes contiguous 20001–20332 |

---

## 27. Freeze verdict

```text
P3-H READY TO FREEZE: YES
P3 READY TO FREEZE:   YES
```

P4 was not started and no P4 handoff was created.
