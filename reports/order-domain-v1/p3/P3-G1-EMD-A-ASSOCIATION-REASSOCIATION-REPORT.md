# P3-G1 — EMD-A Association Lifecycle and Reissue Reassociation

Opening report for P3-G, the ancillary servicing capability.

Companion documents, all in this folder:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, now carrying `ICC-P3-ANCILLARY-EXCHANGE-DISPOSITION` and `ICC-P3-EMD-ASSOCIATION`.
* [P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md](P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md) — the frozen P3-F baseline this builds on.

---

## 1. Starting State

```text
HEAD at start   4ae59e06c4f04fdb7f69335d189f25d5069c5eb8
Commit          P3-F FINAL — Mixed / Multi-Leg + Full P3-F Freeze
Working tree    clean
```

Inherited frozen baseline: fully-unused, multi-coupon and partially-used exchange; repeated A→B→C lineage;
Even, AddCollect, Refund-Due, Residual and Mixed settlement; the accepted-plan rail with per-stage durable
evidence; recover-first with `WasDispatched` on every provider rail; and the integration capability catalog.

**Correction to the P3-F closing report.** That report stated that no EMD aggregate exists. It does. The
following was already present and was built on rather than recreated:

| Existing asset | Location |
| --- | --- |
| `ElectronicMiscDocument` aggregate root, `Issue`, `Void`, `EnsureCanBeVoided`, `DocumentsService` | `src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/` |
| `EmdCoupon` entity with `AssociatedTicketCouponId`, `Purpose`, `ReasonForIssuanceSubCode`, `Status` | `.../ElectronicMiscDocumentAggregate/Entities/EmdCoupon.cs` |
| `EmdCouponStatus`, `ElectronicMiscDocumentType`, `EmdCouponPurpose`, `ElectronicMiscDocumentStatus` | `Contracts/AeroTech.Messages/Ordering/Enums/` |
| EMD issuance rail (`IEmdIssuancePort`, `DeterministicEmdIssuanceAdapter`) and EMD void lifecycle | Application / Providers |
| `IElectronicMiscDocumentRepository` with `ListByOrderAsync`, `GetAsync`, `AddAsync` | `.../Contracts/` |

The one thing the EMD aggregate had **no** concept of was the *lifecycle* of an association: the coupon knew
which ticket coupon it pointed at, but nothing recorded where it came from, when it moved, on whose authority,
or against which provider evidence. That is what this revision adds.

---

## 2. The Guard That Was Replaced

`ExchangePreconditions.EnsureNoAssociatedMiscDocumentAsync` refused the whole exchange whenever any
non-voided miscellaneous document had a coupon pointing at a coupon in the reissue scope
(`ExchangeBlockedByAssociatedMiscDocument`, 20272).

It was **not deleted**. It was replaced by a scope calculation of the same shape,
`ExchangePreconditions.AffectedAncillariesAsync`, which returns the affected associations instead of throwing
on the first one, and the refusal moved from "an ancillary exists" to "an ancillary exists without an
authoritative disposition".

The resulting three-way gate:

| Situation | Behaviour |
| --- | --- |
| no affected EMD-A | the P3-F flow is byte-for-byte unchanged; neither ancillary port is consulted at all |
| affected EMD-A, every coupon carries an accepted executable disposition | the exchange proceeds, with a reassociation stage before local finalization |
| affected EMD-A, any disposition missing, ambiguous, malformed or unsupported | fail closed **before** the first irreversible exchange operation |

`ExceptionFactory.ExchangeBlockedByAssociatedMiscDocument` (20272) is retained but is now unreachable. It is
kept so the exception numbering stays contiguous inside 20000–29999; it is listed as a known gap in
`ICC-P3-EMD-ASSOCIATION` so it is not silently forgotten.

---

## 3. What Makes an Ancillary Affected

Computed mechanically by `ElectronicMiscDocument.CouponsAssociatedWith`, with no inference:

```text
document.Type == Associated
&& document.StatusSummary != Voided
&& coupon.Status == OpenForUse
&& coupon.AssociatedTicketCouponId ∈ { Open coupons of the resolved accountable predecessor }
```

The reissue scope is the predecessor's `Open` coupons, exactly as P3-F froze it. A `Used` coupon is historical
pricing context and an ancillary attached to one is therefore **not** affected. The decision to keep that rule
identical is deliberate: the affected ancillary scope must never be wider than the document mutation scope.

Ordering never widens the scope to a standalone EMD, a voided document, a voided coupon, or an ancillary on
another accountable document of the same order.

---

## 4. The Authoritative Disposition

`IAncillaryExchangeDispositionPort` — a new Domain port under `Domain/Ports/AncillaryDisposition/`.

It is a new port rather than an extension of `IExchangeQuotePort` because no per-ancillary extension point
existed on the accepted-exchange contract, and because the acceptance of a priced exchange and the disposition
of an ancillary attached to it are two different authoritative answers. The port is **side-effect-free**: one
`DecideAsync`, no operation key, no recovery, no `WasDispatched`. That is not an omission — a lookup that
mutates nothing needs none of them.

`AncillaryExchangeDisposition` (new, in `Contracts/AeroTech.Messages/Ordering/Enums/`):

| Value | This revision |
| --- | --- |
| `ReassociateExisting` | **executed** |
| `Refund` | recognised, recorded, refused with 20298 |
| `ExchangeToNewEmd` | recognised, recorded, refused with 20298 |
| `RetainAsResidual` | recognised, recorded, refused with 20298 |
| `Cancel` | recognised, recorded, refused with 20298 |
| `ManualReview` | recognised, recorded, refused with 20298 |

The target coupon is expressed as `TargetPredecessorCouponNumber`. The successor document number and its
coupon numbers do not exist when the decision is obtained; the predecessor coupon number is resolvable at
acceptance time and maps deterministically through the accepted plan to the pre-minted successor coupon
identity. That mapping is done once, at acceptance, and stored.

---

## 5. Structural Validation — Fail Closed

`ExchangeAncillaryPlanner` is the single place a decision is judged. All of it happens before
`AcceptedExchangePlanStore.SaveAsync` and before `order.PrepareExchange`, so nothing has been persisted and
nothing has been mutated when any of these fire:

| Condition | Code | HTTP |
| --- | --- | --- |
| an affected coupon has no decision | 20296 `AncillaryDispositionMissing` | 422 |
| two decisions name the same coupon | 20297 `AncillaryDispositionMalformed` | 422 |
| a decision names an ancillary outside the affected scope | 20297 | 422 |
| a decision names another predecessor document | 20297 | 422 |
| a decision names another predecessor coupon | 20297 | 422 |
| `ReassociateExisting` carries no target coupon | 20297 | 422 |
| the target is outside the accepted successor scope | 20297 | 422 |
| the target is a `Used` coupon | 20297 | 422 |
| the decision carries no reference of its own | 20297 | 422 |
| the disposition is not `ReassociateExisting` | 20298 `AncillaryDispositionNotExecutable` | 422 |

Every one of them is recorded through the existing `TryRecordRejectionAsync` rail, so the accepted plan is
stored as `AcceptedExchangeDisposition.Rejected` and the **same code and HTTP status replay terminally** under
the same command receipt. No affected ancillary is ever carried forward silently, and no disposition is ever
substituted for another.

An unreachable or unconfigured disposition source is handled differently on purpose: the servicing operation
is left `AwaitingExternal`, **no plan is persisted**, nothing is mutated, and the caller sees the original
failure. That is a configuration or transport problem, not a business rejection of this exchange, so it stays
retryable from the start rather than becoming a durable refusal.

---

## 6. The Accepted Ancillary Plan

`AcceptedExchangeAncillaryDisposition` (Domain, `Servicing/Plans/`) is immutable accepted evidence, one record
per affected coupon:

```text
ElectronicMiscDocumentId, EmdDocumentNumber, EmdCouponNumber, EmdCouponId,
PredecessorTicketCouponId, PredecessorDocumentNumber, PredecessorCouponNumber,
Disposition, TargetPredecessorCouponNumber, TargetSuccessorTicketCouponId,
DecisionReference, DecisionVersion,
AssociationOutcome, AssociationProviderReference, AssociationDetail
```

`AcceptedExchangePlan` gained one optional parameter, `AncillaryDispositions`, plus provider-neutral readings:
`Ancillaries`, `Reassociations`, `RequiresAncillaryReassociation`, `IsAncillarySettled`,
`HasRejectedAncillary` and `AncillaryState`. The aggregation order of `AncillaryState` is
`Rejected > Pending > NotStarted > Confirmed`, matching how `MonetaryState` already aggregates legs.

Each disposition exposes `LegIdentity` as `emd-reassociate:{document}:{coupon}`, which is what the stable
operation key is derived from.

---

## 7. The Reassociation Stage

`IEmdAssociationPort` — `ReassociateAsync` plus `RecoverReassociationAsync`, the same two-method durable rail
shape as every other P3 provider boundary.

Stable operation identity, one per affected coupon:

```text
emd-reassociate:{emdDocumentNumber}:{emdCouponNumber}:{operationId}
```

derived internally by `OrderOperationCoordinator.ProviderOperationKey` before first dispatch. One key per
coupon, not one per exchange, so a multi-coupon exchange has one independently recoverable durable operation
per coupon and partial completion is representable rather than lost.

The request crosses the boundary in accountable-document terms only: EMD document number and coupon number,
predecessor document number and coupon number, successor document number and coupon number, the beneficiary
traveller id, the issuing carrier id and the decision reference. No EMD coupon id, no ticket coupon id and no
order service id leave Ordering.

**Evidence validation.** `ExchangeSettlementEvidencePolicy.ReassociationContradiction` is the single place a
confirmation is judged. A `Confirmed` result is contradictory when it carries no provider reference, or when
it names a different EMD document, a different EMD coupon, a different associated document or a different
associated coupon than the plan asked for. A contradiction persists into `AssociationDetail`, moves the
operation to `NeedsReconciliation`, leaves the local association untouched and never retries the move. Echoed
fields are treated as optional, but each one that is present is verified.

**Pre-dispatch admissibility.** Before dispatching, the stage loads the EMD aggregate and asks
`ElectronicMiscDocument.PermitsReassociation`. If the document has since been voided, the coupon voided, or
the association moved elsewhere, the operation goes to `NeedsReconciliation` rather than dispatching a move
that could not be applied locally afterwards.

---

## 8. Where the Stage Sits

```text
quote
  -> accept
  -> obtain ancillary dispositions          (side-effect-free)
  -> validate and accept them               (fail closed here)
  -> persist the complete accepted plan
  -> eligibility
  -> funding guarantee                      (AddCollect / Mixed)
  -> inventory change
  -> document exchange
  -> monetary settlement                    (capture, refund-due, residual)
  -> EMD-A reassociation                    (per coupon, ordinal by document then coupon)
  -> local finalization                     (successor ticket + local association move + completion)
```

Two orderings are load-bearing.

**The disposition lookup is before every irreversible step.** That is what makes 20296, 20297 and 20298 safe:
no inventory has moved, no document has been exchanged, no money has moved, no successor exists.

**The local association move is the last thing, inside the finalizing transaction.** The local
`ReassociateCoupon` runs in the same unit of work that mints the successor ticket. Until the successor exists,
the ancillary still points at the predecessor coupon, which is the truthful state while the reissue may still
fail. The local association follows the authoritative one; it never leads it.

The stage is also strictly after all monetary settlement. An ancillary is not moved onto a successor document
whose money has not settled, and a successor document is not created locally while an ancillary it must carry
is unresolved.

No new `ServicingOperationKind` was appended. The reassociation is a stage of the Exchange operation, so it
inherits the Exchange claim, receipt, replay and reconciliation semantics unchanged.

---

## 9. Recovery and Crash Semantics

Recover-first is threaded exactly as P3-F froze it. The stage dispatches fresh only when its prerequisite
confirmed in the same attempt; otherwise it reads back first and dispatches only when the recovery answers
`WasDispatched = false`.

| Boundary | Behaviour |
| --- | --- |
| crash before dispatch | recovery answers `WasDispatched = false`; the stage dispatches once |
| crash after dispatch, before the response | recovery answers `WasDispatched = true`; the stage never re-dispatches |
| crash after the outcome is persisted | the plan already carries the outcome; the coupon is skipped |
| `Pending` / `Unknown` | `AwaitingExternal`, claim held, no successor, read back on the next replay |
| `Rejected` | `NeedsReconciliation`, no successor, local association untouched |
| contradictory `Confirmed` | `NeedsReconciliation`, detail persisted, never retried |
| multi-coupon partial | settled coupons are skipped; only the first unsettled coupon is attempted per pass |

Dispatch order is ordinal by `(EMD document number, EMD coupon number)`, so the sequence is reproducible
across replays and process restarts.

---

## 10. Association History

`EmdCouponAssociationChange` is a new append-only child of `EmdCoupon`, ordered by `Sequence`:

| Kind | When | Carries |
| --- | --- | --- |
| `Associated` | at EMD issuance, when the coupon is born attached | the ticket coupon identity only |
| `DisassociatedByReissue` | on a reassociation | where it came from, the operation, the decision, the provider reference |
| `Reassociated` | on a reassociation | where it came from and where it went, plus the same authority evidence |

The `Associated` row deliberately records the ticket coupon **identity only**, with no ticket document number
or coupon number, because the EMD aggregate does not know them at issuance and must not duplicate
ticket-owned state. The later rows do carry document numbers, because those are immutable audit evidence of an
accountable-document move supplied by the servicing decision, not current state.

The history is append-only: `Reassociate` appends both rows and only then moves
`AssociatedTicketCouponId`. A repeated move onto the coupon it already sits on returns without appending
anything and without bumping `DocumentVersion`, so a replayed finalization is a no-op.

---

## 11. Persistence and Migrations

Migration `20260911204022_P3G1EmdAssociationLifecycle` — **two new tables, no change to any existing column**:

| Table | Key | Notes |
| --- | --- | --- |
| `AcceptedExchangePlanAncillaries` | composite `(OperationId, EmdCouponId)` | natural key, no surrogate id; cascade from the plan |
| `EmdCouponAssociationChanges` | `Id`, unique `(EmdCouponId, Sequence)` | owned collection of `EmdCoupon` |

The ancillary row uses the natural composite key on purpose: the plan store has no id generator, and
`(operation, EMD coupon)` is already the unique business identity of a disposition. The unique index on
`(EmdCouponId, Sequence)` makes a duplicate history sequence unrepresentable rather than validated.

`AcceptedExchangePlanStore` was extended to load and save the ancillary rows and gained
`RecordAncillaryAssociationOutcomeAsync`, which persists one coupon's provider outcome and commits before the
next coupon is attempted.

`dotnet ef migrations has-pending-model-changes` reports no pending changes. The migration was applied to the
dev database.

---

## 12. Observability

`ExchangeOutcome` gained two fields:

* `AncillaryState` — the provider-neutral roll-up (`NotRequired`, `NotStarted`, `Pending`, `Confirmed`, `Rejected`, `NeedsReconciliation`).
* `Ancillaries` — one `ExchangeAncillaryOutcome` per accepted disposition, carrying the leg identity, the EMD document and coupon, the predecessor coupon, the target, the disposition, the per-coupon state, the decision reference and version, the provider reference and any contradiction detail.

An exchange with no ancillary reports `NotRequired` and an empty list, which is how the unchanged P3-F path is
observable as unchanged.

---

## 13. Ports and Contract Tests

| Port | Unconfigured | Deterministic | Reusable kit |
| --- | --- | --- | --- |
| `IAncillaryExchangeDispositionPort` | `UnconfiguredAncillaryDispositionProvider` → 20294 / 501 | `DeterministicAncillaryDispositionAdapter` | N/A — side-effect-free lookup |
| `IEmdAssociationPort` | `UnconfiguredEmdAssociationProvider` → 20295 / 501 | `DeterministicEmdAssociationAdapter` | `EmdAssociationPortContract` |

`EmdAssociationPortContract` is the reusable kit any implementation must pass: a confirmed move names what it
moved and where; a never-dispatched key answers `WasDispatched = false`; a dispatched key is recoverable under
its own key only; a repeated key never moves the coupon twice; a conflicting intent on a known key fails
closed; one key never consumes another operation's move; a read-back never rewrites resolved evidence; and no
local Ordering identity is ever reported back.

Both deterministic adapters are registered in `AeroTech.Ordering.Providers.Deterministic` behind the existing
activation flag, and both unconfigured providers are the default registration in
`AeroTech.Ordering.Providers`. The test harness accepts either a steerable deterministic adapter or an
arbitrary port implementation, so the unconfigured path is exercised through the real orchestration.

---

## 14. Edge Matrix A–AO

### Scope — what is affected (A–F)

| # | Case | Result |
| --- | --- | --- |
| A | no ancillary at all | completed; neither ancillary port consulted; `NotRequired` |
| B | standalone EMD | outside scope; disposition source never consulted |
| C | voided EMD document | outside scope |
| D | voided EMD coupon | outside scope |
| E | ancillary on a flown coupon outside the reissue scope | outside scope |
| F | affected ancillary | described to the source in accountable terms with purpose and sub code |

### Decisions that cannot be trusted (G–O)

| # | Case | Result |
| --- | --- | --- |
| G | missing decision | 20296; nothing mutated; no provider call |
| H | duplicated decision | 20297 |
| I | decision for an ancillary outside the affected scope | 20297 |
| J | decision naming another predecessor coupon | 20297 |
| K | decision naming another predecessor document | 20297 |
| L | `ReassociateExisting` with no target | 20297 |
| M | target outside the accepted successor scope | 20297 |
| N | decision with no reference of its own | 20297 |
| O | target is a historical `Used` coupon | 20297; no history row written |

### Dispositions this capability cannot run (P–R)

| # | Case | Result |
| --- | --- | --- |
| P | each of `Refund`, `ExchangeToNewEmd`, `RetainAsResidual`, `Cancel`, `ManualReview` | 20298 before the first irreversible operation |
| Q | one unsupported disposition among two ancillaries | the whole exchange stops; nothing moved |
| R | a refused decision replayed under the same command | terminal replay with the same code |

### A source that cannot answer (S–T)

| # | Case | Result |
| --- | --- | --- |
| S | unreachable disposition source | `AwaitingExternal`; no plan persisted; nothing mutated; retryable |
| T | unconfigured disposition source | 20294 / 501; `AwaitingExternal`; no plan; no successor |

### The settled move (U–W)

| # | Case | Result |
| --- | --- | --- |
| U | confirmed reassociation | completed; association on the successor coupon; provider and decision evidence projected |
| V | the request shape | accountable-document terms only; key carries document, coupon and operation |
| W | AddCollect exchange | capture observed; the move happens only after the money settled |

### The unsettled move (X–Z)

| # | Case | Result |
| --- | --- | --- |
| X | refused move | `NeedsReconciliation`; no successor ticket; no order change; association untouched |
| Y | `Pending` and `Unknown` that stay unresolved | `AwaitingExternal`; one dispatch only; read back on replay; claim held |
| Z | unresolved that resolves on read-back | completed once; one dispatch; one order change |

### Evidence the provider cannot prove (AA–AD)

| # | Case | Result |
| --- | --- | --- |
| AA | crash after dispatch | read back, not re-dispatched; exactly one `Reassociated` row |
| AB | confirmation naming another associated coupon | `NeedsReconciliation`; detail persisted; association untouched |
| AC | confirmation with no provider reference | `NeedsReconciliation` |
| AD | confirmation naming another EMD document | `NeedsReconciliation` |

### More than one ancillary (AE–AG)

| # | Case | Result |
| --- | --- | --- |
| AE | two affected ancillaries | both moved; two distinct stable keys; two distinct leg identities |
| AF | attach order reversed | dispatch and projection order is ordinal by document number |
| AG | one confirms, one stays pending | `AwaitingExternal`; on resolution the settled one is not repeated; one `Reassociated` row each |

### The durable record (AH–AL)

| # | Case | Result |
| --- | --- | --- |
| AH | association history | `Associated`, `DisassociatedByReissue`, `Reassociated` in sequence 1, 2, 3 with full provenance |
| AI | document version | incremented exactly once per moved coupon |
| AJ | process restart with a shared provider | the accepted ancillary plan reloads intact; recovery finalizes; the source is never re-asked |
| AK | replay of a completed exchange | same successor; one dispatch; one decision; one history row; `Confirmed` projected |
| AL | unconfigured association source | 20295 / 501; nothing moved; no order change; no successor |

### What the aggregate refuses (AM–AO)

| # | Case | Result |
| --- | --- | --- |
| AM | reassociating a standalone EMD | 20300 |
| AN | reassociating a coupon whose association already moved elsewhere | 20302 |
| AO | reassociating onto the coupon it already sits on | idempotent no-op; no version bump; no history row |

---

## 15. Regression Results

TESTS_PLACEHOLDER

---

## 16. Requirement → Code → Test Traceability

| Requirement | Code | Test |
| --- | --- | --- |
| the blanket refusal is replaced, not deleted | `ExchangePreconditions.AffectedAncillariesAsync` | A–F, B6 |
| affected scope is mechanical | `ElectronicMiscDocument.CouponsAssociatedWith` | B, C, D, E |
| every affected coupon needs an explicit disposition | `ExchangeAncillaryPlanner.Accept` | G |
| a decision must be structurally sound | `ExchangeAncillaryPlanner.Accept` | H–N |
| a target must be in the successor scope and not historical | `ExchangeAncillaryPlanner.Accepted` | M, O |
| only `ReassociateExisting` executes | `ExchangeAncillaryPlanner.EnsureExecutable` | P, Q, R |
| refusals happen before irreversible work | `ExchangeService.ExecuteFreshAsync` ordering | G–R |
| an unavailable source does not mutate anything | `MarkAwaitingExternalAsync` around `DecideAsync` | S, T |
| the accepted plan is complete before it is persisted | `plan with { AncillaryDispositions = ... }` before `SaveAsync` | AJ |
| one stable durable key per coupon | `ExchangeService.ReassociationKey` | V, AE |
| recover-first, never re-dispatch | `ReassociateAncillaryAsync` | AA, Y, AJ |
| the move happens after money and before local finalization | `FinalizeAsync` gate order | W, X |
| confirmations must not contradict the request | `ExchangeSettlementEvidencePolicy.ReassociationContradiction` | AB, AC, AD |
| the local move is inside the finalizing transaction | `ApplyReassociationsAsync` before `TransitionAsync` | X, U |
| deterministic multi-coupon ordering and partial completion | ordinal ordering plus per-coupon records | AE, AF, AG |
| append-only association history | `EmdCoupon.Reassociate` / `Append` | AH, AO |
| the aggregate refuses an inadmissible move | `ElectronicMiscDocument.ReassociateCoupon` | AM, AN, AO |
| the outcome projects the ancillary plan | `ExchangeOutcome.AncillaryState` / `Ancillaries` | A, U, X, Y, AK |
| port contract is reusable | `EmdAssociationPortContract` | `DeterministicEmdAssociationPortTests` |

---

## 17. ICC Changes

Two new entries, both `BLOCKED_INTEGRATION`:

* `ICC-P3-ANCILLARY-EXCHANGE-DISPOSITION` — the authoritative per-ancillary decision. Side-effect-free, no
  operation key, no recovery, with the fail-closed validation table and the reason the target is a predecessor
  coupon number.
* `ICC-P3-EMD-ASSOCIATION` — the durable association move. Per-coupon stable identity, all five outcome
  states, recover-first with `WasDispatched`, the evidence-contradiction rule, and the irreversible-step
  ordering.

The catalog index line was updated. No existing entry was modified.

---

## 18. BLOCKED_DEVELOPMENT

None. Every semantic this revision needed was decidable from existing frozen rules plus the brief.

---

## 19. BLOCKED_INTEGRATION

1. **No authoritative ancillary disposition source.** AirPrice exposes no per-ancillary exchange disposition
   endpoint. The Ordering boundary, validation and gate are complete and deterministic; the source is not
   wired.
2. **No real EMD association authority.** `UnconfiguredEmdAssociationProvider` fails closed with 20295 / 501.
3. **Whether reassociation exists as an operation at all** on the real authority, or only as void-and-reissue
   of the EMD. If only the latter, `ReassociateExisting` is not implementable there and the disposition becomes
   `ExchangeToNewEmd`, which this revision deliberately does not execute.
4. **Whether the authority echoes the coupon it acted on.** Ordering treats each echoed field as optional and
   verifies every one that is present.
5. **Whether the real authority is idempotent under Ordering's operation key.**

---

## 20. Deferred

Deliberately out of scope and **not** approximated:

* EMD refund, EMD exchange to a new EMD, EMD residual value and EMD cancellation as part of an exchange.
* Any EMD value lifecycle — revaluation, repricing, fee recalculation on reissue.
* A new `ServicingOperationKind` for ancillary servicing.
* An operator remediation command for a `NeedsReconciliation` ancillary. The durable evidence a future command
  needs — accepted target, decision identity and version, provider outcome, provider reference and
  contradiction detail — is all already persisted.
* Ancillaries attached to a different accountable document of the same order.
* Integration events for the association move. The domain history exists; no wire contract was added.
* P3-G2.

---

## 21. No-Redesign-Risk Check

| Risk | Why it does not force a later redesign |
| --- | --- |
| more dispositions become executable | each is an independent stage keyed like this one; `AncillaryExchangeDisposition` already carries all six values, so no enum or schema change is needed |
| the real target arrives as a successor coupon number | `TargetPredecessorCouponNumber` and `TargetSuccessorTicketCouponId` are both stored; a successor-shaped decision resolves to the same stored pair |
| more than one ancillary per exchange | already the modelled case, with per-coupon keys, records and partial completion |
| an ancillary needs its own servicing operation later | the accepted plan and history are keyed by EMD coupon, not by the exchange, so they survive being read by another operation |
| association history grows a new kind | `EmdCouponAssociationChangeKind` is an enum on an append-only child table; a new kind is additive |
| the authority rejects after a confirmed document | already modelled as `NeedsReconciliation` with full evidence, the same as every other post-document economic exception in P3-F |

---

## 22. P3-F Regression Guarantee

No P3-F frozen semantic changed. Specifically:

* `AcceptedExchangePlan` gained one **optional** trailing parameter, so every existing construction site and
  every existing `with` expression is unchanged, and a plan with no ancillary is structurally identical to a
  P3-F plan.
* The monetary settlement chain, its ordering, its keys, its evidence rules and its reconciliation states are
  untouched.
* The migration adds two tables and changes no existing column, so a P3-F-era plan row reads back exactly as
  before with an empty ancillary collection.
* `FinalizeAsync` gained one gate that is `false` whenever no ancillary is affected.
* One P3-F test changed meaning, deliberately and visibly: `ExchangeFlowTests.B6` no longer asserts the
  blanket 20272 refusal; it now asserts the fail-closed 20296 gate. That is the behaviour change this revision
  was asked to make.

---

## 23. Scope Confirmation

Delivered, as asked:

* the safe guard replaced by an explicit-disposition gate, for `ReassociateExisting` only;
* every affected ancillary carries an authoritative disposition before any exchange mutation;
* no ancillary carried forward silently;
* no EMD refund, exchange or value lifecycle;
* no new `ServicingOperationKind`;
* P3-G2 not started.

Not committed, not pushed, no pull request opened.

---

## 24. Files

**New — Contracts**

```text
Contracts/AeroTech.Messages/Ordering/Enums/AncillaryExchangeDisposition.cs
Contracts/AeroTech.Messages/Ordering/Enums/EmdCouponAssociationChangeKind.cs
Contracts/AeroTech.Messages/Ordering/Enums/ExchangeAncillaryState.cs
```

**New — Domain**

```text
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/Arguments/EmdCouponReassociation.cs
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/Entities/EmdCouponAssociationChange.cs
src/AeroTech.Ordering.Domain/Ports/AncillaryDisposition/IAncillaryExchangeDispositionPort.cs
src/AeroTech.Ordering.Domain/Ports/EmdAssociation/IEmdAssociationPort.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/AcceptedExchangeAncillaryDisposition.cs
```

**New — Application**

```text
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/AffectedAncillaryAssociation.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeAncillaryOutcome.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeAncillaryPlanner.cs
```

**New — Persistence**

```text
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanAncillaryRow.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanAncillaryConfiguration.cs
src/AeroTech.Ordering.Persistence/Migrations/20260911204022_P3G1EmdAssociationLifecycle.cs
src/AeroTech.Ordering.Persistence/Migrations/20260911204022_P3G1EmdAssociationLifecycle.Designer.cs
```

**New — Providers**

```text
src/AeroTech.Ordering.Providers/Unconfigured/UnconfiguredAncillaryDispositionProvider.cs
src/AeroTech.Ordering.Providers/Unconfigured/UnconfiguredEmdAssociationProvider.cs
src/AeroTech.Ordering.Providers.Deterministic/DeterministicAncillaryDispositionAdapter.cs
src/AeroTech.Ordering.Providers.Deterministic/DeterministicEmdAssociationAdapter.cs
src/AeroTech.Ordering.Providers.Deterministic/DeterministicEmdAssociationOperation.cs
```

**New — Tests**

```text
tests/AeroTech.Ordering.Persistence.Tests/Contracts/EmdAssociation/EmdAssociationPortContract.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/EmdAssociation/EmdAssociationPortFixture.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/EmdAssociation/DeterministicEmdAssociationPortTests.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/EmdAssociation/UnconfiguredAncillaryProviderTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/AncillaryDispositionGateTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/EmdReassociationFlowTests.cs
```

**Modified**

```text
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/ElectronicMiscDocument.cs
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/Entities/EmdCoupon.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/AcceptedExchangePlan.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/Contracts/IAcceptedExchangePlanStore.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/Policies/ExchangeSettlementEvidencePolicy.cs
src/AeroTech.Ordering.Domain/_Shared/Resources/ExceptionFactory.cs
src/AeroTech.Ordering.Domain/_Shared/Resources/ExceptionMessages.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeOutcome.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangePreconditions.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeScope.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeService.cs
src/AeroTech.Ordering.Persistence/ElectronicMiscDocumentAggregate/ElectronicMiscDocumentConfiguration.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanRow.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanStore.cs
src/AeroTech.Ordering.Providers/DependencyInjection.cs
src/AeroTech.Ordering.Providers.Deterministic/DependencyInjection.cs
tests/AeroTech.Ordering.Persistence.Tests/P1/OrderSliceHarness.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/ExchangeFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/ExchangeScenarios.cs
reports/order-domain-v1/p3/P3-integration-capability-catalog.md
```

---

## 25. New Exception Codes

All inside the mandated 20000–29999 range and contiguous with the existing block.

| Code | Name | HTTP |
| --- | --- | --- |
| 20294 | `AncillaryDispositionSourceNotConfigured` | 501 |
| 20295 | `EmdAssociationSourceNotConfigured` | 501 |
| 20296 | `AncillaryDispositionMissing` | 422 |
| 20297 | `AncillaryDispositionMalformed` | 422 |
| 20298 | `AncillaryDispositionNotExecutable` | 422 |
| 20299 | `ElectronicMiscDocumentCouponNotFound` | 404 |
| 20300 | `ElectronicMiscDocumentIsNotAssociable` | 422 |
| 20301 | `ElectronicMiscDocumentCouponIsNotAssociable` | 409 |
| 20302 | `ElectronicMiscDocumentAssociationMoved` | 409 |
| 20303 | `ElectronicMiscDocumentNotFound` | 404 |

Highest code now in use: **20303**.
