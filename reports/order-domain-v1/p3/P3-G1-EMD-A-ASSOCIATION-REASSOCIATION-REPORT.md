# P3-G1 — EMD-A Association Lifecycle and Reissue Reassociation

Closing report for P3-G1, the first slice of P3-G ancillary servicing.

Companion documents, all in this folder:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, carrying `ICC-P3-ANCILLARY-EXCHANGE-DISPOSITION` and `ICC-P3-EMD-ASSOCIATION`.
* [P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md](P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md) — the frozen P3-F baseline this builds on.
* [P3-PHASE-PLAN.md](P3-PHASE-PLAN.md) — the P3-G deliverables and exit gate this slice answers to.

---

## 1. Starting State

```text
P3-G1 correction baseline   08bb99ed43f1f8a6e4494c13a341615e07bf3104  P3-G1 — Consolidated Freeze-Gate Correction
Residual correction baseline 7adad7e3e106e8cb8388c797e599e118842a5e50  P3-G1 — FINAL FREEZE CORRECTION + Report
Working tree at each start  clean
```

This report covers P3-G1 and the P3-F residual document-coupling correction that closing P3-G1 exposed. The
residual correction itself is described in
[P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md](P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md) §7.1; its effect on this
slice is that a document-coupled residual is settled by the document act and is therefore never an unsettled
downstream leg holding up the ancillary stage.

Inherited frozen baseline: fully-unused, multi-coupon and partially-used exchange; repeated A→B→C lineage;
Even, AddCollect, Refund-Due, Residual and Mixed settlement; the accepted-plan rail with per-stage durable
evidence; recover-first with `WasDispatched` on every provider rail; and the integration capability catalog.

**Correction to the P3-F closing report.** That report stated no EMD aggregate exists. It does. The following
was already present and was built on rather than recreated:

| Existing asset | Location |
| --- | --- |
| `ElectronicMiscDocument` aggregate root, `Issue`, `Void`, `EnsureCanBeVoided`, `DocumentsService` | `src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/` |
| `EmdCoupon` with `AssociatedTicketCouponId`, `Purpose`, `ReasonForIssuanceSubCode`, `Status` | `.../ElectronicMiscDocumentAggregate/Entities/EmdCoupon.cs` |
| `EmdCouponStatus`, `ElectronicMiscDocumentType`, `EmdCouponPurpose`, `ElectronicMiscDocumentStatus` | `Contracts/AeroTech.Messages/Ordering/Enums/` |
| EMD issuance rail (`IEmdIssuancePort`, `DeterministicEmdIssuanceAdapter`) and EMD void lifecycle | Application / Providers |
| `IElectronicMiscDocumentRepository` with `ListByOrderAsync`, `GetAsync`, `AddAsync` | `.../Contracts/` |

What the EMD aggregate had no concept of was the *lifecycle* of an association: the coupon knew which ticket
coupon it pointed at, but nothing recorded where it came from, when it moved, on whose authority, or against
which provider evidence.

---

## 2. The Governing Semantic

Two invariants, and they are **independent**:

```text
a provider-confirmed ticket exchange is an authoritative fact
that no later monetary or ancillary outcome can undo
```

```text
the authoritative attachment of an ancillary follows the authority, never leads it
```

The distinction the implementation now enforces everywhere:

```text
ticket exchange confirmed  !=  servicing operation completed
```

This is not a new rule. `P3-PHASE-PLAN.md` already froze the P3-G exit gate as:

> association changes are coupon-level; revalidation preserves association while reissue breaks it;
> **EMD refund/exchange are independent of ticket refund/exchange**; no dependent ancillary is left implicitly
> untouched.

and the P3-G deliverable as:

> The Amadeus rule (S1), normalized: **reissue ⇒ EMD-A disassociated**, then source-decided refund, exchange
> into a new EMD associated at issuance, retain-as-residual, cancel, or manual review.

`docs/order-domain-design-v1/02-PRICING-AND-SERVICING.md` §692 states the same separation for money:
"Ordering records confirmed document outcome and accepted commercial reversal treatment; **Payment separately
confirms release/refund of actual money**."

---

## 3. The Corrected Post-Document Sequence

```text
quote
  -> accept
  -> obtain ancillary dispositions                     (side-effect-free)
  -> validate binding and structure                    (FAIL CLOSED HERE)
  -> persist the complete accepted plan
  -> eligibility
  -> funding guarantee                                 (AddCollect / Mixed)
  -> inventory change
  -> document exchange
  ===================== document authority confirms =====================
  -> persist the document confirmation evidence
  -> MATERIALIZE, in one transaction:
        order change committed
        predecessor ETKT  = Exchanged
        affected coupons  = Exchanged
        successor ETKT    minted at its pre-minted identity
        lineage           predecessor -> successor, per coupon
        every affected EMD-A coupon -> DisassociatedByReissue
     ...committed before any further downstream dispatch
  -> monetary settlement    (capture -> refund-due / residual, frozen P3-F order)
  -> EMD-A reassociation    (per coupon, ordinal by document then coupon)
  -> complete the servicing operation
```

The single change from the previous revision is that **materialization moved ahead of monetary settlement**.
Previously `FinalizeAsync` entered `SettleMonetaryAsync` before `MaterializeAsync`, so an unresolved capture,
refund-due or residual still hid an already-confirmed reissue: no successor locally, no lineage, and the
affected EMD-A still attached to a coupon that the authority had already exchanged.

Three supporting corrections were required and are easy to miss:

| Correction | Why it is load-bearing |
| --- | --- |
| a committed exchange replays terminally **only** once the servicing operation reached `Completed` | otherwise a replay short-circuits into `ReplayCompletedAsync` and never runs the remaining stages |
| `AdvanceAsync` no longer treats **this operation's own** commercial-version bump as a concurrent change | otherwise every resume after materialization immediately reconciles |
| `IsUsableSuccessorIdentityAsync` accepts a successor **this operation already minted** | otherwise the document-number uniqueness probe rejects our own successor on resume |

### Valid states this produces

```text
Ticket = Exchanged   Successor = retrievable   EMD-A = disassociated
Funding capture = Pending          Operation = AwaitingExternal
```

```text
Ticket = Exchanged   Successor = retrievable   EMD-A = disassociated
Refund-due = Rejected              Operation = NeedsReconciliation
```

```text
Ticket = Exchanged   Successor = retrievable   EMD-A = disassociated
Reassociation = Pending/Unknown    Operation = AwaitingExternal
```

The existence of a local successor never means the servicing operation completed.

### Monetary safety is unchanged

Collection capture still precedes refund-due or residual execution for a Mixed plan; a collection that never
settles never returns value; no confirmed document exchange is compensated because money failed; no confirmed
capture is reversed because a later return-of-value leg failed; and reassociation is not dispatched until the
accepted plan's monetary prerequisites are satisfied. Materialization changed **when local truth is written**,
not the economics or the leg order.

---

## 4. Association Is Two Transitions

`ElectronicMiscDocument.DisassociateCouponByReissue` — **mechanically implied by the confirmed reissue**:

* sets `AssociatedTicketCouponId = null`;
* appends exactly one `DisassociatedByReissue` row carrying the predecessor ticket coupon identity, document
  number and coupon number, the exchange operation id, the decision reference and the **document-exchange**
  provider reference;
* increments `DocumentVersion` exactly once;
* is a no-op on replay of the same operation — no duplicate row, no extra version bump;
* fails closed with `ElectronicMiscDocumentAssociationMoved` (20302) if the coupon moved to an unrelated
  association.

**No provider call is invented for it.** The frozen P3-G semantic is that reissue breaks the association, so
once the authority has confirmed the reissue the local disassociation is a consequence, not a request. Adding
an external disassociation command purely for architectural symmetry would fake a provider capability. If a
real host contract later requires one, that is an ACL adaptation, recorded as `BLOCKED_INTEGRATION`.

`ElectronicMiscDocument.ReassociateCoupon` — the **authoritative attachment**:

* requires the coupon to be in the legitimate post-reissue disassociated state **of this same operation**;
* sets `AssociatedTicketCouponId` to the successor coupon;
* appends exactly one `Reassociated` row carrying the predecessor evidence, the successor ticket coupon
  identity, the successor document and coupon numbers, the operation id, the decision reference and the
  **EMD-association** provider reference;
* increments `DocumentVersion` exactly once;
* is a no-op when the coupon already sits on that successor;
* is applied in the same transaction as the provider outcome that authorised it.

`DisassociatedByReissue` is no longer appended inside the reassociation method.

**Revalidation is untouched.** It is not a reissue, it does not enter this path, and it preserves the
association — asserted by `PostDocumentTruthFreezeGateTests.FG10`.

---

## 5. Scope — What Makes an Ancillary Affected

Computed mechanically by `ElectronicMiscDocument.CouponsAssociatedWith`, with no inference:

```text
document.Type == Associated
&& document.StatusSummary != Voided
&& coupon.Status == OpenForUse
&& coupon.AssociatedTicketCouponId ∈ { Open coupons of the resolved accountable predecessor }
```

A `Used` coupon is historical pricing context, so an ancillary attached to one is **not** affected — the
affected ancillary scope is never wider than the document mutation scope.

---

## 6. The Authoritative Disposition and Its Binding

`IAncillaryExchangeDispositionPort` — a Domain port under `Domain/Ports/AncillaryDisposition/`, **side-effect
free**: one `DecideAsync`, no operation key, no recovery, no `WasDispatched`.

`AncillaryExchangeDisposition`:

| Value | This slice |
| --- | --- |
| `ReassociateExisting` | **executed** |
| `Refund`, `ExchangeToNewEmd`, `RetainAsResidual`, `Cancel`, `ManualReview` | recognised, recorded, refused with 20298 |

The target is `TargetPredecessorCouponNumber`, because successor coupon numbers do not exist when the decision
is obtained; it maps deterministically through the accepted plan to the pre-minted successor coupon identity.

**Response binding.** The request carries a `ContextFingerprint` — a SHA-256 over the order id, quoted exchange
id, predecessor document number, the ordered reissue scope, and every affected coupon's document number,
coupon number, EMD type, purpose, sub code and predecessor coupon. A conforming answer must echo **both** the
`QuotedExchangeId` and the `ContextFingerprint`. Anything else is `AncillaryDispositionContextMismatch`
(20304, 422), raised before the accepted plan is persisted. Every accepted disposition row retains its
`DecisionContextFingerprint` for audit and replay.

### Structural validation — fail closed

`ExchangeAncillaryPlanner` is the single place a decision is judged, and all of it happens before
`AcceptedExchangePlanStore.SaveAsync` and before `order.PrepareExchange`:

| Condition | Code | HTTP |
| --- | --- | --- |
| an affected coupon has no decision | 20296 `AncillaryDispositionMissing` | 422 |
| two decisions name the same coupon | 20297 `AncillaryDispositionMalformed` | 422 |
| a decision names an ancillary outside the affected scope | 20297 | 422 |
| a decision names another predecessor document or coupon | 20297 | 422 |
| `ReassociateExisting` carries no target coupon | 20297 | 422 |
| the target is outside the accepted successor scope | 20297 | 422 |
| the target is a `Used` coupon | 20297 | 422 |
| the decision carries no reference of its own | 20297 | 422 |
| the answer names another quoted exchange | 20304 `AncillaryDispositionContextMismatch` | 422 |
| the answer's context fingerprint does not match | 20304 | 422 |
| the disposition is not `ReassociateExisting` | 20298 `AncillaryDispositionNotExecutable` | 422 |

All are durable rejections through the existing `TryRecordRejectionAsync` rail: the plan is stored as
`AcceptedExchangeDisposition.Rejected` and the same code and HTTP status replay terminally under the same
command receipt.

An unreachable or unconfigured disposition source is handled differently on purpose: the operation is left
`AwaitingExternal`, **no plan is persisted**, nothing is mutated, and the command is retryable from the start.
That is a configuration or transport problem, not a business rejection of this exchange.

---

## 7. The Reassociation Stage

`IEmdAssociationPort` — `ReassociateAsync` plus `RecoverReassociationAsync`, the same durable rail shape as
every other P3 provider boundary.

Stable operation identity, **one per affected coupon**:

```text
emd-reassociate:{emdDocumentNumber}:{emdCouponNumber}:{operationId}
```

so a multi-coupon exchange has one independently recoverable operation per coupon and partial completion is
representable rather than lost.

The request crosses the boundary in accountable-document terms only — EMD document and coupon number,
predecessor document and coupon number, successor document and coupon number, the beneficiary traveller id,
the issuing carrier id and the decision reference. No EMD coupon id, ticket coupon id or order service id
leaves Ordering.

**Evidence validation.** `ExchangeSettlementEvidencePolicy.ReassociationContradiction` is the single place a
confirmation is judged. A `Confirmed` result is contradictory when it carries no provider reference, or names a
different EMD document, EMD coupon, associated document or associated coupon than the plan asked for. A
contradiction persists into `AssociationDetail`, moves the operation to `NeedsReconciliation`, leaves the
coupon detached and never retries the move. Echoed fields are optional; each one present is verified.

**Pre-dispatch admissibility.** Before dispatching, the stage loads the EMD aggregate and asks
`PermitsReassociation`. If the document or coupon was voided, or the coupon is not in this operation's
disassociated state, the operation reconciles rather than dispatching a move that could not be applied.

---

## 8. Transaction Boundaries

| # | Transaction | Contents |
| --- | --- | --- |
| 1 | **Materialization** | order change, predecessor `Exchanged`, lineage, successor ticket, `DisassociatedByReissue` for every affected coupon |
| 2 | **Per-coupon settlement** | the provider outcome row and, for a clean `Confirmed`, that coupon's `Reassociated` transition |
| 3 | **Completion** | operation status, receipt, claim release |

Transaction 2 pairing the provider evidence with the local attachment is deliberate: there is no window in
which the evidence is durable but the attachment is missing, or the reverse.

Transaction 1 is committed only when a downstream stage remains (`HasUnsettledDownstreamStage`). When an Even
exchange has no ancillary, materialization and completion remain a **single** transaction, exactly as P3-F
froze it.

---

## 9. Recovery and Crash Semantics

| Boundary | Behaviour | Test |
| --- | --- | --- |
| D1 — host executed, confirmation not persisted | recover document exchange, no redispatch, persist, materialize exactly one successor | `G1_C6` |
| D2 — confirmation persisted, successor not materialized | replay materializes the pre-minted successor exactly once | `G1_C6` |
| D3 — successor materialized, disassociation not saved | atomic with transaction 1, so it cannot split; replay applies each exactly once | `G1_C7_C8` |
| D4 — local truth saved, monetary not yet dispatched | document untouched; money continues under its existing stable key | `D4_a_crash_before_the_first_capture_dispatch...` |
| D5 — monetary dispatched, result not persisted | existing recover-first / `WasDispatched` | frozen P3-F `AddCollectFundingRecoveryTests` |
| D6 — money stays Pending/Unknown | recovers money only; no new ticket, no document redispatch, no duplicate disassociation, no premature reassociation | `FG1_FG2` |
| D7 — money Rejected or contradictory | successor and lineage retained, EMD detached, `NeedsReconciliation`, no rollback | `FG3` |
| D8 — money later resolves | continues to reassociation under the existing stable key | `FG6_FG7` |
| reassociation dispatched, response lost | read back, never re-dispatched | `G1_C9_C10` |
| reassociation confirmed, local save lost | atomic with transaction 2; replay applies only what is missing | `G1_C9_C10` |

Dispatch order is ordinal by `(EMD document number, EMD coupon number)`, reproducible across replays and
restarts.

**On D3.** Materialization and disassociation share one transaction, so "successor saved but disassociation
not" cannot occur. The gap is closed by atomicity rather than by a compensating replay, and `G1_C7_C8` asserts
the observable claim instead: across a crash and replay, exactly one `DisassociatedByReissue` row and exactly
one version bump for it. The replay path still calls the idempotent disassociation defensively.

---

## 10. Association History

`EmdCouponAssociationChange` is an append-only child of `EmdCoupon`, ordered by `Sequence`:

| Kind | When | Carries |
| --- | --- | --- |
| `Associated` | at EMD issuance, when the coupon is born attached | the ticket coupon **identity only** |
| `DisassociatedByReissue` | in the materialization transaction | where it came from, the operation, the decision, the document-exchange provider reference |
| `Reassociated` | with the confirmed provider outcome | where it came from and where it went, plus the association provider reference |

The `Associated` row records the ticket coupon identity only, with no ticket document or coupon number,
because the EMD aggregate does not know them at issuance and must not duplicate ticket-owned state. The later
rows do carry document numbers, because those are immutable audit evidence of an accountable-document move
supplied by the servicing decision, not current state.

---

## 11. Persistence and Migrations

Two migrations across the whole of P3-G1:

| Migration | Change |
| --- | --- |
| `20260911204022_P3G1EmdAssociationLifecycle` | two new tables: `AcceptedExchangePlanAncillaries` (composite key `(OperationId, EmdCouponId)`) and `EmdCouponAssociationChanges` (unique `(EmdCouponId, Sequence)`). No existing column touched. |
| `20260911215648_P3G1AncillaryDecisionBinding` | one additive `nvarchar(64)` column `DecisionContextFingerprint` on `AcceptedExchangePlanAncillaries`, with a default; `Down` drops it. |

**This correction required no further schema change.** The corrected sequence replays entirely from evidence
that already existed: the document outcome and successor identity on the accepted plan, the pre-minted
successor ticket and coupon identities, the monetary leg outcomes, the ancillary dispositions, the association
outcomes, and the append-only association history. No transient orchestration state was mirrored into columns.

No persisted numeric enum value changed. No historical migration was edited.

```text
dotnet ef migrations has-pending-model-changes
  --project src/AeroTech.Ordering.Persistence
  --startup-project src/AeroTech.Ordering.ServiceHost
  --context OrderingDbContext

No changes have been made to the model since the last migration.
```

---

## 12. Outcome and API Truth

`ExchangeOutcome` carries `AncillaryState` plus one `ExchangeAncillaryOutcome` per accepted disposition (leg
identity, EMD document and coupon, predecessor coupon, target, disposition, per-coupon state, decision
reference and version, provider reference, contradiction detail).

After document confirmation, every response and replay for that operation exposes the confirmed truth:

| Situation | `DocumentOutcome` | `SuccessorElectronicTicketId` | `OperationStatus` |
| --- | --- | --- | --- |
| capture Pending | `Exchanged` | not null | `AwaitingExternal` |
| capture Rejected | `Exchanged` | not null | `NeedsReconciliation` |
| refund-due / residual Pending | `Exchanged` | not null | `AwaitingExternal` |
| reassociation Pending / Unknown | `Exchanged` | not null | `AwaitingExternal` |
| reassociation Rejected / contradictory | `Exchanged` | not null | `NeedsReconciliation` |

`ExchangeOutcome` is what `AcceptExchangeCommand` returns and what `OrderChangeResponse.Exchange` carries, so
Backoffice and OTA consume the same shared truth. No channel-specific business logic and no new public
endpoint were added.

An exchange with no affected ancillary reports `AncillaryState = NotRequired` and an empty list, which is how
the unchanged P3-F path is observable as unchanged.

---

## 13. Ports and Contract Test Kits

| Port | Unconfigured | Deterministic | Reusable kit |
| --- | --- | --- | --- |
| `IAncillaryExchangeDispositionPort` | `UnconfiguredAncillaryDispositionProvider` → 20294 / 501 | `DeterministicAncillaryDispositionAdapter` | `AncillaryDispositionPortContract` |
| `IEmdAssociationPort` | `UnconfiguredEmdAssociationProvider` → 20295 / 501 | `DeterministicEmdAssociationAdapter` | `EmdAssociationPortContract` |

`AncillaryDispositionPortContract` replaces the previous revision's `N/A — side-effect-free lookup`. A lookup
that mutates nothing still has a semantic consumer contract, and any implementation must pass the same one:
every affected coupon receives an explicit decision; no ancillary outside the affected scope appears; no
coupon is decided twice; the predecessor document and coupon binding is exact; a `ReassociateExisting` target
is inside the reissue scope; a coupon outside the reissue scope is never offered as a target; the answer echoes
the `QuotedExchangeId` and `ContextFingerprint`; an answer for one context cannot pass for another; a different
predecessor document is a different context; the same context fingerprints identically however often it is
asked; and every `AncillaryExchangeDisposition` value survives without silent remapping. It carries no
recovery semantics.

`EmdAssociationPortContract`: a confirmed move names what it moved and where; a never-dispatched key answers
`WasDispatched = false`; a dispatched key is recoverable under its own key only; a repeated key never moves the
coupon twice; a conflicting intent on a known key fails closed; one key never consumes another operation's
move; a read-back never rewrites resolved evidence; no local Ordering identity is reported back.

---

## 14. Cross-Stage Edge-Case Matrix

Axes crossed: document outcome × monetary plan × post-document monetary state × ancillary state. Equivalent
cells are collapsed deliberately; every distinct state-transition or recovery invariant has a load-bearing
test.

### Document outcome × ancillary (collapsed on monetary = Even)

| Document | Ancillary | Expected | Test |
| --- | --- | --- | --- |
| Confirmed | none | completed; neither ancillary port consulted; `NotRequired` | `AncillaryDispositionGateTests.A` |
| Confirmed | `ReassociateExisting` Confirmed | completed; `Associated → DisassociatedByReissue → Reassociated` | `G1_C1` |
| Confirmed | Pending | reissue authoritative; coupon detached; `AwaitingExternal` | `G1_C2_C3` |
| Confirmed | Unknown | same | `G1_C2_C3` |
| Confirmed | Rejected | reissue authoritative; coupon detached; `NeedsReconciliation` | `G1_C4` |
| Confirmed | contradictory Confirmed (coupon / document / no reference) | same, contradiction persisted | `G1_C5` |
| Pending | any | no materialization, no disassociation, no reassociation | frozen P3-F `ExchangeFlowTests` |
| Unknown | any | same | frozen P3-F `ExchangeFlowTests` |
| Rejected | any | terminal; nothing materialized | frozen P3-F `ExchangeFlowTests` |
| contradictory Confirmed | any | `NeedsReconciliation`; nothing materialized | frozen P3-F `DocumentExchangeIdentityTests` |

Document outcomes other than a clean `Confirmed` are collapsed across the ancillary axis on purpose: the
disposition gate runs before the document stage and the ancillary stage runs after it, so a non-confirmed
document never reaches either.

### Monetary plan × post-document monetary state (with an affected EMD-A)

| Monetary plan | Post-document money | Expected | Test |
| --- | --- | --- | --- |
| Even | n/a | straight to reassociation | `G1_C1` |
| AddCollect | capture Confirmed | reassociation runs after capture | `The_move_happens_only_after_the_money_has_settled` |
| AddCollect | capture Pending | ticket authoritative, EMD detached, no reassociation, `AwaitingExternal` | `FG1_FG2` |
| AddCollect | capture Unknown | same | `FG1_FG2` |
| AddCollect | capture Rejected | ticket authoritative, EMD detached, `NeedsReconciliation` | `FG3` |
| AddCollect | crash before capture dispatch | local truth durable; money resumes on its own key | `D4_...` |
| RefundDue | return Pending | ticket authoritative, EMD detached, no reassociation | `FG4_FG5` |
| Residual | residual Pending | same | `FG4_FG5` |
| Mixed (collection + refund-due) | leg 1 Confirmed, leg 2 Pending → resolves | ticket authoritative throughout; reassociation only after leg 2 | `FG6_FG7` |
| Mixed (collection + residual) | same | same | `FG6_FG7` |
| Mixed | collection never settles | no return of value dispatched | `FG8_a_collection_that_never_settles...` |
| contradictory capture / refund / residual | — | `NeedsReconciliation` with the successor retained | frozen P3-F evidence policies + `FG3` |

`throw-before-dispatch` and `throw-after-dispatch` on the monetary rails are covered by the frozen P3-F
`AddCollectFundingRecoveryTests`, `RefundDueExchangeFlowTests` and `ResidualExchangeFlowTests`; this
correction adds `D4` for the new ordering (local truth already durable when the throw happens).

### Multi-coupon and durability

| Case | Expected | Test |
| --- | --- | --- |
| two affected ancillaries, both confirm | two distinct stable keys, two leg identities | `Every_affected_ancillary_moves_under_its_own_stable_key` |
| attach order reversed | dispatch ordinal by document number | `The_affected_ancillaries_are_settled_in_a_deterministic_order` |
| one confirms, one pending, then resolves | settled one not repeated; one `Reassociated` row each | `One_unresolved_ancillary_holds_completion...` |
| process restart mid-reassociation | accepted plan reloads intact; source never re-asked | `The_accepted_ancillary_plan_survives_a_process_restart` |
| replay of a completed exchange | same successor; one dispatch; one decision; one history row | `Replaying_a_completed_exchange_never_moves_the_ancillary_again` |
| document version | one bump per transition (two in total) | `The_moved_ancillary_document_is_versioned_once_per_transition` |

### Scope, decision and aggregate refusals

Covered by `AncillaryDispositionGateTests` A–T (scope A–F, structural refusals G–O, non-executable
dispositions P–R, unavailable source S–T), `EmdReassociationFlowTests` G1-C11/G1-C12 (binding), and the
aggregate-level refusal tests (standalone, moved elsewhere, never disassociated, double disassociation,
double reassociation, another operation's claim).

### Revalidation

| Case | Expected | Test |
| --- | --- | --- |
| revalidation of a coupon carrying an EMD-A | association preserved, no version bump, no history row, neither ancillary port consulted | `FG10` |

---

## 15. Regression Results

Every run below was executed in isolation. Concurrent test processes share the `DotAirOrderNew` dev database
and interfere with claim-conflict and document-number assertions, so overlapping runs are not reported here.

### Complete projects

```text
dotnet test tests/AeroTech.Ordering.Domain.Tests/AeroTech.Ordering.Domain.Tests.csproj
  passed 511   failed 0   skipped 0   total 511

dotnet test tests/AeroTech.Ordering.Persistence.Tests/AeroTech.Ordering.Persistence.Tests.csproj
  passed 985   failed 0   skipped 0   total 985
```

### Focused suites

| Suite | Passed | Failed | Skipped | Total |
| --- | --- | --- | --- | --- |
| `PostDocumentTruthFreezeGateTests` — FG1–FG8, FG10, D4 | 11 | 0 | 0 | 11 |
| `EmdReassociationFlowTests` — G1-C1…C12 plus the retained matrix | 29 | 0 | 0 | 29 |
| `AncillaryDispositionGateTests` — A–T | 24 | 0 | 0 | 24 |
| `ResidualDocumentCouplingTests` — RD1–RD11 | 15 | 0 | 0 | 15 |
| `ResidualExchangeFlowTests` | 16 | 0 | 0 | 16 |
| `MixedExchangeFlowTests` | 53 | 0 | 0 | 53 |
| `DocumentExchangeIdentityTests` + `ExchangeCrashBoundaryTests` | 40 | 0 | 0 | 40 |
| Both port contract kits — `Contracts.AncillaryDisposition` + `Contracts.EmdAssociation` | 35 | 0 | 0 | 35 |
| P3-F exchange regression — `AddCollectExchangeFlowTests`, `AddCollectFundingRecoveryTests`, `RefundDueExchangeFlowTests`, `ExchangeFlowTests`, `PartiallyUsedExchangeFlowTests`, `MultiCouponExchangeFlowTests` | 216 | 0 | 0 | 216 |

### Migration check

```text
dotnet ef migrations has-pending-model-changes
  --project src/AeroTech.Ordering.Persistence
  --startup-project src/AeroTech.Ordering.ServiceHost
  --context OrderingDbContext

No changes have been made to the model since the last migration.
```

### Exception-code invariant

```text
305 codes, 20001-20305, contiguous
outside 20000-29999 : none
duplicates          : none
gaps                : none
inline `new BusinessException` anywhere in src/ : none
```

### A note on one reported duration

The complete persistence run reports a wall-clock duration of `8 h 34 m`. That is elapsed time across a
machine suspend, not execution time; the same suite completed in about eight minutes immediately before and
after. It is reported as observed rather than edited, and it is not a performance signal.


---

## 16. Benchmark Traceability

Narrow question benchmarked, before implementation:

> Once the accountable ticket exchange/reissue is confirmed by the document authority, may downstream
> monetary or ancillary settlement uncertainty make the system behave as though the reissue has not happened?

**Answer: no for the ancillary follow-up, and no for a confirmed reissue in general — with one genuine
contradiction on residual value, recorded as `BLOCKED_DECISION` below.**

| Semantic | Benchmark evidence | AeroTech behaviour | Test |
| --- | --- | --- | --- |
| EMD-A association is **coupon-level** | IATA, *Airline Guide to EMD Implementation*, 1st ed. July 2010, §4.2.5 / §5.1.4: "Association is by coupon…"; "Only one ET flight coupon may be associated to an EMD-A value coupon, however multiple EMD-A value coupons may be associated to the same ET flight coupon"; "association and disassociation is by coupon." Corroborated by Travelport Smartpoint *Working with EMDs*: "The EMD-A is linked to the specific Electronic Ticket (ET) flight coupon in the airline's ET database." | Scope, disposition, stable provider key, history and both transitions are all per `EmdCoupon`; `AcceptedExchangePlanAncillaries` is keyed `(OperationId, EmdCouponId)`; the provider key is `emd-reassociate:{doc}:{coupon}:{operationId}` | `Every_affected_ancillary_moves_under_its_own_stable_key`, `AncillaryDispositionGateTests.F` |
| Ticket reissue and EMD follow-up are **separable facts** | IATA §4.2.5: "…EMD-A 001 coupon 1 is disassociated from ET 001 coupon 1. **After exchange of the ET**, EMD-A value document 001 … coupon 1 **can** be re-associated to the relevant coupon … on the new ET flight ticket." FAQ A24: "After disassociation the EMD-A is available for further use or for exchange or refund." Note "can", not "must" — and no ET rollback is contemplated where it is not. Travelport GWS *EMD Exchange* is its own service call against an already-ticketed booking. | The reissue is materialized and completed independently; the ancillary is a separate downstream stage whose `Pending`/`Unknown`/`Rejected`/contradictory outcome never undoes it | `G1_C2_C3`, `G1_C4`, `G1_C5` |
| A reissue **requires** prior disassociation, and automating it is expected | IATA §5.2.2: "When exchanging/reissuing an EMD-A, **or the ET to which it is associated, the documents must first be disassociated**. As discussed in section 5.1.4.2, **some System Providers may automate this function**, if not it will be necessary for the user to disassociate them manually." | Ordering automates exactly this: `DisassociateCouponByReissue` is applied as a consequence of the confirmed reissue, in the materialization transaction, with no provider call of its own | `G1_C1`, `FG1_FG2`, `G1_C7_C8` |
| A confirmed reissue is **final** and not re-openable by later steps | IATA §5.2.2: the Coupon Status Indicator for an exchanged/reissued coupon is `E`, and "**This is a final status** and … renders that coupon eligible to be included in the lift to Revenue Accounts." §5.2.3: an ET coupon already at `E` "**cannot** be associated" to an EMD-A coupon. Rollback exists only as an explicit, narrow, separately authorised compensating transaction — IATA §5.3.4 *Void Exchange* / *Refund Cancel*, and Amadeus `TRDC` gated by an airline-level `VOID EXCHANGE/REISSUE = Y/N` flag **and only on the day of the reissue**. | No automatic compensation of a confirmed document exchange for any downstream failure; a refused or contradictory downstream outcome becomes `NeedsReconciliation` with the successor and lineage retained | `FG3`, `G1_C4`, `G1_C5`, and the frozen P3-F evidence policies |
| **Revalidation ≠ reissue** for the resulting association | Partly contradicted in mechanism, confirmed in outcome. IATA §4.2.5 requires a disassociate/re-associate cycle for revalidation **too**: "…requires the ET to be revalidated **or** exchanged, therefore EMD-A 002 coupon 1 is disassociated… After the revalidation or exchange of the ET, EMD-A 002 … can be re-associated to **original ET 002** coupon 1 **if revalidated** or to the relevant coupon on the **new ET** if exchanged." Carrier-official AEGEAN Hub states the net outcome: "EMD-A will remain associated to original ticket in case of involuntary **revalidation**"; "EMD-A will be associated to a **new** ticket in case of involuntary **reissue**." | Ordering preserves the association across revalidation with **no** transition and no history row. The **resulting authoritative state is identical** to IATA's cycle (same ticket, same coupon), but Ordering does not write the intermediate detach/attach pair. This is a deliberate, recorded divergence in mechanism, not in outcome | `FG10` |
| Ancillary refund/reuse treatment is **authoritative-source driven**, never invented | Vendor-primary: Sabre Dev Studio *GetAncillaryOffers RQ/RS User Guide* (v3.0.2, Aug 2018) — ancillary data "contains part of the information from **S7 record**"; `AncillaryRules` are "Rules defined for an ancillary, such as **refundability** or form of payment"; payloads carry `<RefundableReissuable>Y/N/R</RefundableReissuable>` and `<FormOfRefund code="1">ORIGINAL</FormOfRefund>`. **ATPCO's own Optional Services reference manual was NOT obtained** (subscription-gated; atpco.net returns 403 to automated fetch), so this is vendor-primary evidence of ATPCO S7 semantics, not ATPCO primary | `IAncillaryExchangeDispositionPort` obtains every disposition from the authority; Ordering never defaults, infers or substitutes one, and refuses `Refund` / `ExchangeToNewEmd` / `RetainAsResidual` / `Cancel` / `ManualReview` with 20298 rather than approximating them | `AncillaryDispositionGateTests.G`, `.P`, `.Q`, `.R`; `AncillaryDispositionPortContract` |
| Downstream EMD/value handling does **not** redefine whether a confirmed reissue occurred | **No affirmative standard found.** Strong indirect support from the `E`-is-final rule above. **NO RELIABLE PUBLIC EVIDENCE FOUND** for any standard requiring a confirmed ETKT exchange to be undone because a later EMD or money step failed | implemented as frozen | `FG1_FG2`, `FG3`, `FG4_FG5`, `FG6_FG7` |

### Evidence gaps — stated, not filled

* **No public evidence either way** on whether add-collect *capture* (PSP settlement) is decoupled from
  ticketing-host exchange confirmation. In GDS/BSP flows the form of payment is part of the ticketing
  transaction itself. Ordering's two-stage guarantee/capture rail is anchored in JetPay's own contract
  (`RequiredGuarantee.AuthorizedBeforeIssuance`, `PaymentIntentStatus Guaranteed → CommittedForIssuance →
  Capturing → Paid`), not in an industry standard.
* **ATPCO primary not obtained** (see above).
* Amadeus Service Hub statements are from indexed snippets only — every `servicehub.amadeus.com` fetch
  returned HTTP 403. Treated as indicative and labelled as such; nothing in the implementation depends on
  them alone.
* IATA PSCRM resolution full text is licensed and not publicly retrievable. Note on citation hygiene: the
  2010 guide cites "Reso 725f" for EMD, but in the current PSCRM (40th ed.) the EMD resolutions are **722h /
  723 / 724 / 725**, and **725f is now "Collection of Reservation Change Fees."** Do not cite 725f as the
  current EMD resolution.

### Residual value — the contradiction that was found, and its resolution

The benchmark exposed one genuine contradiction with the post-document model: a residual/refundable balance
fulfilled as an EMD-S cannot be a downstream stage.

* IATA §5.2.2.3 — "the EMD issued for the residual value or refundable balance **must be an EMD-S**."
* IATA §5.2.2.4 — that EMD-S document number "**must be included in the same Change of Status request
  message**."
* IATA §5.3.5 — a "**single** exchange/reissue request message … **including** … **any EMD-S value document
  number(s) issued for refundable balance or penalty fee**."
* Amadeus Service Hub 911593 — a reissue is refused unless ticket and residual are issued in one entry.

It was raised as `BLOCKED_DECISION` rather than silently implemented, and the business decision resolved it:
an exchange-coupled residual document is executed and recovered as part of the same `IDocumentExchangePort`
operation; external value instruments stay downstream. That correction is implemented and reported in
[P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md](P3-F-MIXED-EXCHANGE-AND-FREEZE-REPORT.md) §7.1 and in
`ICC-P3-EXCHANGE-DOCUMENT`.

```text
MUST NOW   exchange-coupled residual EMD-S issued inside the document exchange operation   — implemented
DEFER      penalty-fee EMD-S issuance in the same message (IATA §5.3.5 names it alongside)  — not in scope
DEFER      EMD-A re-association declared inside the exchange request (IATA §5.3.5, the agency/GDS protocol)
           — Ordering is the carrier side, where §4.2.5 supports post-hoc re-association
NOT APPLICABLE   add-collect capture coupling — no primary evidence either way; the rail follows JetPay's own
           contract, not an industry standard
```


---

## 17. Requirement → Code → Test Traceability

| Requirement | Code | Test |
| --- | --- | --- |
| confirmed document establishes local ticket truth before any downstream stage | `ExchangeService.FinalizeAsync` → `MaterializeAsync` ahead of `SettleMonetaryAsync` | `FG1_FG2`, `FG3`, `FG4_FG5`, `FG6_FG7` |
| local truth is committed before a downstream dispatch | `MaterializeAsync` + `HasUnsettledDownstreamStage` | `D4_...`, `FG1_FG2` |
| replay never creates a second successor | `CommittedExchange` branch in `MaterializeAsync` | `G1_C6`, `FG1_FG2` |
| replay never redispatches the document exchange | recover-first in `EnterDocumentExchangeAsync`; replay routing on `OperationStatusAsync` | `G1_C6`, `FG1_FG2`, `FG3` |
| this operation's own commit is not a concurrent change | `AdvanceAsync` materialized guard | `FG1_FG2` replay leg |
| a successor this operation minted is its own | `IsUsableSuccessorIdentityAsync` | `FG6_FG7` second pass |
| confirmed reissue ⇒ local disassociation | `DisassociateAncillariesAsync` → `ElectronicMiscDocument.DisassociateCouponByReissue` | `G1_C1`, `FG1_FG2`, `FG3` |
| disassociation is idempotent per operation | `EmdCoupon.IsDisassociatedByReissue` | `G1_C7_C8`, `Disassociating_the_same_reissue_twice_changes_nothing` |
| disassociation fails closed on an unrelated association | `PermitsDisassociation` + 20302 | `An_ancillary_whose_association_already_moved_elsewhere...` |
| reassociation requires the post-reissue state | `ReassociateCoupon` + `IsDisassociatedByReissue` | `An_ancillary_that_was_never_disassociated...`, `A_disassociated_ancillary_is_not_claimed_by_another_operation` |
| a later unresolved ancillary never restores the old association | reassociation applied only on clean `Confirmed` | `G1_C2_C3`, `G1_C4`, `G1_C5` |
| monetary leg order unchanged | `SettleMonetaryAsync` unchanged internally | `FG8_...`, frozen P3-F suites |
| reassociation waits for money | `FinalizeAsync` gate order | `FG1_FG2`, `FG6_FG7` |
| no rollback of a confirmed reissue | `ReconcileAsync` / `SettleAsync` carry `MaterializedExchange` | `FG3`, `G1_C4`, `G1_C5` |
| outcome exposes confirmed truth on every replay | `SettleAsync` materialized projection | `FG1_FG2`, `FG3`, `G1_C2_C3` |
| every affected coupon needs an explicit disposition | `ExchangeAncillaryPlanner.Accept` | `AncillaryDispositionGateTests.G` |
| the decision is bound to its own context | `ExchangeAncillaryPlanner.Fingerprint` + echo check | `G1_C11`, `G1_C12`, `AncillaryDispositionPortContract` |
| only `ReassociateExisting` executes | `EnsureExecutable` | `AncillaryDispositionGateTests.P`, `Q`, `R` |
| refusals happen before irreversible work | ordering inside `ExecuteFreshAsync` | `AncillaryDispositionGateTests.G–R`, `PartiallyUsedExchangeFlowTests.J_..._fails_closed_before_the_reissue` |
| one stable durable key per coupon | `ReassociationKey` | `Every_affected_ancillary_moves_under_its_own_stable_key` |
| recover-first, never re-dispatch | `ReassociateAncillaryAsync` | `G1_C9_C10`, `The_accepted_ancillary_plan_survives_a_process_restart` |
| confirmations must not contradict the request | `ExchangeSettlementEvidencePolicy.ReassociationContradiction` | `G1_C5` |
| append-only association history | `EmdCoupon.Append` | `G1_C1`, `G1_C7_C8` |
| revalidation preserves association | untouched revalidation path | `FG10` |
| port contracts are reusable | `AncillaryDispositionPortContract`, `EmdAssociationPortContract` | `Deterministic*PortTests` |

---

## 18. ICC Changes

Both entries updated; no other entry touched.

`ICC-P3-ANCILLARY-EXCHANGE-DISPOSITION` — the response-binding contract and its fingerprint; the binding
failures in the validation table; the corrected irreversible-step ordering; and the reusable contract kit
replacing the previous `N/A`.

`ICC-P3-EMD-ASSOCIATION` — association as two transitions; disassociation as mechanically implied with no
invented provider call; ticket truth independent of downstream settlement; the three transaction boundaries;
the corrected irreversible-step ordering including materialization; read-back applying the missing local
transition; the separate-disassociation-command integration question; and the detached-coupon state as a
deliberately visible outcome.

Both remain `BLOCKED_INTEGRATION`.

---

## 19. BLOCKED_DEVELOPMENT

```text
BLOCKED_DEVELOPMENT:
none
```

Every semantic this correction needed was decidable from the frozen P3-F rails, the P3-G exit gate in
`P3-PHASE-PLAN.md`, and the design pack. No unresolved Ordering business decision was found.

---

## 20. BLOCKED_INTEGRATION

```text
BLOCKED_INTEGRATION:
1. No authoritative ancillary disposition source. AirPrice exposes no per-ancillary exchange disposition
   endpoint. The Ordering boundary, binding, validation and fail-closed gate are complete and deterministic.
2. No real EMD association authority. UnconfiguredEmdAssociationProvider fails closed with 20295 / 501; the
   reissue stays authoritative and the ancillary stays detached with its DisassociatedByReissue evidence.
3. Whether the real host requires a SEPARATE disassociation command before or during the reissue. Ordering
   treats disassociation as mechanically implied by the confirmed reissue and issues no provider call for it.
4. Whether the authority exposes reassociation as its own operation or only as void-and-reissue of the EMD.
   If only the latter, ReassociateExisting is not implementable there and the disposition becomes
   ExchangeToNewEmd, which this slice deliberately does not execute.
5. Whether the authority echoes the EMD coupon and associated coupon it acted on, and whether it is idempotent
   under Ordering's operation key.
6. Whether AirPrice returns the reassociation target as a predecessor coupon number, a successor coupon
   number, or a segment reference. Ordering requires the predecessor coupon number, the only one resolvable at
   decision time.
```

---

## 21. Deferred — Not Approximated

EMD refund; EMD exchange to a new EMD; residual EMD execution; EMD cancellation; `ManualReview` execution; any
EMD value revaluation or repricing; wallet or stored-value liability; a generic ancillary servicing engine; a
new `ServicingOperationKind` for association; a public endpoint added for symmetry; ancillaries attached to a
different accountable document of the same order; integration events for the association move; an operator
remediation command for a `NeedsReconciliation` ancillary (the durable evidence a future one needs is already
persisted); and P3-G2.

---

## 22. P3-F Impact

| P3-F semantic | Status |
| --- | --- |
| monetary leg order (collection before return of value) | unchanged |
| monetary stable keys, evidence policies, reconciliation states | unchanged |
| recover-first and `WasDispatched` on every rail | unchanged |
| `AcceptedExchangePlan` shape | one optional trailing parameter; every existing construction and `with` unchanged |
| Even exchange with no ancillary | materialization and completion remain one transaction |
| **when the successor becomes locally visible** | **changed, deliberately**: it is now materialized at document confirmation instead of after monetary settlement |

That last row is the point of this correction, and it changes what some frozen P3-F tests assert. Tests that
asserted a hidden successor while money was unresolved **after a confirmed document** were repointed to the
corrected truth. Tests asserting a hidden successor **before** a confirmed document (eligibility denied,
guarantee rejected, reservation rejected, document rejected, contradictory document evidence) were not
touched, because nothing is materialized in those cases.

`ExchangeFlowTests.B6` and `PartiallyUsedExchangeFlowTests.J` previously asserted the retired blanket refusal
`ExchangeBlockedByAssociatedMiscDocument` (20272). Both now assert the explicit-disposition gate: `J` gained a
companion proving a reissue *does* proceed under an accepted disposition. Code 20272 is retained in
`ExceptionFactory` but is unreachable; it is kept so the numbering stays contiguous inside 20000–29999 and is
recorded as a known gap in `ICC-P3-EMD-ASSOCIATION`.

---

## 23. Files Changed In This Correction

**Modified — Domain**

```text
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/ElectronicMiscDocument.cs
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/Entities/EmdCoupon.cs
src/AeroTech.Ordering.Domain/Ports/AncillaryDisposition/IAncillaryExchangeDispositionPort.cs
src/AeroTech.Ordering.Domain/Servicing/Plans/AcceptedExchangeAncillaryDisposition.cs
src/AeroTech.Ordering.Domain/_Shared/Resources/ExceptionFactory.cs
src/AeroTech.Ordering.Domain/_Shared/Resources/ExceptionMessages.cs
```

**New — Domain**

```text
src/AeroTech.Ordering.Domain/ElectronicMiscDocumentAggregate/Arguments/EmdCouponDisassociation.cs
```

**Modified — Application**

```text
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeService.cs
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/ExchangeAncillaryPlanner.cs
```

**New — Application**

```text
src/AeroTech.Ordering.Application/OrderAggregate/Services/Exchange/MaterializedExchange.cs
```

**Modified — Persistence / Providers**

```text
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanAncillaryRow.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanAncillaryConfiguration.cs
src/AeroTech.Ordering.Persistence/Servicing/AcceptedExchangePlanStore.cs
src/AeroTech.Ordering.Persistence/Migrations/OrderingDbContextModelSnapshot.cs
src/AeroTech.Ordering.Providers.Deterministic/DeterministicAncillaryDispositionAdapter.cs
```

**New — Persistence**

```text
src/AeroTech.Ordering.Persistence/Migrations/20260911215648_P3G1AncillaryDecisionBinding.cs
src/AeroTech.Ordering.Persistence/Migrations/20260911215648_P3G1AncillaryDecisionBinding.Designer.cs
```

**New — Tests**

```text
tests/AeroTech.Ordering.Persistence.Tests/P3/PostDocumentTruthFreezeGateTests.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/AncillaryDisposition/AncillaryDispositionPortContract.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/AncillaryDisposition/AncillaryDispositionPortFixture.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/AncillaryDisposition/DeterministicAncillaryDispositionPortTests.cs
```

**Modified — Tests**

```text
tests/AeroTech.Ordering.Persistence.Tests/P1/OrderSliceHarness.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/EmdReassociationFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/PartiallyUsedExchangeFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/Contracts/EmdAssociation/UnconfiguredAncillaryProviderTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/AddCollectExchangeFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/AddCollectFundingRecoveryTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/MixedExchangeFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/RefundDueExchangeFlowTests.cs
tests/AeroTech.Ordering.Persistence.Tests/P3/ResidualExchangeFlowTests.cs
```

**Frozen P3-F tests repointed to the corrected post-document truth**

Seventeen `Assert.Null(...SuccessorElectronicTicketId)` / `Assert.Null(await FindTicketAsync(...))` sites and
fifteen companion assertion groups (`DoesNotContain(Changes, Exchange)`, `Empty(predecessor.Exchanges)`,
`Equal(ElectronicTicketStatus.Issued, ...)`, unchanged `CustomerTotal`) were repointed, scoped test by test:

| Test | Was | Now |
| --- | --- | --- |
| `AddCollectExchangeFlowTests.O` (Pending, Unknown) | no successor while capture unresolved | successor present, order change committed, total includes the add-collect |
| `AddCollectExchangeFlowTests.P` | predecessor `Issued`, no exchange, no order change | predecessor `Exchanged`, one exchange, one order change |
| `AddCollectFundingRecoveryTests.H_I` (Pending, Unknown) | no successor on the unresolved pass | successor present on the unresolved pass |
| `AddCollectFundingRecoveryTests.J` | no successor on replay | successor present and stable across replay |
| `AddCollectFundingRecoveryTests.K` | **no successor after the crash** — this assertion *was* the defect | the confirmed reissue survived the crash; the resume creates no second successor |
| `AddCollectFundingRecoveryTests.L` (3 shapes) | reissue not durable on contradictory capture | reissue durable, as the test's own name already claimed |
| `AddCollectFundingRecoveryTests.M` | no successor on refused capture | successor retained |
| `MixedExchangeFlowTests.V`, `W` (2 shapes) | no successor on refused/contradictory capture | successor retained |
| `MixedExchangeFlowTests.Z_AA`, `AL_AM_AN` (3 shapes) | no successor on failed second leg | successor retained, capture not reversed |
| `RefundDueExchangeFlowTests.I`, `K_L_M` (4 shapes), `N` | no successor while refund unresolved/refused | successor retained, total reflects the exchange |
| `ResidualExchangeFlowTests.Y`, `AA_AB_AC` (4 shapes), `AD` | no successor while residual unresolved/refused | successor retained |

**Deliberately not repointed** — these are pre-document cases where nothing is materialized, so a hidden
successor is still correct:

```text
AddCollectExchangeFlowTests.F   refused guarantee
AddCollectExchangeFlowTests.I   refused reservation
AddCollectExchangeFlowTests.L   refused reissue (document Rejected)
AddCollectFundingRecoveryTests.B   contradictory guarantee
AddCollectFundingRecoveryTests.E   unresolved release
RefundDueExchangeFlowTests.R    document Rejected
DocumentExchangeIdentityTests   inconsistent / invalid confirmed coupon mapping (not a valid Confirmed)
```

**Modified — Reports**

```text
reports/order-domain-v1/p3/P3-G1-EMD-A-ASSOCIATION-REASSOCIATION-REPORT.md
reports/order-domain-v1/p3/P3-integration-capability-catalog.md
```

---

## 24. Exception Codes

All inside the mandated 20000–29999 range and contiguous.

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
| 20304 | `AncillaryDispositionContextMismatch` | 422 |

Verified mechanically: 304 codes, 20001–20304, no gaps, no duplicates, none outside the range, and no inline
`new BusinessException` anywhere in `src/`.

---

## 25. Freeze Verdict

```text
P3-F RESIDUAL CORRECTION READY TO FREEZE: YES
P3-G1 READY TO FREEZE: YES
```

Every freeze-gate test is green, in isolation, with exact counts recorded in §15. `BLOCKED_DEVELOPMENT` is
empty. Both remaining `BLOCKED_INTEGRATION` families are real-provider absences, not unresolved Ordering
semantics, and neither blocks the deterministic capability.

The one `BLOCKED_DECISION` this work raised — whether a residual EMD-S may be a downstream stage — was
resolved by the business and implemented: an exchange-coupled residual is executed and recovered inside the
same `IDocumentExchangePort` operation, and external value instruments remain downstream. Nothing was
implemented on an unresolved decision.

Two scope boundaries are recorded rather than silently crossed:

* **Penalty-fee EMD-S.** IATA §5.3.5 names it in the same sentence as the refundable balance. Only the
  residual case is implemented; penalty EMD-S issuance is deferred and listed in `BLOCKED_INTEGRATION`.
* **Revalidation.** IATA §4.2.5 describes a disassociate/re-associate cycle for revalidation too, re-attaching
  to the original ticket. Ordering models the stable net association and writes no intermediate transitions.
  The resulting authoritative state is identical; the divergence is in mechanism and is documented, not
  quietly matched.

P3-G2 has not been started.
