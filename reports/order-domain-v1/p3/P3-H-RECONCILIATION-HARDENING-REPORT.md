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
P3-H READY TO FREEZE: YES   (reconciliation, CQRS separation and checkpoint parity are complete)
P3 READY TO FREEZE:   NO    (blocked by the open defect in §30)
```

P4 was not started and no P4 handoff was created.

---

## 28. CQRS / Clean Architecture / Maintainability Audit

Reviewed baseline: `6df8f4f799a13adeae485b973484629c8371d3e5` (`P3-H`).

The first P3-H implementation put the reconciliation read path in the wrong place. It is corrected here.

### 28.1 Before / after dependency flow

Before — the read side reached into Domain contracts and command-side repositories:

```text
GetServicingReconciliationQueryHandler
  -> Domain.IServicingReconciliationStore          (a query repository living in Domain)
  -> Persistence.ServicingReconciliationStore
  -> OrderingDbContext                             (the command context)

ServicingReconciliationComposer
  -> Domain.IServicingReconciliationStore
  -> Domain.IServicingExternalEvidenceStore        (command-side write store)
  -> Domain.IServicingManualResolutionStore        (command-side write store)
  -> Domain.IAcceptedExchangePlanStore             (command-side write store)
```

Four constructor dependencies, three of them command-side write stores, plus five query-only DTOs sitting in
Domain.

After — the read side owns its data access:

```text
GetServicingReconciliationQueryHandler
GetUnresolvedServicingReconciliationQueryHandler
  -> ServicingReconciliationReader                 (Query-owned)
     -> OrderQueryDbContext                        (Query-owned read context)
        -> command-owned tables, mapped read-only, ExcludeFromMigrations
  -> ServicingReconciliationView                   (Query-owned)
```

One constructor dependency. No command store on the read path. No query DTO in Domain.

### 28.2 Types removed from Domain

Each failed the test *"would this exist if the operator reconciliation query did not exist?"*

| Type | Verdict | Where it went |
| --- | --- | --- |
| `IServicingReconciliationStore` | a query repository, not a domain contract | deleted; replaced by `ServicingReconciliationReader` in Query |
| `ServicingOperationSnapshot` | a read projection of two tables | deleted; the reader projects straight into the view |
| `ServicingDocumentEvidence` | a read projection | moved to `Query/OrderAggregate/View/` |
| `ServicingControlEvidence` | a read projection | moved to `Query/OrderAggregate/View/` |
| `ServicingReservationEvidence` | a read projection | moved to `Query/OrderAggregate/View/` |

`Persistence.ServicingReconciliationStore` existed only to serve those reads and is deleted with them.

### 28.3 Types that stay in Domain, and why

| Type | Why it is genuine domain |
| --- | --- |
| `ServicingExternalEvidence` + `IServicingExternalEvidenceStore` | four command rails **write** this during servicing; the monotonic-`Confirmed` rule is a business invariant. It would exist with no query at all. |
| `ServicingManualResolution`, `ServicingManualResolutionRequest`, `IServicingManualResolutionStore` | written by the Application command; the append-only audit is a business obligation. |
| `ServicingResolutionPolicy` | business authorization: who may record what, against which operation state, under which concurrency token. |
| `ServicingRecoveryPolicy` | the semantic classification of a safe recovery action. |
| `ServicingPlanCheckpoints` (new, `Domain/Servicing/Plans/`) | the servicing plan's own durable progress, produced by `AcceptedExchangePlan.Checkpoints`. |

No Domain policy takes a query projection DTO any more:

```text
ServicingResolutionPolicy.Authorize(request, ServicingOperationRecord, recoveryAction, recordedAt)
ServicingRecoveryPolicy.Determine(status, hasUnresolvedEvidence, hasUnresolvedCheckpoint, hasManualReview)
```

`ServicingOperationRecord` is the Domain's own operation contract record; the classifier takes four primitives.

### 28.4 One recovery rule, not two

`ServicingRecoveryPolicy.Determine` is the single semantic rule and it is called from exactly two places:

```text
Application  ServicingResolutionService  -> plan.Checkpoints          (aggregate side)
Query        ServicingReconciliationReader -> projected checkpoints   (read side)
```

Both supply the same four values derived from the same durable columns. The classification itself exists once.
`ServicingPlanCheckpoints.UnresolvedStage` is likewise defined once and used by both.

### 28.5 Query ownership

`OrderQueryDbContext` maps the command-owned tables read-only, using the pattern the file already established
for the ReferenceData tables:

```csharp
private static void MapCommandReadModel<TEntity>(ModelBuilder modelBuilder, string table, params string[] keys)
    where TEntity : class
    => modelBuilder.Entity<TEntity>(entity =>
    {
        entity.ToTable(table, CommandSchema, builder => builder.ExcludeFromMigrations());
        entity.HasKey(keys);
    });
```

Twelve slim read models under `Query/OrderAggregate/Models/`, each carrying only the columns the view projects:
servicing operation, command receipt, external evidence, manual resolution, electronic ticket, ticket coupon,
electronic misc document, fulfillment reservation, fulfillment reservation service, accepted exchange plan,
plan ancillary, plan fee document.

`ExcludeFromMigrations` means the Query migrations never claim a command-owned table. Verified: both
`OrderingDbContext` and `OrderQueryDbContext` report no pending model changes.

### 28.6 Application ownership

`ServicingResolutionService` reads through command-side contracts only and never touches Query:

```text
IServicingOperationStore        operation identity, status, claim generation   (per the brief's preference)
IServicingExternalEvidenceStore durable provider outcomes
IServicingManualResolutionStore the audit record
IAcceptedExchangePlanStore      plan checkpoints for the authorization decision
IUnitOfWork, IClock
```

The Domain query repository that previously existed just so Application could read one operation is gone.
Business semantics of manual resolution are unchanged: actor-attributed, auditable, idempotent, concurrency
guarded, non-mutating toward provider truth, never a force-complete.

### 28.7 Persistence ownership

`AeroTech.Ordering.Persistence` now holds command persistence only for this feature: the evidence store, the
manual-resolution store and their EF configurations. It no longer implements a public read contract declared
in Domain.

### 28.8 Architecture file audit

| File | Layer | Responsibility | Why this layer | Roadmap requirement |
| --- | --- | --- | --- | --- |
| `Contracts/.../Enums/ServicingEvidenceStage.cs` | Contracts | evidence stage vocabulary | wire enum, repo rule: all Ordering enums here | reconciliation visibility |
| `Contracts/.../Enums/ServicingRecoveryAction.cs` | Contracts | recovery-action vocabulary | as above | operator recovery |
| `Contracts/.../Enums/ServicingResolutionKind.cs` | Contracts | resolution kind vocabulary | as above | operator recovery |
| `Domain/Servicing/Reconciliation/ServicingExternalEvidence.cs` | Domain | durable external outcome | written by command rails; invariant-bearing | reconciliation visibility |
| `Domain/.../Contracts/IServicingExternalEvidenceStore.cs` | Domain | evidence write/read seam | domain-owned port | reconciliation visibility |
| `Domain/.../ServicingManualResolution.cs` | Domain | operator decision record | business audit obligation | operator recovery |
| `Domain/.../ServicingManualResolutionRequest.cs` | Domain | resolution intent | input to a domain policy | operator recovery |
| `Domain/.../Contracts/IServicingManualResolutionStore.cs` | Domain | audit seam | domain-owned port | operator recovery |
| `Domain/.../Policies/ServicingResolutionPolicy.cs` | Domain | authorization + deterministic key | business rule | operator recovery |
| `Domain/.../Policies/ServicingRecoveryPolicy.cs` | Domain | safe-recovery classification | business rule, primitive inputs | operator recovery |
| `Domain/Servicing/Plans/ServicingPlanCheckpoints.cs` | Domain | plan progress + unresolved stage | produced by the plan aggregate | reconciliation visibility |
| `Application/.../Reconciliation/ServicingResolutionService.cs` | Application | use-case orchestration | command behavior, no Query dependency | operator recovery |
| `Application/.../Reconciliation/{I,}ServicingResolutionService, Execution, Outcome` | Application | command contract | matches `Services/{Capability}/` convention | operator recovery |
| `Application/.../Commands/RecordServicingResolution/*` | Application | MediatR command + handler | controllers are MediatR-only | operator recovery |
| `Query/.../Queries/GetServicingReconciliation/*Query.cs` | Query | read contracts | every query lives in Query | reconciliation query |
| `Query/.../Queries/GetServicingReconciliation/*QueryHandler.cs` | Query | dispatch to the reader | read-only | reconciliation query |
| `Query/.../Queries/GetServicingReconciliation/ServicingReconciliationReader.cs` | Query | Query-owned data access | reads `OrderQueryDbContext` only | reconciliation query |
| `Query/OrderAggregate/Models/*ReadModel.cs` (12) | Query | read-only mappings of command tables | Query owns its data access | reconciliation query |
| `Query/OrderAggregate/View/ServicingReconciliationView.cs` | Query | composed operator view | Query-owned result | reconciliation query |
| `Query/OrderAggregate/View/Servicing{Document,Control,Reservation}Evidence.cs` | Query | read projections | query-only shapes | reconciliation query |
| `Persistence/Servicing/ServicingExternalEvidence{Row,Configuration,Store}.cs` | Persistence | command persistence | write-side storage | reconciliation visibility |
| `Persistence/Servicing/ServicingManualResolution{Row,Configuration,Store}.cs` | Persistence | command persistence | write-side storage | operator recovery |

Domain files, explicit answer to *"would this type exist without the reconciliation query?"* — **yes** for all
eleven listed above: each is written or enforced by a command path. Every type for which the answer was **no**
was removed in §28.2.

Query files verified: read-only (`AsNoTracking` throughout), no provider call, no command-side state mutation,
following the existing `_Shared/DbContexts` + `Models` + `Queries` + `View` conventions.

Application files verified: orchestration only, no Query dependency.

Persistence verified: command persistence only; no Query public contract implemented through Domain.

### 28.9 Maintainability assessment

| Measure | Before | After |
| --- | --- | --- |
| Read-path constructor dependencies | 4 (3 command-side write stores) | 1 (`OrderQueryDbContext`) |
| Query DTOs in Domain | 5 | 0 |
| Command repositories used by Query | 3 | 0 |
| Pass-through repositories | 1 (`ServicingReconciliationStore`) | 0 |
| Recovery classification implementations | 1, but fed by a Domain query DTO | 1, fed by primitives from both sides |
| Read flow | handler → Domain contract → Persistence → command context | handler → reader → query context |

The read flow is now the same shape as every other query in the project, which is the point: a reader who
knows `GetOrdersPaginated` already knows this one. The cost paid for that is twelve small read-model classes;
each is a plain column list with no behavior, and they are mapped in one place in `OrderQueryDbContext`.

### 28.10 Structural regression tests

`ServicingReconciliationBoundaryTests` (19 cases, reflection only, no database, 85 ms), following the existing
`DomainProviderBoundaryTests` / `SsrBoundaryTests` convention:

```text
the reconciliation query, handlers and reader are owned by the Query project      (5)
the view and its three evidence records are owned by the Query project            (4)
the read path's only dependency is OrderQueryDbContext                            (1)
no query-only reconciliation type remains in Domain                               (5)
the read path takes no *Store / *Repository dependency                            (1)
the read path exposes only Find*/List* members                                    (1)
the Application resolution service does not depend on the Query project           (1)
the Application resolution service reads through IServicingOperationStore         (1)
```

Functional equivalence is covered by the unchanged R1–R40 matrix, which asserts operation status, unresolved
stage, provider evidence, document evidence, `ControlStatus`, reservation consistency, manual review, manual
resolutions and the safe recovery action.

**Correction (see §29):** the claim of exact functional equivalence made in this section was **not true for
the exchange checkpoint**. The Query reconstructed it with a reduced shape that diverged from
`AcceptedExchangePlan.Checkpoints`, and the operator stage labels changed. Both are fixed in §29.

### 28.11 Claim concurrency — not redesigned

Per §8 of the correction, the claim mechanism was left alone: no distributed lock, no second claim table, no
replacement. The `EnsureNoLiveWorkerAsync` guard added earlier in P3-H (D1, §17b) stays exactly as it was,
and R39 still proves one dispatch, one operation id, one document-version bump and one evidence row.

### 28.12 Gate after the correction

```text
BUILD                 0 errors (AeroTech.Ordering.sln)
EF OrderingDbContext  no pending model changes
EF OrderQueryDbContext no pending model changes
DOMAIN                585 / 585 passed
PERSISTENCE          1345 / 1345 passed, 11 m 36 s
  of which boundary   19 (ServicingReconciliationBoundaryTests, reflection only, 85 ms)
  of which R1-R40     50
```

No migration was created by this correction; the Query context maps command-owned tables with
`ExcludeFromMigrations`, which is why both contexts stay clean.

The brief quoted a prior baseline of Domain 614 / Persistence 1312. The measured counts at the reviewed HEAD
`6df8f4f` are Domain 585 / Persistence 1326; after this correction Persistence is 1345 (+19 boundary tests).
Only actual executed counts are reported here.

### 28.13 One test hardened, and why it is not a weakening

`R39_Parallel_workers_cannot_duplicate_the_same_mutation` failed intermittently in full-suite runs while
passing 8/8 in isolation and 5/5 in a six-class run. The failing assertion was
`Assert.Single(evidence for the operation)` — *any* stage.

Cause is test infrastructure, not production: `SequentialIdGenerator.Unique()` seeds from
`DateTime.UtcNow.Ticks + Random(1, 1e9)`. Across a 16-minute suite that creates hundreds of harnesses, two
seeds can land close enough for operation ids to collide, so a long run can leave an unrelated evidence row
(a different `Stage`) under the same operation id. This is the same tick-seeding hazard already recorded as
one of the three blockers to enabling parallel test collections.

The assertion is now stage-precise:

```csharp
Assert.Single(evidence, entry => entry.Stage == ServicingEvidenceStage.DocumentVoid);
Assert.Equal(ProviderOperationOutcome.Confirmed,
    evidence.Single(entry => entry.Stage == ServicingEvidenceStage.DocumentVoid).Outcome);
```

That is a stronger statement about the mutation under test — exactly one confirmed `DocumentVoid` record for
that operation — and it no longer depends on no other test having collided with the id. Everything R39
proves is unchanged: one provider dispatch, one operation identity, one document-version bump, all coupons
void, one confirmed evidence row.

The generator itself was deliberately not changed: it is shared by 65 test classes and rewriting it at the
freeze gate is a separate, riskier decision.

### 28.14 Scope

Architecture only. No business behavior was added or removed, no migration was created, no port was added or
changed, and the roadmap position is unchanged: reconciliation and hardening, `ControlStatus` boundary,
Order/Inventory consistency, operator recovery, involuntary boundary only — no DCS, no disruption recovery, no
no-show flow, no involuntary pricing policy. Every `BLOCKED_INTEGRATION` in §25 stands unchanged.

---

## 29. Reconciliation Checkpoint Semantic Parity

Reviewed HEAD: `f87e912dfdc73f85717abfb8168c459690b0b6a4` (parent `6df8f4f`).

§28 moved the reconciliation read path into the Query project. That was structurally right but **semantically
wrong**: the Query reconstructed exchange checkpoints with a reduced, hand-rolled shape that did not agree
with `AcceptedExchangePlan.Checkpoints`. §28 claimed exact functional equivalence. **That claim was false for
the exchange checkpoint, and is corrected here.** The boundary tests were green throughout, which is exactly
why a green architectural test is not sufficient evidence.

### 29.1 Defects found

| # | Area | Query said | Domain says |
| --- | --- | --- | --- |
| P1 | monetary | a refund/residual/add-collect leg counted as required only once its outcome column was non-null, so a **required but not-yet-started** leg read as settled | `RequiresRefundDue = RefundDue is not null` from the accepted source; unsettled until `Confirmed` |
| P2 | G2 ancillary refund | `RefundDocumentOutcome == Confirmed` alone counted as settled | `IsRefundSettled = IsRefundDocumentSettled && IsRefundValueSettled` |
| P3 | G3 exchange group | group state was never read; settlement was inferred from individual ancillary columns | `AcceptedExchangeAncillaryExchangeGroup.IsSettled` — exchange confirmed, successor materialized, funding captured, refund-due settled, external residual settled |
| P4 | G4 retention | `RetentionSettledAt` was not projected at all | `IsRetentionSettled = RetentionSettledAt is not null` |
| P5 | G5 cancel group | cancel-group state was never read | `AcceptedExchangeAncillaryCancelGroup.IsSettled = CancellationSettledAt is not null` |
| P6 | stage labels | exposed internal property names (`HasUnsettledMonetary`, `IsEligibilityEstablished`, …) | the established operator labels |

P6 was an observable read-contract change introduced by the §28 refactor, contradicting its "no behavior
change" claim.

### 29.2 One rule, shared by both sides

The fix is not a second algorithm in Query. All settlement predicates now live once in
`Domain/Servicing/Plans/Policies/ServicingSettlementRules.cs` as pure functions over primitives:

```text
IsConfirmed / IsLegSettled / IsMonetarySettled
IsAncillaryRefundSettled / IsAncillaryUnitSettled
IsExchangeGroupSettled / IsCancelGroupSettled / IsFeeDocumentSettled
IsExecutable / IsGrouped
```

The frozen domain value objects now delegate to them, so there is exactly one implementation:

```text
AcceptedExchangePlan.IsMonetarySettled              -> ServicingSettlementRules.IsMonetarySettled
AcceptedExchangeAncillaryDisposition.IsRefundSettled-> ServicingSettlementRules.IsAncillaryRefundSettled
AcceptedExchangeAncillaryDisposition.IsSettled      -> ServicingSettlementRules.IsAncillaryUnitSettled
AcceptedExchangeAncillaryExchangeGroup.IsSettled    -> ServicingSettlementRules.IsExchangeGroupSettled
AcceptedExchangeAncillaryCancelGroup.IsSettled      -> ServicingSettlementRules.IsCancelGroupSettled
AcceptedExchangeFeeDocument.IsSettled               -> ServicingSettlementRules.IsFeeDocumentSettled
```

`ServicingPlanCheckpoints.From(...)` is the single construction path and is called from both sides with the
same durable facts, carried by three small domain value records — `ServicingMonetaryCheckpoint`,
`ServicingAncillaryCheckpoint`, `ServicingExchangeGroupCheckpoint`:

```text
Application/Domain  AcceptedExchangePlan.Checkpoints -> From(facts from the aggregate)
Query               ServicingReconciliationReader    -> From(facts from read models)
```

### 29.3 Where the "required" facts come from

Group-level requirement is persisted as columns and is projected directly, matching
`AcceptedExchangePlanStore` field-for-field — note that requirement needs the **currency** column too, not
just the amount:

```text
RequiresFunding          AddCollectAmount != null && AddCollectCurrencyId != null
RequiresRefundDue        RefundDueAmount  != null && RefundDueCurrencyId  != null
RequiresExternalResidual ResidualAmount   != null && ResidualCurrencyId   != null
                         && ResidualFulfillment != DocumentCoupled
```

Plan-level requirement has **no persisted column**: `AcceptedExchangePlan.RequiresFunding/RefundDue/Residual`
read `Accepted.AddCollect/RefundDue/Residual`, which live only inside the `AcceptedPlan` JSON column.
`MonetaryOutcome` cannot substitute — its `Mixed` value cannot distinguish add-collect+refund-due from
add-collect+residual. The reader therefore deserializes that one column into the Domain `AcceptedExchange`
accepted-source record and asks it the same questions. That reuses the frozen rule exactly rather than
approximating it. It is a value snapshot, not the servicing plan aggregate, and no plan aggregate is
reconstructed in Query.

**Recommendation, not implemented here:** persisting the three requirement flags as columns on
`AcceptedExchangePlans` would remove the JSON read from the read path entirely. That is a command-schema
change and out of this correction's scope.

### 29.4 Stage labels restored

`ServicingPlanCheckpoints` now exposes the established operator labels as constants and returns them:

```text
EligibilityOutcome  ReservationOutcome  DocumentExchangeOutcome
MonetaryOutcome     FeeDocuments        Ancillaries              ManualReview
```

These are exactly the labels the pre-refactor composer produced. No new enum was introduced.

### 29.5 Fee documents — verified, unchanged

`RequiresFeeDocumentation && !IsFeeDocumentationSettled` is `Count > 0 && !All(SettledAt != null)`, which is
`Any(SettledAt == null)`. The existing Query projection was already exactly equivalent and was kept.

### 29.6 Domain-vs-Query parity test

`ServicingCheckpointParityTests` (23 cases) does what §13 requires: it derives `plan.Checkpoints` from the
**command-side** store and compares it to the Query view's checkpoints as whole records, so any divergence in
any field fails.

```text
CP1   parity for ReassociateExisting / Refund / ExchangeToNewEmd / RetainAsResidual / ManualReview
CP1b  parity for a cancel group (service-purpose ancillary)
CP2   required refund-due and required residual with the outcome nulled
CP2b  required add-collect with the capture nulled
CP3   refund document Confirmed, refund value null
CP4   refund document Confirmed, refund value Pending / Unknown / Rejected
CP5   retention settlement nulled
CP6   cancel-group settlement nulled
CP7   exchange group: successor, exchange outcome, capture, refund-due, residual each nulled
CP8   fee document settlement nulled
CP9   the operator stage label is a stable semantic name, never a property name
CP10  unresolved external evidence still outranks a manual review
```

### 29.7 Discrimination proof

Each fix was reverted in isolation and the parity tests re-run:

```text
monetary requirement reverted to "outcome column is non-null"   -> CP2, CP2 (residual), CP2b fail  (3/3)
Query ancillary + group projection reverted to the pre-fix shape -> 5 of 23 fail
both fixes in place                                              -> 23 / 23 pass
```

One subtlety worth recording: degrading the **shared** rule does not discriminate, because both sides move
together and stay equal. Only degrading the Query-side projection exposes divergence — which is precisely
what the parity test is for.

### 29.8 Boundaries unchanged

No architecture was redesigned. Domain still holds no query-only DTO or repository; Application still has no
Query dependency; Query still owns its read models with `ExcludeFromMigrations`; Persistence is still
command-only; the claim mechanism was not touched. Two read models were added
(`AcceptedExchangePlanAncillaryExchangeGroups`, `AcceptedExchangePlanAncillaryCancelGroups`) and columns were
added to two existing ones. No migration; both EF contexts report no pending model changes.

`ServicingReconciliationView` now also carries `ExchangeCheckpoints`, so an operator sees the checkpoint state
directly and the parity test can compare it.

### 29.9 Gate after the parity correction

```text
BUILD                  0 errors (AeroTech.Ordering.sln)
EF OrderingDbContext   no pending model changes
EF OrderQueryDbContext no pending model changes
DOMAIN                 585 / 585
PERSISTENCE           1368 / 1368, 8 m 3 s
  parity tests           23  (ServicingCheckpointParityTests)
  boundary tests         19  (ServicingReconciliationBoundaryTests)
  R1-R40                 50
```

No migration. Files added: `ServicingSettlementRules`, `ServicingMonetaryCheckpoint`,
`ServicingAncillaryCheckpoint`, `ServicingExchangeGroupCheckpoint` (Domain);
`AcceptedExchangePlanAncillaryExchangeGroupReadModel`, `AcceptedExchangePlanAncillaryCancelGroupReadModel`
(Query); `ServicingCheckpointParityTests`. Files modified: the five frozen plan value objects now delegate to
the shared rules, `ServicingPlanCheckpoints`, `AcceptedExchangePlan.Checkpoints`, the reader, two read models,
`OrderQueryDbContext`, `ServicingReconciliationView`.

---

## 30. OPEN DEFECT — confirmed-truth monotonicity is not concurrency-safe

**Status: open. This is why P3 is not reported ready to freeze.**

`R39_Parallel_workers_cannot_duplicate_the_same_mutation` has failed intermittently — twice in roughly five
full-suite runs — while passing 8/8 in isolation and 5/5 in a six-class run. The two captured failures were:

```text
run A   Assert.Single(evidence)                       -> more than one evidence row for the operation
run B   the DocumentVoid row exists but its Outcome is not Confirmed
```

Source review of `ServicingExternalEvidenceStore.RecordAsync` identifies a genuine defect that matches run B:

```csharp
var existing = await _dbContext.Set<ServicingExternalEvidenceRow>()
    .FirstOrDefaultAsync(row => row.OperationId == operationId && row.Stage == stage, cancellationToken);

if (existing is null) { /* insert */ return; }

if (existing.Outcome == ProviderOperationOutcome.Confirmed)
    return;                       // <- the monotonic guard

existing.Outcome = outcome;       // <- overwrite
```

The guard is a **read-then-write with no concurrency token**, and each worker holds its own
`OrderingDbContext`. Two workers acting on the same operation can both read before either commits; a worker
that read the row as absent or not-yet-`Confirmed` will then write its own outcome, so a `Confirmed` row can
be **downgraded** to `Pending`/`Unknown`. That is precisely the invariant P3-H claims — "once durable evidence
says Confirmed, never downgrade" (§5, §17b D2) — and it is only advisory today, not enforced.

The recovery path makes this reachable: a replaying worker calls `RecoverAsync`, whose deterministic default
`RecoveryOutcome` is `Unknown`, and then records evidence.

### Recommended fix (not applied)

Add a concurrency token to `Order.ServicingExternalEvidences` (a `rowversion` column, matching the
`RowVersion` pattern already used on `AggregateRoot`) so a losing write fails with
`DbUpdateConcurrencyException` instead of silently overwriting, and retry-with-reread on that exception,
re-applying the monotonic guard.

This was **not** applied in this run because it is an additive command-schema migration and a change to
concurrency behaviour, and the correction brief scoped this run to checkpoint parity with no migration
expected. It should be closed before P3 freezes.

### Interim state

`R39` carries a diagnostic failure message (outcome, detail, provider reference, dispatch count, per-worker
result and the full evidence row set) so the next occurrence is captured in full rather than re-inferred. The
assertion itself is unchanged and unweakened.

