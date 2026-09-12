# P3-F FINAL — Mixed / Multi-Leg Exchange Settlement and P3-F Freeze

Closing report for P3-F Exchange and Reissue.

Companion documents, all in this folder:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog.
* [P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md](P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md) and [P3-F-FREEZE-GATE-CORRECTION-REPORT.md](P3-F-FREEZE-GATE-CORRECTION-REPORT.md)
* [P3-F-ADDCOLLECT-EXCHANGE-REPORT.md](P3-F-ADDCOLLECT-EXCHANGE-REPORT.md) and [P3-F-ADDCOLLECT-FREEZE-GATE-CORRECTION-REPORT.md](P3-F-ADDCOLLECT-FREEZE-GATE-CORRECTION-REPORT.md)
* [P3-F-NEGATIVE-BALANCE-EXCHANGE-REPORT.md](P3-F-NEGATIVE-BALANCE-EXCHANGE-REPORT.md)

---

## 1. Starting State

```text
HEAD at start   cf786aefa0aae147f27225ae6e7dce4cf7905722
Commit          P3-F — Negative-Balance Exchange
Working tree    clean
```

Frozen baseline inherited: fully-unused, multi-coupon and partially-used exchange; repeated A→B→C lineage;
Even, AddCollect, Refund-Due and Residual settlement; the accepted-plan rail with per-stage durable evidence;
recover-first with `WasDispatched` on inventory, document, funding, refund and residual; and the integration
capability catalog.

Two facts found by inspection shaped the whole design. `ChangeMonetaryOutcome.Mixed = 5` **already existed**,
so no enum change was needed. And the accepted plan **already stored per-leg execution evidence in separate
columns** for funding guarantee, funding capture, funding release, refund-due and residual, so no schema
change was needed either.

---

## 2. Implementation Summary

One authoritative pricing result can now carry two independently executable monetary obligations, in exactly
two shapes: one collection with one refund-due, or one collection with one residual.

* `Mixed` uses its existing persisted value. No enum renamed or renumbered.
* An explicit `AcceptedExchangeMonetaryLeg` model with a stable per-leg identity is **derived** from the
  authoritative settlement records already on the accepted plan. Nothing was duplicated and no schema changed.
* Monetary obligations are now read from leg presence rather than from the top-level outcome, which makes
  single-outcome and mixed handling uniform and keeps every historical row valid.
* The settlement stage walks the legs in a fixed order: collection first, then the single return of value.
* Every frozen rail is reused as-is. No new port, no new simulator, no new migration.
  *(Superseded for the residual leg by the document-coupling correction in §7.1 and §11.1: `IDocumentExchangePort`
  gained coupled-residual request, result and recovery semantics, and `ResidualFulfillment` was added.)*
* The projection gained a structured leg collection, so a client can observe each obligation separately.

Verified mechanically: there is **no arithmetic anywhere in `src/` on an accepted monetary amount**, and no
exchange monetary leg uses `PricingSource.OrderingDerived`.

---

## 3. Why Mixed Exists

A professional exchange may present more than one commercial consequence: an amount to collect alongside
value that is separately refundable or preservable. The carrier, market, fare and provider rules decide that,
not Ordering.

So the capability is not "Ordering computes one net amount". It is "AirPrice returns an authoritative
settlement plan and Ordering executes it". Given a plan that collects 137.43 and refunds 21.17, Ordering
collects 137.43 and refunds 21.17. It never produces 116.26. `Mixed` is a classification saying more than one
obligation exists; it is not authority to calculate a net.

Ordering owns none of the commercial meaning: not the outcome, not which legs exist, not the amounts, not the
currencies, not the dispositions, not any relationship between them.

---

## 4. Accepted Monetary Plan Model

`AcceptedExchange` already carried three optional authoritative settlement records — `AddCollect`,
`RefundDue` and `Residual`. Those remain the durable evidence, and `AcceptedExchangeMonetaryPlan.MonetaryLegs()`
projects them into the explicit model:

```csharp
AcceptedExchangeMonetaryLeg(Kind, Amount, CurrencyId, Disposition, ExpectedInstrument)
    LegIdentity  =>  "collection" | "refund-due" | "residual"
```

`ExchangeMonetaryLegKind` is `Collection`, `RefundDue`, `Residual`. `Even` remains zero legs, not a fake
zero-value leg.

**Backward compatibility is structural, not migrated.** The persisted JSON shape is unchanged, so every
historical accepted plan deserializes exactly as before. The obligation readings became presence-based:

```csharp
RequiresFunding    => AddCollect is not null
RequiresRefundDue  => RefundDue  is not null
RequiresResidual   => Residual   is not null
IsMonetarySettled  => every present leg is settled
```

An old AddCollect row therefore has one collection leg, an old Even row has none, and a Mixed row has two.
No historical row was rewritten, no commercial value was recomputed, and no migration was needed — which also
means there is no downgrade path that could silently flatten a Mixed plan, because there is nothing to
downgrade.

**Duplicate legs are unrepresentable rather than rejected.** Each settlement is a single optional record, not
a list, so two collection legs or two return legs cannot be expressed at all. That is stronger than runtime
validation. A test asserts the leg projection always has distinct kinds and identities, and the report records
this deliberately in place of a rejection test that could not be written.

---

## 5. Supported Monetary Matrix

| Outcome | Legs | Settlement order | Finalization gate |
| --- | --- | --- | --- |
| `Even` | none | none | document confirmed |
| `AddCollect` | one collection | capture | document + capture confirmed |
| `Refund` | one refund-due | payout | document + payout confirmed |
| `Residual` — external value | one residual | instrument | document + instrument confirmed |
| `Residual` — document coupled | one residual | **issued inside the document exchange** | document + residual document confirmed together |
| `Mixed` collection + refund-due | two | capture then payout | document + capture + payout confirmed |
| `Mixed` collection + residual — external value | two | capture then instrument | document + capture + instrument confirmed |
| `Mixed` collection + residual — document coupled | two | **residual document inside the exchange, then capture** | document + residual document + capture confirmed |

> **Corrected after P3-G1 freeze (residual document-coupling correction).** The two document-coupled rows are
> new and they supersede the original statement that a residual is always a post-document downstream leg.
> `AcceptedResidual.Fulfillment` now says which of the two applies, and it is authoritative source evidence,
> not an Ordering inference. See §7.1.

---

## 6. Explicitly Unsupported Monetary Shapes

Rejected by the pricing policy with code 20275 before any irreversible work:

```text
Mixed with zero legs
Mixed with one leg
Mixed with three legs
refund-due + residual without a collection
a single outcome hiding a second obligation
an undeclared monetary outcome
```

Unrepresentable by construction:

```text
two collection legs
two refund-due legs
two residual legs
```

Out of scope and not implemented:

```text
split tender
multiple forms of payment
multiple positive collection legs
collection + refund-due + residual
wallet or store-credit funding
arbitrary monetary graphs
```

---

## 7. Orchestration

Both mixed shapes use one order, and it is frozen:

```text
AirPrice quote
  -> AirPrice accept
  -> persist the full AcceptedExchangePlan
  -> protect the collection                       [reversible by release]
  -> inventory mutation
  -> document exchange                            [irreversible]
       (+ the document-coupled residual document, when the accepted plan says so)
  -> persist the document confirmation, the raw successor evidence
     and any returned residual document
  -> local post-document materialization checkpoint
  -> capture the collection                       [irreversible]
  -> persist the capture confirmation
  -> execute the external return leg: refund-due OR external residual   [irreversible]
  -> complete the servicing operation
```

The **external** return leg is dispatched only when the collection capture is confirmed. Otherwise the system
could reissue the document and give value back while failing to collect what is owed. `SettleMonetaryAsync`
enforces it structurally: it returns at the capture stage whenever the collection is unsettled, so the return
leg is never reached.

Checkpoint ordering is respected. The capture outcome is recorded and saved before the external return leg is
dispatched, so a crash between them is unambiguous on replay.

### 7.1 Correction — a document-coupled residual is not a downstream leg

Two statements in the original P3-F freeze were wrong and are corrected here.

**Wrong:** a residual is always fulfilled after the document exchange, through `IExchangeResidualValuePort`.
**Wrong:** in a Mixed plan the residual always follows the capture.

Primary evidence, verified verbatim:

* IATA, *Airline Guide to EMD Implementation* §5.2.2.3 — "If the transaction results in a residual value or
  refundable balance, the EMD issued for the residual value or refundable balance **must be an EMD-S**."
* IATA §5.2.2.4 — "When the transaction results in an EMD-S issued for residual value or refundable balance,
  the document number of the EMD-S **must be included in the same Change of Status request message**."
* IATA §5.3.5 — "…will generate a **single** exchange/reissue request message … **including** the new document
  number(s) … **and any EMD-S value document number(s) issued for refundable balance or penalty fee**."
* Amadeus Service Hub 911593, "RESIDUAL VALUE MUST BE ISSUED SIMULTANEOUSLY" — a reissue is refused unless the
  ticket and the residual document are issued in one entry.

An EMD-S residual therefore cannot be created by a second operation after the exchange has already been
confirmed: its document number has to exist inside the exchange message.

**Corrected semantic:**

```text
document exchange may atomically return
    successor ETKT + exchange-coupled residual document
```

```text
exchange-coupled residual EMD-S  !=  post-document IExchangeResidualValuePort dispatch
```

External value instruments — voucher, travel credit, other source-approved external value — genuinely are
created by their value owner after the exchange, and they keep the original downstream rail unchanged.

**What did not change.** The collection still precedes any *external* return of value. No downstream
return-of-value operation was invented. No automatic compensation was introduced. Ordering still calculates no
residual value: the amount, currency, disposition, expected instrument and now the fulfilment category are all
authoritative source evidence on `AcceptedResidual`.

**Why the Mixed order moves for the coupled case.** The EMD-S cannot wait for the capture, because the host
protocol requires it inside the exchange transaction. The funding **guarantee** still precedes the irreversible
exchange, so the collection is still protected before the document act; only the *capture* now follows the
residual document rather than preceding it. Nothing else in the leg order moved.

**One durable operation.** A document-coupled residual has no operation key of its own: one
`IDocumentExchangePort` key, one dispatch, one recover, one `WasDispatched` decision, covering the successor
ticket and the residual document together. `SettleMonetaryAsync` routes to `IExchangeResidualValuePort` only
when `plan.RequiresExternalResidual`, so the same accepted residual can never be fulfilled through both ports.

**When the host confirms but proves nothing.** A confirmed exchange that returns no residual document, or one
whose amount, currency or instrument family contradicts the accepted obligation, does not invent a document and
does not roll back the ticket. The confirmed document truth is persisted, the residual stays unsettled, and the
operation becomes `NeedsReconciliation`.

---

## 8. Recovery and Crash Semantics

Every stage dispatches fresh only when its prerequisite confirmed in the same attempt; otherwise it reads back
first under the same key and dispatches only on `WasDispatched = false`. A capture confirmed in this attempt
enables the return leg to dispatch fresh, which is why the happy path performs no redundant read-back.

| Stage | Unresolved | Refused | Contradictory confirmation |
| --- | --- | --- | --- |
| Protection | `AwaitingExternal`, claim held, no inventory, document, capture or return | frozen AddCollect rejection, nothing mutated | `NeedsReconciliation` |
| Inventory | `AwaitingExternal`, no document, capture or return | release the protection, then terminal | not applicable |
| Document | `AwaitingExternal`, no capture or return | release the protection, then reconcile | reconcile |
| Capture | `AwaitingExternal`, **zero return dispatch**, claim held | `NeedsReconciliation`, zero return dispatch | `NeedsReconciliation`, zero return dispatch |
| Return leg | `AwaitingExternal`, document and capture stay confirmed | `NeedsReconciliation`, no automatic capture reversal | `NeedsReconciliation` |

After a refused or contradictory return leg the truth is kept exactly: the ticket was reissued, the collection
was captured, the value was not returned. No automatic compensation, no capture reversal, no predecessor
restoration and no second successor. Those need explicit future policy.

Evidence is append-only per leg. A later stage never overwrites an earlier confirmation, so operations can
reconstruct the accepted plan, the protection result, the document result, the capture result, the return
result and the finalization from the plan alone.

---

## 9. Persistence and Migrations

```text
Migrations added: none
Schema changes:   none
Enum changes:     none at the time of the Mixed bundle
                  ResidualFulfillment added later by the document-coupling correction (new enum, new file,
                  no persisted numeric value changed, still no migration)
```

The eight refund and residual columns and the nine funding columns from the previous bundles already carry
every leg's provider state and evidence — including, after the document-coupling correction, a residual that
is settled by the document act rather than by a downstream call. `Mixed` already existed in
`ChangeMonetaryOutcome`. Amounts and currencies stay in the immutable accepted plan JSON, which the
two-condition persistence rule says not to duplicate.

Historical single-outcome rows remain readable and keep their semantics, because the obligation readings are
presence-based rather than outcome-based.

---

## 10. API and Query Observability

No new API surface and no new top-level field per future combination. `ExchangeOutcome` gained one structured
collection:

```csharp
ExchangeMonetaryLegOutcome(Kind, LegIdentity, Amount, CurrencyId, Disposition,
                           State, ProviderReference, InstrumentReference, Instrument)
```

`MonetaryState` is now the aggregate across present legs: rejected if any leg is rejected, released if any is
released, pending if any is pending, settled only when all are. With one leg that equals the leg's own state,
so single-outcome responses are unchanged. The existing singular fields, `FundingState`, `AddCollectAmount`
and the residual instrument fields, are untouched.

A client can therefore render:

```text
Additional collection EUR 137.43   collection leg, Settled
Refund due EUR 21.17               refund leg, Pending
Ticket reissued                    DocumentOutcome = Exchanged
Requires reconciliation: no        RequiresReconciliation = false
```

and the failure case:

```text
Ticket reissued                    DocumentOutcome = Exchanged
Collection capture failed          collection leg, Rejected
Refund not dispatched              refund leg, Required
Requires reconciliation: yes       OperationStatus = NeedsReconciliation
```

without guessing from a generic failure. No PAN, CVV, token or credential is exposed; references are the
providers' own.

---

## 11. Semantic Ports and Contract Tests

No port was added and none was changed **by the Mixed bundle itself**. Mixed reuses the frozen rails exactly:

| Leg | Port | Operation key |
| --- | --- | --- |
| Collection protect | `IExchangeFundingPort` | `exchange-funding-guarantee:{ticketId}:{operationId}` |
| Collection capture | `IExchangeFundingPort` | `exchange-funding-capture:{ticketId}:{operationId}` |
| Collection release | `IExchangeFundingPort` | `exchange-funding-release:{ticketId}:{operationId}` |
| Refund-due | `IRefundValuePort` | `exchange-refund-value:{ticketId}:{operationId}` |
| Residual | `IExchangeResidualValuePort` | `exchange-residual:{ticketId}:{operationId}` |

Each leg's key is distinct by construction because the step name differs, so two obligations can never resolve
to the same economic operation. No `IMixedPaymentPort`, `IMixedRefundPort` or `IMultiLegProvider` was created,
and no all-in-one mixed simulator was built. The existing reusable contract kits are unchanged and still
green, including their same-key conflicting-intent rejection.

### 11.1 Correction — one port did change

The document-coupling correction (§7.1) changed `IDocumentExchangePort`. The final capability split is:

| Port | What it does |
| --- | --- |
| `IDocumentExchangePort` | the ETKT exchange **plus**, when the accepted plan says so, the exchange-coupled residual EMD-S — one key, one dispatch, one recover |
| `IExchangeResidualValuePort` | **external** residual fulfilment only — voucher, travel credit, other source-approved external value |

`DocumentExchangeRequest` gained an optional `ExchangeCoupledResidualRequest`; `DocumentExchangeResult` and
`DocumentExchangeRecovery` gained an optional `ResidualDocumentIdentity`. The residual row in the table above
therefore applies only to an **external** residual; a document-coupled residual has no key of its own and
never reaches `IExchangeResidualValuePort`.

**Every confirmed exchange is judged against the accepted residual obligation**, not only a coupled one. A
residual document returned when the plan owes none, or when the plan fulfils the residual externally, is
contradictory evidence: it is recorded, the document is not materialized, the downstream port is not
dispatched, and the operation reconciles on top of an authoritative ticket. That is what makes
double-fulfilment structurally impossible rather than merely unlikely.

**Only an EMD-S is executable as a coupled document today.** A `DocumentCoupled` residual naming any other
instrument family — including MCO — fails closed with `ExchangeResidualFulfillmentMalformed` (20305) before
any irreversible work. MCO is deferred until a real MCO lifecycle and authority are designed; it is not mapped
onto `ElectronicMiscDocument` and it is not silently reinterpreted as external value.

---

## 12. Edge Matrix A–BH

All pass. Mixed cases A–AY are in `MixedExchangeFlowTests` unless noted; BA–BH are the inherited suites.

| Case | Result | Test | Proof |
| --- | --- | --- | --- |
| A | PASS | `A_a_mixed_collection_and_refund_plan_preserves_both_authoritative_amounts` | 137.43 and 21.17 survive into two distinct legs |
| B | PASS | `B_a_mixed_collection_and_residual_plan_preserves_both_authoritative_amounts` | 83.11 and 14.29 survive into two distinct legs |
| C | PASS | `C_a_mixed_refund_exchange_collects_and_refunds_the_gross_amounts` | guarantee, capture 137.43; refund 21.17; all asserted not equal to 116.26 |
| D | PASS | `D_a_mixed_residual_exchange_collects_and_issues_the_gross_amounts` | capture 83.11; residual 14.29; not 68.82 |
| E–K | PASS | `E_to_K_an_unsupported_mixed_shape_is_refused_before_any_external_mutation` | six shapes, code 20275, zero external calls |
| H, I | PASS | `H_I_a_monetary_leg_kind_can_never_appear_twice` | leg kinds and identities always distinct; duplicates unrepresentable |
| J | PASS | `J_a_malformed_mixed_leg_is_refused_before_any_external_mutation` | zero, negative, foreign currency on either leg |
| L | PASS | `L_a_stale_expected_version_dispatches_nothing` | code 20089, zero acceptances |
| M | PASS | `M_a_fully_unused_mixed_refund_exchange_runs_protect_inventory_document_capture_refund` | one of each stage, one successor, one order change, one price change set |
| N | PASS | `N_a_partially_used_mixed_refund_exchange_keeps_used_coupons_historical` | used coupon untouched, only replaced service to inventory, document scope [2,3] |
| O | PASS | `O_an_unresolved_protection_stops_before_inventory_document_capture_and_refund` | both unresolved outcomes, claim held |
| P | PASS | `P_an_unresolved_protection_that_resolves_on_read_back_protects_once` | one protection total, flow completes |
| Q | PASS | `Q_an_unresolved_inventory_stops_before_document_capture_and_refund`, `Q_a_refused_inventory_releases_the_protection_and_never_refunds` | release once, zero refund |
| R | PASS | `R_an_unresolved_document_stops_before_capture_and_refund` | zero capture, zero refund |
| S | PASS | `S_a_document_that_resolves_on_read_back_captures_once_then_refunds_once` | one document, one capture, one refund |
| T | PASS | `T_an_unresolved_capture_dispatches_no_refund` | zero refund requests and zero refund recoveries |
| U | PASS | `U_a_capture_that_resolves_on_read_back_refunds_once` | one capture, one refund |
| V | PASS | `V_a_refused_capture_needs_reconciliation_and_dispatches_no_refund` | zero refund, no successor, no release |
| W | PASS | `W_a_contradictory_capture_needs_reconciliation_and_dispatches_no_refund` | wrong amount and wrong currency |
| X | PASS | `X_an_unresolved_refund_keeps_the_document_and_capture_confirmed` | collection leg reads Settled while aggregate reads Pending |
| Y | PASS | `Y_an_unresolved_refund_that_resolves_on_read_back_finalizes_once` | one refund, one capture, one document, one successor |
| Z, AA | PASS | `Z_AA_a_refused_or_contradictory_refund_needs_reconciliation_without_reversing_the_capture` | capture stays confirmed, zero releases |
| AB | PASS | `AB_a_completed_mixed_replay_moves_no_money_and_creates_nothing` | one of everything, totals unchanged |
| AC | PASS | `AC_a_fully_unused_mixed_residual_exchange_runs_protect_inventory_document_capture_residual` | instrument reference present, zero refund calls |
| AD | PASS | `AD_a_partially_used_mixed_residual_exchange_keeps_used_coupons_historical` | used coupon untouched |
| AE–AH | PASS | `AE_AF_AG_AH_an_unresolved_stage_never_reaches_the_residual_leg` | protection, document, capture pending and capture refused all yield zero residual |
| AI | PASS | `AI_a_capture_confirmed_but_never_recorded_is_recovered_then_the_residual_runs_once` | cross-process, one capture, one residual |
| AJ, AK | PASS | `AJ_AK_an_unresolved_residual_holds_then_resolves_without_a_duplicate` | one residual total |
| AL–AN | PASS | `AL_AM_AN_a_failed_residual_needs_reconciliation_without_reversing_the_capture` | refused, wrong amount, missing instrument |
| AO | PASS | `AO_a_completed_mixed_residual_replay_calls_no_provider` | one of each, replay flagged |
| AP | PASS | `AP_a_protection_confirmed_but_never_recorded_is_recovered_without_a_duplicate` | cross-process, one protection |
| AQ | PASS | `AQ_a_document_confirmed_but_never_recorded_yields_one_successor` | one document, one successor |
| AR | PASS | `AR_a_capture_confirmed_but_never_recorded_runs_the_refund_once` | zero refund before the crash, one after |
| AS | PASS | `AS_a_refund_confirmed_but_never_recorded_pays_once` | cross-process, one payout, zero re-capture, zero re-document |
| AT | PASS | `AT_a_residual_confirmed_but_never_recorded_issues_one_instrument` | same instrument reference recovered |
| AU–AW | PASS | port contracts: `ExchangeFundingPortContract`, `RefundValuePortContract`, `ExchangeResidualPortContract` | conflicting amount, currency, destination or disposition on an existing key fails closed |
| AX | PASS | `AX_every_monetary_leg_owns_a_distinct_economic_operation_key` | three distinct keys, each naming its own step |
| AY | PASS | `AY_a_repeated_mixed_exchange_settles_against_the_current_accountable_predecessor` | B is the predecessor, two refunds against two documents, distinct keys, four tickets |
| AZ | PASS | `ExchangeFlowTests` even suite | unchanged |
| BA | PASS | `AddCollectExchangeFlowTests`, `AddCollectFundingRecoveryTests` | unchanged |
| BB | PASS | `RefundDueExchangeFlowTests` | unchanged |
| BC | PASS | `ResidualExchangeFlowTests` | unchanged |
| BD | PASS | `ExchangeFlowTests`, `MultiCouponExchangeFlowTests` | unchanged |
| BE | PASS | `PartiallyUsedExchangeFlowTests` | unchanged |
| BF | PASS | `VoluntaryChangeFlowTests`, `VoluntaryChangeCrashBoundaryTests` | revalidation unchanged |
| BG | PASS | `ExchangeCrashBoundaryTests`, `DocumentExchangeIdentityTests` | unchanged |
| BH | PASS | `AddCollectFundingRecoveryTests` release and capture recovery | unchanged |

Three deferral tests were retargeted. Every declared outcome is now supported, so the fail-safe default is
proved with an **undeclared** value, `(ChangeMonetaryOutcome)99`, which still defers before inventory and
releases the claim. That keeps the guard tested rather than deleting it.

---

## 13. Regression Results

```text
dotnet build AeroTech.Ordering.sln                                    Build succeeded
dotnet test tests/AeroTech.Ordering.Domain.Tests --no-build           511 / 511 passed
dotnet test tests/AeroTech.Ordering.Persistence.Tests --no-build      870 / 870 passed
```

Focused runs during implementation:

| Suite | Result |
| --- | --- |
| `MixedExchangeFlowTests` | 53 / 53 |
| Domain suite including the new leg tests | 511 / 511 |
| All port contract suites | unchanged and green within the persistence run |

Zero failures, zero skips. No other test project was modified.

---

## 14. Requirement → Code → Test Traceability

| Requirement | Production implementation | Test proving it |
| --- | --- | --- |
| AirPrice multi-leg authority | `AcceptedExchangeMonetaryPlan.MonetaryLegs`, `ExchangePricingPolicy.EnsureTheOutcomeMatchesItsLegs` | `Mixed…A`, `Mixed…B`, domain `A_mixed_plan_carries_one_collection_leg_and_one_return_leg` |
| No Ordering netting | no arithmetic on accepted amounts anywhere in `src/`; each leg dispatched with its own amount by `CaptureAsync`, `DispatchRefundDueAsync`, `DispatchResidualAsync` | `Mixed…C`, `Mixed…D` assert gross amounts and explicitly assert not-equal to the netted value |
| Accepted plan before irreversible work | `ExecuteFreshAsync` persists the plan before `AdvanceAsync` | `Mixed…L` (stale version, zero dispatch), `Mixed…E_to_K` (zero external calls on refusal) |
| Stable per-leg operation identity | `FundingGuaranteeKey`, `FundingCaptureKey`, `FundingReleaseKey`, `RefundDueKey`, `ResidualKey` | `Mixed…AX` proves three distinct keys naming their own steps |
| Protect before inventory | `EnterFundingAsync` precedes `EnterReservationAsync` | `Mixed…O` proves zero inventory while protection is unresolved |
| Document before capture | capture is reached only from `FinalizeAsync`, itself reached only from a confirmed document | `Mixed…R` proves zero capture while the document is unresolved |
| Capture before return leg | ordered gate in `SettleMonetaryAsync`, with an explicit `IsCollectionSettled` guard | `Mixed…T`, `Mixed…V`, `Mixed…W`, `Mixed…AE_AF_AG_AH` all prove zero return dispatch |
| Capture pending prevents return dispatch | gate returns at the capture stage | `Mixed…T` asserts zero refund requests and zero refund recoveries |
| Capture rejected prevents return dispatch | `AfterMonetarySettlementAsync` reconciles | `Mixed…V` |
| Refund recovery | recover-first in `SettleRefundDueAsync` | `Mixed…X`, `Mixed…Y`, `Mixed…AS` |
| Residual recovery | recover-first in `SettleResidualAsync` | `Mixed…AJ_AK`, `Mixed…AT` |
| Provider-confirmed, local save lost | per-stage recover-first plus injectable providers across harnesses | `Mixed…AP`, `AQ`, `AR`, `AS`, `AT` |
| Partial-used isolation | unchanged `ExchangePreconditions` and plan coupon scope | `Mixed…N`, `Mixed…AD` |
| Repeated-exchange predecessor resolution | unchanged `ResolveAccountableDocumentAsync` | `Mixed…AY` |
| Single-outcome backward compatibility | presence-based `RequiresFunding`, `RequiresRefundDue`, `RequiresResidual`; no schema change | domain `An_even_plan_carries_no_monetary_leg`, `A_single_outcome_can_never_hide_a_second_obligation`, plus the whole inherited AddCollect, Refund and Residual suites |
| Projection observability | `ExchangeMonetaryLegOutcome`, aggregate `MonetaryState`, `LegState`, `LegProviderReference` | `Mixed…M` asserts two settled legs; `Mixed…X` asserts a settled collection leg under a pending aggregate |
| One successor, one order change, one price change set | `FinalizeAsync` behind the settlement gate; unique index on `SuccessorElectronicTicketId` | `Mixed…M`, `Mixed…AB`, `Mixed…AQ`, `Mixed…AY` |

---

## 15. ICC Changes

All in [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md). No parallel catalog was
created.

* **`ICC-P3-EXCHANGE-AIRPRICE`** — the capability scope now covers mixed; a stated no-netting invariant with
  the worked 137.43 / 21.17 example; the two supported shapes and everything excluded; the duplicate-leg
  guarantee recorded as unrepresentable rather than validated; and a new first integration blocker for
  multi-leg settlement representation, including that an adapter must never hand Ordering a pre-netted amount.
* **`ICC-P3-EXCHANGE-FUNDING`** — states that the same guarantee-then-capture rail serves the mixed collection
  leg with unchanged semantics, keys and recovery; one funding reference in current scope; per-leg keys cannot
  collide. No second payment capability was created.
* **`ICC-P3-EXCHANGE-REFUND-VALUE`** and **`ICC-P3-EXCHANGE-RESIDUAL`** — each states that it may execute as a
  dependent second leg, eligible only after the document and the collection capture are confirmed, with
  identical ports, evidence rules, recovery guarantees and ownership gaps. Neither was duplicated.
* **`ICC-P3-EXCHANGE-DOCUMENT`** — the finalization gate table now covers both mixed shapes and records the
  ordered settlement walk.

---

## 16. BLOCKED_DEVELOPMENT

```text
None.
```

No unresolved Ordering-owned business semantic was found. Ordering does not choose the outcome, the legs, the
amounts, the currencies or the dispositions, and it does not net. Everything needed was already decided by the
brief, the frozen behaviour or the existing code.

---

## 17. BLOCKED_INTEGRATION

Real external capability gaps only.

| Capability | Status |
| --- | --- |
| AirPrice mixed settlement-plan representation | Unverified. AirPrice must state single-leg or mixed and name each obligation's kind, amount, currency and disposition. An adapter must not synthesize a leg or pre-net the amounts. |
| Real AirPrice exchange support, including the explicit authoritative amount and the open/used split | Unverified |
| Real inventory exchange and recovery, including plan-level replace and `WasDispatched` | Unverified |
| Real document host subset reissue and recovery | Unverified |
| Real AddCollect funding integration: read-back by caller key, exact obligation evidence, two-stage guarantee and capture, same-key idempotency, release, completion after authorization | Unverified |
| Real refund-due return-of-value integration: exact obligation evidence, read-back, same-key idempotency, original-source semantics | Unverified |
| Real residual fulfilment: **ownership itself unverified** — the owner could be the document host, Payment, a stored-value service or another provider, and may vary by market — plus authoritative instrument identity, read-back, same-key idempotency and family mapping | Unverified |
| `TicketCouponFinancialStatus.Used` production ingestion | Missing. No writer exists inside Ordering. |
| `TicketCouponControlStatus` writer | Missing |
| Consumed-operational-segment evidence ingestion | Missing. Issue-time and current-bound segments are both reported; neither is the flown segment. |

Unconfigured production adapters fail closed. No simulator is wired as a production fallback, and simulator
success is not evidence of integration readiness.

---

## 18. Deferred

```text
P3-G EMD and Ancillary servicing
generic EMD and MCO lifecycle
ancillary exchange and ancillary refund
split tender and multiple forms of payment
multiple positive collection legs
collection + refund-due + residual, and refund-due + residual together
wallet and travel-bank residual handling
StoredValue and Ledger integration
involuntary servicing and schedule disruption
forfeit dispositions
automatic monetary compensation after partial failure
real external ACL verification for every provider above
```

The residual outcome stays shaped so P3-G can attach a real instrument lifecycle to an existing instrument
reference without redesigning Exchange.

---

## 19. No-Redesign-Risk Check

```text
Yes.
```

A Backoffice, OTA or public API client reads `MonetaryOutcome` to present Even, Additional Collection, Refund
Due, Residual Value, or Additional Collection with either Refund Due or Residual Value. It then reads
`MonetaryLegs` for per-obligation amount, currency, disposition and state, `FundingState` for collection
protection and capture detail, `DocumentOutcome` and the successor document number for the ticket,
`SuccessorElectronicTicketId` for the local successor, and `RequiresReconciliation` with `OperationStatus` for
reconciliation. Each axis moves independently.

Adding a future shape means adding a settlement record and one branch in the ordered gate. It does not touch
the aggregate, the plan schema, the port set or the projection contract.

---

## 20. P3-F Freeze Checklist

```text
[x] fully-unused Exchange frozen
[x] multi-coupon Exchange frozen
[x] partially-used Exchange frozen
[x] repeated Exchange frozen
[x] Even frozen
[x] AddCollect frozen
[x] RefundDue frozen
[x] Residual frozen
[x] Mixed Collection+RefundDue frozen
[x] Mixed Collection+Residual frozen
[x] accepted-plan crash safety proven
[x] Inventory recovery proven
[x] Document recovery proven
[x] Funding recovery proven
[x] Refund recovery proven
[x] Residual recovery proven
[x] per-leg intent idempotency proven
[x] no-netting invariant proven
[x] backward compatibility proven
[x] API/query observability proven
[x] ICC current
[x] full final regression green
[x] BLOCKED_DEVELOPMENT contains no fake integration blockers
[x] deferred P3-G scope untouched
```

Closing architecture review, the ten questions:

1. **Recalculates AirPrice meaning?** No. Verified by grep: no arithmetic on any accepted monetary amount in
   `src/`, and no exchange leg uses `PricingSource.OrderingDerived`.
2. **Blind retry on an ambiguous result?** No. Every stage recovers first unless its prerequisite confirmed in
   the same attempt.
3. **Replay creating a second ticket?** No. Finalization sits behind the settlement gate and
   `SuccessorElectronicTicketId` carries a unique index.
4. **Duplicate value movement on replay?** No. Per-leg stable keys, simulator intent fingerprints, and
   recover-first; proved by the five crash cases.
5. **A value leg before its prerequisites?** No. The ordered gate returns at the collection stage whenever it
   is unsettled.
6. **A used coupon entering successor scope?** No. `ExchangePreconditions` and the plan coupon set are
   untouched, and both partial-use mixed cases assert it.
7. **Old persisted single-outcome data invalid?** No. Presence-based readings, unchanged JSON, no migration.
8. **Clients observing state without internals?** Yes, through the leg projection and the aggregate state.
9. **An integration gap disguised by a simulator?** No. Unconfigured adapters fail closed and every gap is
   listed as `BLOCKED_INTEGRATION`.
10. **A generic framework added without need?** No. Two small records, one derived projection, one ordered
    gate. No saga, workflow engine, ledger or payment aggregate.

---

## 21. Final P3-F Verdict

```text
P3-F FROZEN
```

---

## 22. Scope Confirmation

```text
P3-G EMD/Ancillary not started.
StoredValue not started.
Ledger integration not started.
Involuntary servicing not started.
Generic workflow/Saga framework not introduced.
```

No EMD aggregate, ancillary, wallet, ledger or involuntary code exists. Split tender and multiple forms of
payment were not implemented. Both mixed shapes satisfy the full recovery, crash and idempotency matrix, and
every inherited Even, AddCollect, Refund-Due and Residual regression is green.
