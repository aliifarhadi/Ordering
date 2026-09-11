# P3-F Negative-Balance Exchange — Refund-Due and Residual Value

Implementation report for negative-balance exchange and reissue.

Companion documents:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, now carrying `ICC-P3-EXCHANGE-REFUND-VALUE` and `ICC-P3-EXCHANGE-RESIDUAL`.
* [P3-F-ADDCOLLECT-EXCHANGE-REPORT.md](P3-F-ADDCOLLECT-EXCHANGE-REPORT.md) and [P3-F-ADDCOLLECT-FREEZE-GATE-CORRECTION-REPORT.md](P3-F-ADDCOLLECT-FREEZE-GATE-CORRECTION-REPORT.md) — the AddCollect bundle and its correction.
* [P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md](P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md) — partially-used even reissue.

---

## 1. Implementation Summary

An AirPrice-authoritative negative exchange balance is now fulfilled as one of two distinct outcomes, over
every already-supported document shape: fully unused, partially used and repeated A→B→C lineage.

* **Refund-Due** returns value to the original refundable source, through the **existing P3-D
  return-of-value port**, evolved additively rather than duplicated.
* **Residual** preserves value in a provider-authoritative instrument, through a new narrow
  `IExchangeResidualValuePort`.

Both settle only **after** the document host has authoritatively confirmed the successor, both follow the
same recovery discipline as the frozen rails, and both gate local finalization on exact provider evidence.

`ChangeMonetaryOutcome.Refund` and `.Residual` already existed and were used exactly as they are. No
persisted enum was renamed or renumbered.

---

## 2. Benchmark Coverage

A negative balance is not one thing, and the implementation refuses to pretend otherwise.

* Ordering never chooses between refund and residual. AirPrice decides, and Ordering executes the accepted
  disposition. There is no Ordering-side rule that could pick one.
* Residual stays provider-neutral. The instrument family is one of `Unknown`, `Mco`, `Emd`, `Voucher`,
  `TravelCredit` or `Other`, carried through as stored provider intent. Nothing in Exchange hardcodes MCO or
  EMD as the universal model, and no EMD servicing was added — that remains P3-G's, which can enrich this
  outcome later without touching Exchange.
* Forfeit was not implemented as a new commercial decision, because no accepted result represents it today.
* A future servicing screen can therefore show `Even`, `Additional Collection`, `Refund Due` or
  `Residual Credit` after repricing, then watch document status and money status move independently.

---

## 3. Frozen Monetary Semantics

| Outcome | Supported | Settlement rail | Local finalization gate |
| --- | --- | --- | --- |
| `Even` | Yes | none | document confirmed |
| `AddCollect` | Yes | funding guarantee then capture | document confirmed and capture confirmed with exact evidence |
| `Refund` | Yes, new | return of value to the original source | document confirmed and payout confirmed with exact evidence |
| `Residual` | Yes, new | residual instrument fulfilment | document confirmed and instrument confirmed with exact evidence |
| `Mixed` | No, deferred | none | not applicable |

Exactly one settlement may accompany an outcome. An accepted result that carries an add-collect amount on a
refund outcome, or any other mismatch, is malformed.

---

## 4. Refund-Due Rail

**Reuse decision.** The existing `IRefundValuePort` from P3-D is the one shared monetary-return boundary in
Ordering, and Exchange now uses it. No `IExchangeRefundPort` was created.

P3-D already expressed the destination the way this bundle needs it: the disposition string
`OriginalFormOfPayment`. What it could not do was let a caller verify the confirmation, because the result
carried no amount or currency. The port was therefore evolved **additively**:

| Record | Added |
| --- | --- |
| `RefundValueRequest` | `SuccessorDocumentNumber`, `SourcePricingReference` |
| `RefundValueResult` | `Amount`, `CurrencyId`, `Disposition` |
| `RefundValueRecovery` | `Amount`, `CurrencyId`, `Disposition`, `AsResult()` |

All optional, all ignored by P3-D, whose tests are unchanged and green.

Sequence, with the payout strictly after document confirmation:

```text
accept  ->  persist plan  ->  eligibility  ->  inventory  ->  document exchange
        ->  persist confirmation and raw successor evidence
        ->  return of value  ->  local finalization
```

The caller supplies nothing monetary: no amount, no currency, no destination. The command identifies the
quote, the changed services and the concurrency, exactly as before.

---

## 5. Residual Rail

A new narrow boundary, `IExchangeResidualValuePort`, with `FulfillAsync` and `RecoverAsync`. Residual is
deliberately **not** routed through the cash return-of-value port, and case V asserts that a residual exchange
touches neither the funding rail nor the refund rail.

The request carries only immutable accepted evidence: operation key, order, servicing operation, quoted
exchange, predecessor and successor document numbers, beneficiary traveller, exact amount, currency, accepted
disposition, expected instrument family and source pricing reference. Correlation is by provider-native
document numbers plus the Ordering-supplied key; no Ordering surrogate keys and no mutable order state cross
the boundary.

A confirmation must identify the value it created: a provider reference, an instrument reference, an
instrument family, and the exact amount and currency. A `Confirmed` with no identifiable instrument is
useless for later servicing and fails closed into reconciliation. Case AC proves it.

No residual aggregate, EMD aggregate or EMD service was added. Case W asserts a residual exchange creates no
miscellaneous document at all.

---

## 6. Final State Transition Tables

`fresh` means the document confirmation was recorded in this very attempt, so the provider cannot have seen
this operation. Any other entry reads back first and dispatches only on `WasDispatched = false`.

### Refund-Due

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, fresh | `RequestAsync` | `Confirmed` + exact evidence | persist, local finalization, `Completed` |
| none, fresh | `RequestAsync` | `Confirmed` + contradictory evidence | persist contradiction, `NeedsReconciliation`, no local commit |
| none, fresh | `RequestAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| none, fresh | `RequestAsync` | `Rejected` | persist, `NeedsReconciliation`, claim retained |
| `Pending` / `Unknown` | `RecoverAsync` | `WasDispatched = false` | `RequestAsync` under the same key |
| `Pending` / `Unknown` | `RecoverAsync` | `Confirmed` + exact | persist, local finalization only, no second document call |
| `Pending` / `Unknown` | `RecoverAsync` | `Confirmed` + contradictory | `NeedsReconciliation` |
| `Pending` / `Unknown` | `RecoverAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| `Pending` / `Unknown` | `RecoverAsync` | `Rejected` | persist, `NeedsReconciliation` |
| `Confirmed` | none | — | local finalization |
| `Rejected` | none | — | `NeedsReconciliation`, never retried |

### Residual

Identical discipline, with the instrument requirement added.

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, fresh | `FulfillAsync` | `Confirmed` + instrument + exact amount and currency | persist, local finalization, `Completed` |
| none, fresh | `FulfillAsync` | `Confirmed` without instrument, provider reference, or with wrong amount or currency | persist contradiction, `NeedsReconciliation` |
| none, fresh | `FulfillAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| none, fresh | `FulfillAsync` | `Rejected` | persist, `NeedsReconciliation`, claim retained |
| `Pending` / `Unknown` | `RecoverAsync` | `WasDispatched = false` | `FulfillAsync` under the same key |
| `Pending` / `Unknown` | `RecoverAsync` | `Confirmed` + exact | persist, local finalization only, same instrument |
| `Pending` / `Unknown` | `RecoverAsync` | `Confirmed` + contradictory | `NeedsReconciliation` |
| `Pending` / `Unknown` | `RecoverAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| `Pending` / `Unknown` | `RecoverAsync` | `Rejected` | persist, `NeedsReconciliation` |
| `Confirmed` | none | — | local finalization |
| `Rejected` | none | — | `NeedsReconciliation`, never retried |

Neither rail has a release or compensation path after a confirmed document, because the value is genuinely
owed to the customer at that point.

---

## 7. Domain and Application Changes

| Change | Location |
| --- | --- |
| `AcceptedRefundDue(Amount, CurrencyId, Disposition)` with the `OriginalFormOfPayment` constant | `Domain/OrderAggregate/AcceptedSource/Exchange/` |
| `AcceptedResidual(Amount, CurrencyId, Disposition, ExpectedInstrument)` | same folder |
| `AcceptedExchange` and `ExchangeQuote` carry optional `RefundDue` and `Residual` | same folder |
| `ResidualInstrumentKind`, `ExchangeMonetaryState` | `Contracts/AeroTech.Messages/Ordering/Enums/` |
| `Refund` and `Residual` become supported outcomes; `RequiresRefundDue`, `RequiresResidual`; one settlement per outcome, each validated against the line balance | `Domain/OrderAggregate/Policies/ExchangePricingPolicy.cs` |
| `IExchangeResidualValuePort` and its five records | `Domain/Ports/ExchangeResidual/` |
| `IRefundValuePort` evolved additively | `Domain/Ports/RefundValue/` |
| `ExchangeSettlementEvidencePolicy` for both negative-balance rails | `Domain/Servicing/Plans/Policies/` |
| Durable refund and residual evidence plus universal monetary readings on the plan | `Domain/Servicing/Plans/AcceptedExchangePlan.cs` |
| Two recorders on the plan store contract | `Domain/Servicing/Plans/Contracts/` |
| `SettleMonetaryAsync` dispatching per outcome, plus the refund and residual stages | `Application/.../Exchange/ExchangeService.cs` |

The AddCollect capture stage was not rewritten. The finalization gate changed from
`RequiresFunding && !IsFundingCaptured` to `RequiresMonetarySettlement && !IsMonetarySettled`, and
`SettleMonetaryAsync` routes to the capture, the payout or the residual by outcome.

The validation rule is consistency, never derivation: the accepted explicit amount stays authoritative, and
the pricing lines only prove the payload is internally coherent.

---

## 8. Persistence and Migrations

One migration, `20260911...P3FNegativeBalanceExchange`, applied to the dev database. Eight nullable columns on
`Order.AcceptedExchangePlans`:

```text
RefundDueOutcome             int
RefundDueReference           nvarchar(128)
RefundDueDetail              nvarchar(512)
ResidualOutcome              int
ResidualProviderReference    nvarchar(128)
ResidualInstrumentReference  nvarchar(128)
ResidualInstrument           int
ResidualDetail               nvarchar(512)
```

No existing column changed, no enum renumbered, and `Down` drops only the new columns, so it is losslessly
reversible and needs no downgrade guard.

Amounts and currencies are **not** duplicated as columns. They live in the immutable accepted plan JSON, which
already provides them unambiguously, so the two-condition persistence rule is not met.

---

## 9. Semantic Ports, Simulators and Contract Tests

| Port | Simulator | Unconfigured production | Reusable contract |
| --- | --- | --- | --- |
| `IRefundValuePort`, shared with P3-D | `DeterministicRefundValueAdapter`, upgraded | existing P3-D unconfigured provider | `Contracts/RefundValue/` |
| `IExchangeResidualValuePort` | `DeterministicExchangeResidualAdapter`, new | `UnconfiguredExchangeResidualProvider`, code 20293, HTTP 501 | `Contracts/ExchangeResidual/` |

Both simulators record one operation per key with its intent, replay it for an identical repeat, fail closed
on a conflicting intent, distinguish never-dispatched from dispatched, keep `Confirmed` and `Rejected` sticky,
preserve immutable evidence through read-back, and expose throw-before-dispatch, throw-after-dispatch and
throw-on-recover knobs plus wrong-amount, wrong-currency and missing-evidence overrides.

The refund simulator's upgrade preserved P3-D exactly, including its legacy `RecoveredAsDispatched` forcing
flag and its reference strings. One defect surfaced during the upgrade and was fixed: an accepted but
unresolved operation was losing its provider reference, so a later confirmed read-back looked contradictory.

No simulator is wired as a production fallback. Simulator success is not evidence of integration readiness.

---

## 10. API and Query Observability

No new API surface. `ExchangeOutcome`, already returned by the accept-exchange command through
`OrderChangeResponse`, gained seven provider-neutral fields:

```text
MonetaryAmount
MonetaryCurrencyId
MonetaryDisposition
MonetaryState
MonetaryProviderReference
ResidualInstrumentReference
ResidualInstrument
```

`MonetaryState` is universal — `NotRequired`, `Required`, `Pending`, `Settled`, `Rejected`, `Released` — and is
derived from the stored outcomes rather than being a second source of truth. The AddCollect-specific
`FundingState` and `AddCollectAmount` remain untouched for the frozen AddCollect projection.

A consumer can now distinguish, from the outcome alone:

| Situation | Reading |
| --- | --- |
| Reissue confirmed, refund pending | `Refund`, document `Exchanged`, monetary `Pending`, reconciliation false |
| Reissue confirmed, refund failed | `Refund`, document `Exchanged`, monetary `Rejected`, reconciliation true |
| Reissue confirmed, residual pending | `Residual`, document `Exchanged`, monetary `Pending`, instrument reference present |
| Reissue confirmed, residual failed | `Residual`, document `Exchanged`, monetary `Rejected`, reconciliation true |
| Refund completed | `Refund`, monetary `Settled`, successor ticket present |
| Residual created | `Residual`, monetary `Settled`, instrument reference and family present |

Case AH asserts refund and residual pending states are distinguishable. Nothing sensitive is exposed: no PAN,
no CVV, no token, no credential. The disposition is a provider-neutral string and the references are the
provider's own.

---

## 11. Edge Matrix A–AH

All pass. Refund cases A–R in `RefundDueExchangeFlowTests`, residual cases S–AH in
`ResidualExchangeFlowTests`, with Q and AE proved in the reusable port contracts.

| Case | Result | Case | Result |
| --- | --- | --- | --- |
| A fully unused refund | Pass | S fully unused residual | Pass |
| B partially used refund | Pass | T partially used residual | Pass |
| C AirPrice amount authority | Pass | U AirPrice amount authority | Pass |
| D malformed pricing, seven shapes | Pass | V correct semantic rail | Pass |
| E stale version | Pass | W no EMD implementation | Pass |
| F document unresolved, no payout | Pass | X pending then confirmed | Pass |
| G document and refund confirmed | Pass | Y unknown stays unresolved | Pass |
| H pending then confirmed | Pass | Z response lost, no duplicate | Pass |
| I unknown stays unresolved | Pass | AA wrong amount | Pass |
| J response lost, one payout | Pass | AB wrong currency | Pass |
| K wrong amount | Pass | AC missing instrument evidence | Pass |
| L wrong currency | Pass | AD refused | Pass |
| M missing provider evidence | Pass | AE same-key conflict | Pass, contract |
| N refused after confirmation | Pass | AF completed replay | Pass |
| O completed replay | Pass | AG repeated A→B→C | Pass |
| P repeated A→B→C | Pass | AH observability | Pass |
| Q same-key conflict | Pass, contract | | |
| R no refund before document | Pass | | |

Case D also covers a refund disposition that is not the original refundable source, and case M covers a
disposition the provider silently changed.

---

## 12. Regression Results

Cross-capability regression is explicit and green.

| Gate | Result |
| --- | --- |
| `dotnet build AeroTech.Ordering.sln` | Succeeded |
| Domain tests | 500 / 500 |
| Persistence tests | 817 / 817 |
| Refund-due matrix | 25 / 25 |
| Residual matrix | 16 / 16 |
| All port contract suites | 73 / 73 |

Even, AddCollect, fully-unused, partially-used and repeated exchange all remain green, as do the P3-D refund
suites whose shared port was evolved, and the AddCollect recovery corrections. Every exchange test asserts at
most one document exchange, one successor, one order change and one price change set per operation identity.
Revalidation is untouched.

Three tests that asserted `Refund` is deferred were retargeted to `Mixed`, now the only deferred outcome:
`ExchangeFlowTests.C4`, the multi-coupon deferral test, and the domain deferral theory.

---

## 13. Requirement → Code → Test Traceability

| Requirement | Production implementation | Test proving it |
| --- | --- | --- |
| AirPrice owns the amount; Ordering derives nothing | `ExchangePricingPolicy.EnsureSettlementIsWellFormed`, accepted amount used verbatim by `DispatchRefundDueAsync` and `DispatchResidualAsync` | `RefundDue…C_the_provider_refund_amount_is_preserved_exactly_end_to_end`, `Residual…U_the_provider_residual_amount_is_preserved_exactly_end_to_end` |
| Malformed negative balance fails before irreversible work | `ExchangePricingPolicy.EnsureMonetaryOutcomeIsWellFormed` | `RefundDue…D_malformed_refund_pricing_is_refused_before_any_external_mutation`, seven shapes |
| No refund before document confirmation | `FinalizeAsync` reached only from a confirmed document; `SettleMonetaryAsync` called from inside it | `RefundDue…F_R_an_unresolved_reissue_never_returns_value`, `RefundDue…R_the_refund_is_dispatched_only_after_the_document_is_confirmed` |
| Refund pending stays recoverable | `AfterMonetarySettlementAsync` unresolved branch → `AwaitingExternal` | `RefundDue…H_an_unresolved_refund_that_resolves_on_read_back_finalizes_once`, `…I_a_refund_that_stays_unresolved_is_read_back_and_never_paid_again` |
| Refund response loss recovers, pays once | recover-first in `SettleRefundDueAsync` keyed on `documentJustConfirmed` | `RefundDue…J_a_refund_confirmed_in_the_provider_survives_a_process_restart_and_pays_once` |
| Residual pending stays recoverable | same branch in `SettleResidualAsync` | `Residual…X`, `Residual…Y` |
| Residual response loss recovers the same instrument | recover-first plus instrument preserved by the simulator's `Resolved` | `Residual…Z_a_residual_created_in_the_provider_survives_a_process_restart_without_a_duplicate` |
| Confirmed evidence must match the obligation | `ExchangeSettlementEvidencePolicy` | `RefundDue…K_L_M`, `Residual…AA_AB_AC` |
| Same-key conflicting intent fails closed | intent fingerprints in both deterministic adapters | `RefundValuePortContract.An_operation_key_cannot_be_reused_for_a_different_obligation`, `ExchangeResidualPortContract.An_operation_key_cannot_be_reused_for_a_different_obligation` |
| Partial-used isolation preserved | unchanged `ExchangePreconditions` scope, unchanged plan coupons | `RefundDue…B`, `Residual…T` |
| One successor per operation | `FinalizeAsync` runs once behind the settlement gate | `RefundDue…O`, `Residual…AF`, plus every happy path asserting a single successor, order change and price change set |
| Local finalization gate | `plan.RequiresMonetarySettlement && !plan.IsMonetarySettled` | `RefundDue…N`, `Residual…AD`, contradiction theories asserting no local commit |
| Public projection distinguishes the rails | `ExchangeOutcome` monetary fields from `AcceptedExchangePlan.MonetaryState` | `Residual…AH_a_consumer_can_tell_residual_state_apart_from_cash_refund_state` |
| Residual never uses the cash or funding rail | separate ports, separate stages | `Residual…V_residual_never_touches_the_funding_or_cash_refund_rails` |
| No EMD servicing added | no aggregate or service touched | `Residual…W_a_residual_exchange_creates_no_miscellaneous_document` |

---

## 14. BLOCKED_DEVELOPMENT

```text
BLOCKED_DEVELOPMENT:
None.
```

Every semantic was resolvable from the brief, the frozen P3 behaviour and the existing code. No commercial
rule was invented: Ordering does not choose between refund and residual, does not implement forfeit, and does
not decide an instrument family.

---

## 15. BLOCKED_INTEGRATION

New, for refund-due return of value against real Payment:

1. **Exact obligation evidence on a confirmation** — amount, currency and a value-movement reference.
   Unverified. An adapter must not echo the request, or the check is tautological.
2. **Read-back by caller key** preserving that operation's evidence. Unverified.
3. **Same-key idempotency failing closed on conflicting intent.** Unverified, and not addable client-side.
4. **Original-source semantics** without Ordering naming a destination. The disposition exists in the P3-D
   contract but is unverified against a real provider.

New, for residual fulfilment:

5. **Integration ownership is itself unverified.** The real owner could be the document host, Payment, a
   stored-value service or another provider, and may vary by market or disposition. Ordering's semantic
   requirement is frozen; the owner is not asserted.
6. **Authoritative instrument identity** that later servicing can use. An adapter cannot mint one.
7. **Read-back by caller key preserving the instrument.** Unverified; without it a lost response risks a
   duplicate accountable instrument.
8. **Same-key idempotency failing closed.** Unverified.
9. **Instrument family mapping** per market. Unverified; Ordering carries the family provider-neutrally.

Carried forward unchanged:

```text
TicketCouponFinancialStatus.Used production ingestion
consumed-operational-segment evidence
AirPrice exchange contract verification, including the explicit authoritative amount
Inventory exchange recovery and idempotency verification
Document host subset-reissue and recovery verification
TicketCouponControlStatus writer
real Payment AddCollect funding adapter
```

None of these block Ordering-first implementation.

---

## 16. Deferred

```text
Mixed / netted monetary exchange outcomes   deferred, and did not delay this bundle
optional forfeit workflows                  deferred, no accepted result represents it
wallet and travel-bank residual handling    deferred
generic EMD and MCO lifecycle (P3-G)        deferred
ancillary exchange (P3-G)                   deferred
involuntary servicing                       deferred
real external ACL verification              deferred
```

The residual outcome is deliberately shaped so P3-G can enrich it — attaching a real EMD or MCO lifecycle to
an existing instrument reference — without redesigning the Exchange aggregate or orchestration.

---

## 17. No-Redesign-Risk Check

```text
Yes.
```

A future Backoffice, OTA or public API can present one professional exchange flow. After repricing it reads
`MonetaryOutcome` to show Even, Additional Collection, Refund Due or Residual Credit, and the accepted amount
and currency alongside. It then observes four independent axes on the same outcome: document status through
`DocumentOutcome` and the successor document number, money or value status through `MonetaryState` and the
provider-safe reference, the successor ticket through `SuccessorElectronicTicketId`, and reconciliation
through `RequiresReconciliation` and `OperationStatus`.

Adding `Mixed` later means adding a settlement record and one branch in `SettleMonetaryAsync`. It does not
require changing the aggregate, the plan schema, the port set or the projection.

---

## 18. Scope Confirmation

```text
Mixed Exchange not started.
StoredValue not started.
Generic EMD servicing not started.
Ancillary Exchange not started.
Involuntary servicing not started.
```

`Mixed` remains deferred by `ExchangePricingPolicy.DeferralReason` and is asserted as the only deferred
outcome. No wallet, ledger, EMD aggregate, ancillary or involuntary code exists. Refund-Due and Residual both
satisfy the full recovery, crash and idempotency matrix, and every Even and AddCollect regression is green.
