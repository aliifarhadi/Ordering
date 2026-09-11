# P3 Integration Capability Catalog

Living catalog of the integration capabilities that Ordering depends on for P3 servicing.

Each entry states **Ordering's own semantic requirement** and the **Ordering-owned boundary** that expresses it.
No entry documents an external service's current API as if it were the Ordering contract. Where a heading does
not apply, it says `N/A — <reason>` and is never removed silently.

Status vocabulary:

| Status | Meaning |
| --- | --- |
| `VERIFIED_DETERMINISTIC` | The semantics are implemented and covered by deterministic simulator plus contract tests inside Ordering. |
| `BLOCKED_INTEGRATION` | The real provider's capability is unverified from inside Ordering. Does not block the deterministic Ordering capability. |
| `BLOCKED_DEVELOPMENT` | An unresolved Ordering business semantic. Blocks implementation. |

Entries in this revision: `ICC-P3-EXCHANGE-AIRPRICE`, `ICC-P3-EXCHANGE-FUNDING`,
`ICC-P3-EXCHANGE-REFUND-VALUE`, `ICC-P3-EXCHANGE-RESIDUAL`, `ICC-P3-EXCHANGE-INVENTORY`,
`ICC-P3-EXCHANGE-DOCUMENT`, `ICC-P3-EXCHANGE-USAGE`.

Capability scope of this revision: **even, add-collect, refund-due and residual reissue**, each over both
supported exchange shapes (fully unused and partially used) and over repeated A→B→C lineage. `Mixed` remains
deferred.

For a partially-used predecessor — at least one `Used` coupon and at least one `Open` coupon — the reissue
scope is **all** `Open` coupons and `Used` coupons are historical pricing context only. The governing
invariant is:

```text
pricing context != document mutation scope
```

For an add-collect reissue the governing invariant is:

```text
no successor document is knowingly issued without funding assurance
and no crash or replay can charge the customer twice
```

---

## ICC-P3-EXCHANGE-AIRPRICE

### Capability

Exchange quote and acceptance for a voluntary even reissue, including the partially-used case.

### Authoritative Owner

AirPrice (pricing engine). AirPrice owns fare re-pricing, reissue value transfer, penalty and residual
determination, and the monetary outcome classification.

### Ordering Semantic Requirement

Ordering must obtain a priced, provider-determined exchange plan without performing any pricing itself.
Ordering never calculates `FareUsed`, fare, tax, penalty, residual, add-collect or FX. Ordering must be able
to state, separately and explicitly, four kinds of evidence in one request:

1. the **Open exchange scope** that will actually be reissued;
2. the **historical `Used` context** that must inform pricing but must never be mutated;
3. the **existing predecessor pricing correlation evidence** (`XPL-…` correlation refs);
4. the **stored fare-construction snapshot**, passed through as stored facts with no inference and no
   calculation. Absent or empty is valid.

Ordering must reject any accepted plan whose coupon set is not exactly the Open reissue scope.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/Exchange/IExchangeQuotePort.cs`

```text
QuoteAsync(ExchangeQuoteRequest)                        -> ExchangeQuote
AcceptQuotedExchangeAsync(AcceptedQuotedExchangeSelection) -> AcceptedExchange
```

The request and result records live beside the interface. They are Ordering-owned vocabulary, not an
AirPrice wire contract.

### Request Evidence

`ExchangeQuoteRequest` carries:

| Field | Meaning |
| --- | --- |
| `OrderId`, `CommercialVersion` | Order identity and the commercial version the quote is bound to. |
| `PredecessorElectronicTicketId`, `PredecessorDocumentNumber` | Predecessor document identity. |
| `ChangedOrderServiceIds` | The services the caller is changing. |
| `ExchangeScope` | `ExchangeScopeCoupon[]` — every `Open` coupon, with `ServiceIsChanging`, `CurrentSegment` and `IssuedSegment`. |
| `HistoricalUsedCoupons` | `HistoricalUsedCoupon[]` — `Used` coupons with `IssuedSegment` and, when it differs, `CurrentBoundSegment`. |
| `PredecessorPricing` | `PredecessorPricingEvidence[]` — existing correlation refs (`XPL-` + SHA256(ticketId:lineId)), component type, code, sale amount and attributed value. |
| `FareConstructions` | `FareConstructionContext[]` — provider-neutral stored snapshot of groups, units and components. |
| `SaleCurrencyId` | Sale currency of the order. |

The two coupon collections are disjoint by construction: `ExchangePreconditions` builds `ExchangeScope` from
the reissue scope and `HistoricalUsedCoupons` from coupons whose financial status is `Used`.

### Outcome Semantics

`ExchangeQuote` and `AcceptedExchange` report `ChangeMonetaryOutcome`, provider pricing lines, per-coupon
disposition (`Replaced` / `Continued`), a replacement for every `Replaced` coupon, and a successor coupon
value with its price links. This bundle accepts only `Even`. `PricingSource.OrderingDerived` is never a
valid answer and is asserted against in the contract tests.

**Even is defined by the customer balance, not by a gross ledger balance.** The frozen rule is the one
`ExchangePricingPolicy.NetCustomerBalance` already implements: sum the signed sale amounts of the lines whose
`PricingEffect` is `CustomerBalance`, and require zero. Informational, accounting and source-explanatory
lines are not required to gross-balance against each other. The reusable contract calls that policy method
directly rather than carrying a second balance algorithm.

**Quote being side-effect free is not the same as two independent quotes being identical.** Two independent
`QuoteAsync` calls may legitimately return different quote ids, different expiry, refreshed source pricing or
a changed source-authoritative result. The invariant that binds a real adapter is only that each answer stays
bound to the order, commercial version, predecessor document and requested scope it was asked about. Identity
equality across independent quotes is a property of the deterministic simulator alone and is asserted only in
its own adapter-specific test.

**The monetary outcome and its amount are AirPrice's alone.** AirPrice decides whether an exchange is `Even`,
`AddCollect`, `Refund` or `Residual`, and it decides the exact amount and currency. Ordering never computes
old fare minus new fare, `FareUsed`, a tax difference, a penalty, refundability, a residual, FX or a customer
entitlement, and never chooses between a refund and a residual. That choice is commercial and provider
authority; Ordering executes the accepted disposition.

**A negative balance is authoritative, never derived.** For `Refund` the accepted result must carry
`AcceptedRefundDue(Amount, CurrencyId, Disposition)`, and the disposition must be the original refundable
source. For `Residual` it must carry `AcceptedResidual(Amount, CurrencyId, Disposition, ExpectedInstrument)`,
where the expected instrument is a provider-neutral family — `Unknown`, `Mco`, `Emd`, `Voucher`,
`TravelCredit` or `Other` — and Ordering hardcodes none of them as the universal model. In both cases
Ordering validates only internal consistency: the amount is present and strictly positive, the currency is
the sale currency, exactly one settlement accompanies the outcome, and the net customer balance of the
accepted pricing lines equals the negative of the settled amount. A disagreement is malformed provider
evidence and fails closed with code 20275 before any inventory, document or monetary call.

**AddCollect is authoritative, never derived.** When the outcome is `ChangeMonetaryOutcome.AddCollect`, the
accepted result must carry an explicit `AcceptedAddCollect(Amount, CurrencyId)`. Ordering preserves those two
values exactly and never recalculates, infers, normalizes, splits, rounds, converts or reconstructs the amount
from pricing lines. It validates only that the authoritative amount is coherent: present, strictly positive,
in the sale currency, and equal to `ExchangePricingPolicy.NetCustomerBalance` of the accepted pricing lines. A
disagreement is malformed provider evidence and fails closed with code 20275 before any funding, inventory or
document call. `FareUsed`, fare difference, taxes, penalties and fees remain entirely AirPrice's to compute.

Penalty and fee pricing lines are legitimate in an add-collect result and are exactly what usually produces
one. They remain a deferral reason for an `Even` outcome, where they would contradict the zero customer
balance. `Refund`, `Residual` and `Mixed` stay deferred.

**Historical evidence may inform the calculation; it must not be attributed as transferred value.** A
source-authoritative AirPrice result may legitimately reference historical or `Used` value for valuation
context, `FareUsed`-related output, audit and explanation, or other source-authoritative decomposition, and
Ordering must not refuse it merely because the predecessor coupon is `Used`. What is forbidden is the
mechanical attribution of that value into the successor: resolving a `SuccessorDocumentPriceLink.SourceLineRef`
to its `AcceptedExchangePricingLine`, any line that is a predecessor-value `Transfer` carrying a
`PredecessorCorrelationRef` must correlate to the actual `Open` exchange scope. Unattributed
source-explanatory lines may reference historical evidence freely. Ordering still calculates none of it.

### Identity and Correlation

Correlation to predecessor value uses the Ordering-owned `ExchangePricingCorrelation` ref
(`XPL-` + SHA256(ticketId:lineId)) carried on each pricing line as `PredecessorCorrelationRef`, plus
`TransferGroupId` for the transfer pair. Coupon correlation is by predecessor ticket-coupon id and
predecessor coupon number. No AirPrice-local primary key is stored as Ordering identity.

### Idempotency / Stable Operation Identity

Acceptance uses the durable operation rail: `OrderOperationCoordinator.ProviderOperationKey(operation, "exchange-quote")`
yields `exchange-quote:{operationId}`. The same operation key with the same acceptance intent must answer the
same accepted plan. The same operation key with a conflicting intent must fail.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

`N/A — acceptance is a synchronous pricing decision, not a dispatched external mutation.` Acceptance either
returns an accepted plan or throws. There is no pending/unknown acceptance state; a failed acceptance leaves
no persisted plan and retains the claim.

### Recovery / Read-back

`N/A — nothing irreversible happens at acceptance.` Recovery of an interrupted acceptance is re-acceptance
under the same operation key, which must be replay-safe. The accepted plan is persisted
(`AcceptedExchangePlan`) only after a successful acceptance, and every later stage reads the persisted plan
rather than re-asking AirPrice.

### WasDispatched Requirement

`N/A — no dispatch semantics at this boundary.` See **Recovery / Read-back**.

### Atomicity / Coupling

Quoting is side-effect free from Ordering's perspective: it persists nothing and claims nothing. Acceptance
and the persistence of `AcceptedExchangePlan` happen before any inventory or document mutation, so a pricing
failure cannot leave a partially mutated document.

### Irreversible-Step Ordering

```text
preflight (local, fail-closed)
  -> AirPrice acceptance  [this capability]
  -> persist AcceptedExchangePlan
  -> document eligibility
  -> inventory mutation
  -> document exchange
  -> single local finalization transaction
```

Acceptance is the first external call and the last fully reversible one.

### Deterministic Simulator

`src/AeroTech.Ordering.Providers.Deterministic/DeterministicExchangeQuoteAdapter.cs` with the composition
fixture `tests/AeroTech.Ordering.Domain.Tests/_Shared/ExchangeSourceFactory.cs`.

The simulator observes the exchange scope, the historical `Used` context, the predecessor pricing evidence
and the fare-construction context. It produces a deterministic valid partially-used `Even` outcome. It
implements no ATPCO Cat 31, no `FareUsed` calculation, and no tax, fare or penalty rules.

Its behaviour is a **deliberately simple Even fixture, not a universal AirPrice contract**. Two simulator
choices in particular must not be read as provider obligations:

1. it filters predecessor pricing evidence down to the scope coupon numbers before building transfer lines,
   so no historical correlation ref appears on any of its lines at all;
2. it answers the same quote id and the same lines for the same request every time.

A real adapter is held only to the semantics in **Outcome Semantics** above. Both simulator properties are
asserted in `DeterministicExchangeQuotePortTests`, deliberately outside the reusable contract.

### Consumer Contract Tests

`tests/AeroTech.Ordering.Persistence.Tests/Contracts/ExchangeQuote/`

* `ExchangeQuotePortContract.cs` — the reusable semantic assertions. Collections are compared by domain
  identity, never by enumeration order: the changed scope as a set of order-service ids, the coupon scope by
  coupon number, and replay equivalence by `SourceLineRef`.
* `ExchangeQuotePortFixture.cs` — the canonical partially-used request (one `Used` historical coupon, one
  continued `Open` coupon, one changed `Open` coupon) and a variant carrying a stored fare construction. The
  request deliberately carries predecessor pricing evidence for the `Used` coupon as well, so the
  no-mechanical-attribution rule is actually exercised.
* `DeterministicExchangeQuotePortTests.cs` — binds the contract to the deterministic adapter and adds the two
  simulator-only properties described above.

A future ACL adapter satisfies the same base class.

Ordering-visible side-effect freedom is proved where it actually belongs, at the application boundary:
`PartiallyUsedExchangeFlowTests.A_partially_used_quote_leaves_no_ordering_visible_trace_however_often_it_is_asked`
quotes three times and proves no servicing operation, no `AcceptedExchangePlan`, no `OrderChange`, no
`PriceChangeSet`, no inventory or document call, no successor ticket, and no movement in commercial version,
financial sequence, obligation version, customer total or coupon state. AirPrice remains free to persist its
own quote.

Flow-level coverage lives in `PartiallyUsedExchangeFlowTests` (cases B, C, D, K, L) and
`ExchangeFlowTests`.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — no real AirPrice exchange adapter exists inside Ordering. `Providers/Unconfigured/`
holds the fail-fast placeholder. The real AirPrice repository was deliberately not inspected in this phase.

### BLOCKED_INTEGRATION

1. Whether AirPrice can accept an explicit Open-scope / historical-`Used`-context split in one request.
2. Whether AirPrice can consume the stored fare-construction snapshot in this shape.
3. Whether AirPrice honours an Ordering-supplied operation key with replay-safe acceptance.
4. Whether AirPrice returns a partially-used `Even` outcome at all, or always prices an add-collect.
5. Whether AirPrice returns an explicit authoritative add-collect amount and currency alongside its pricing
   lines, rather than leaving the caller to total the lines. Ordering requires the explicit value and will not
   derive it, so an adapter cannot fill this gap by summing lines — that would silently make Ordering the
   pricing authority.

### Known Semantic Gaps

* `Even`, `AddCollect`, `Refund` and `Residual` are accepted. `Mixed` and netted outcomes remain out of scope,
  as does any forfeit disposition, which Ordering will not invent as a commercial decision.
* `FareConstructions` is a pass-through snapshot. Ordering does not validate it against the reissue scope and
  intentionally omits `BrandName`, `CreatedAt`, foreign keys and line items from the projection.
* The historical context carries no consumed-operational-segment evidence. See `ICC-P3-EXCHANGE-USAGE`.

### Explicit Non-Responsibilities

Ordering does not price, re-price, calculate `FareUsed`, apply fare or tax rules, compute penalties or
residual value, perform FX, or derive a monetary outcome. `PricingSource.OrderingDerived` is never produced,
and is rejected outright by `EnsureWellFormed`. Moving money is not this capability's concern either — see
`ICC-P3-EXCHANGE-FUNDING`. No stored value, wallet or ledger movement belongs to any capability in this
revision.

---

## ICC-P3-EXCHANGE-FUNDING

### Capability

Collecting the authoritative add-collect amount for a voluntary reissue.

### Authoritative Owner

Payment. Payment owns the actual movement, protection, collection and reversal of money, the tender, the
provider attempt and every acquirer-facing rule.

### Ordering Semantic Requirement

Ordering owns only the semantic funding requirement, the orchestration, the stable operation identity, the
immutable accepted instruction, the durable external outcome and the reconciliation state.

The requirement is a **two-stage funding contract**:

1. **Guarantee** the exact accepted amount before the document host is asked to reissue;
2. **Capture** it after the document host has authoritatively confirmed the successor document;
3. **Release** the guarantee if the exchange definitely terminates before document confirmation.

The decisive rules are:

* no successor document is knowingly issued without confirmed funding assurance;
* a crash or a replay can never charge the customer twice;
* a definite downstream failure can never silently leave the customer overcollected;
* `Pending` and `Unknown` are recoverable states, never failures;
* Ordering never invents a provider guarantee, and an adapter may translate protocol or shape but must never
  fabricate a Payment capability that does not exist.

**Chosen model and rationale.** The two-stage guarantee-then-capture model was chosen over one-step
collection plus compensation, for three reasons. First, it is the model the shared contracts already
anticipate: `Contracts/AeroTech.Messages/JetPay/` carries `RequiredGuarantee.AuthorizedBeforeIssuance` and
`PaidBeforeIssuance`, a `PaymentIntentStatus` running `Guaranteed → CommittedForIssuance → Capturing → Paid`
with `PaidUnapplied` and `Exception` beside it, `InstructionType.Issuance` and `DocumentOutcome`, and the
integration events `PaymentIntentGuaranteed` and `PaymentCompleted`. The staging is therefore aligned with an
existing design, not invented here. Second, a guarantee is reversible by an independent release operation,
whereas a completed collection would require a compensating refund that this revision explicitly does not
implement. Third, it keeps the window in which the customer's money is committed but the document is not yet
issued as short as the document call itself.

**Stage placement.** The guarantee is dispatched **after document eligibility and before the inventory
mutation**. The reason is that the inventory mutation is the first step this bundle cannot reverse within its
own scope, while the guarantee is reversible by design. Guaranteeing first means every definite failure after
that point — inventory rejected, document rejected, document denied — has one defined release path, and no
capacity is mutated for an exchange the customer cannot fund. The alternative, guaranteeing after inventory,
would leave a confirmed capacity change stranded whenever funding is refused, which is the "silent partial
success" the acceptance matrix forbids.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/ExchangeFunding/IExchangeFundingPort.cs`

```text
GuaranteeAsync(ExchangeFundingGuaranteeRequest) -> ExchangeFundingResult
RecoverGuaranteeAsync(ExchangeFundingRecoveryRequest) -> ExchangeFundingRecovery
CaptureAsync(ExchangeFundingCaptureRequest)     -> ExchangeFundingResult
RecoverCaptureAsync(ExchangeFundingRecoveryRequest)   -> ExchangeFundingRecovery
ReleaseAsync(ExchangeFundingReleaseRequest)     -> ExchangeFundingResult
RecoverReleaseAsync(ExchangeFundingRecoveryRequest)   -> ExchangeFundingRecovery
```

This is Ordering-owned vocabulary. It is deliberately **not** shaped around the current Payment service API.
The existing `IPaymentProvider.CaptureAsync` returns only a reference and a timestamp, with no outcome, no
pending or unknown state, no read-back and no dispatch knowledge, and `IFundingCoveragePort` verifies coverage
of an existing obligation with no recovery semantics. Neither satisfies this contract, and neither was
extended to pretend otherwise.

### Request Evidence

| Stage | Evidence |
| --- | --- |
| Guarantee | operation key, order id, operation id, quoted exchange id, predecessor document number, payer traveller id, exact amount, currency, funding-method reference |
| Capture | operation key, order id, operation id, quoted exchange id, **successor document number**, guarantee reference, exact amount, currency |
| Release | operation key, order id, operation id, quoted exchange id, guarantee reference, `ExchangeFundingReleaseReason` |
| Recovery | operation key, order id, operation id |

The capture carries the successor document number as its economic justification: the money is captured
because that document exists. The funding-method reference is an opaque safe reference supplied by the
caller. No credential, card number, CVV or unrestricted token is accepted, persisted or logged.

### Outcome Semantics

`ProviderOperationOutcome` — `Confirmed`, `Pending`, `Unknown`, `Rejected` — plus an optional provider
reference and the amount and currency the provider acted on. A confirmed guarantee must name a provider
reference. A rejected outcome carries none. Provider-neutral state is projected as `ExchangeFundingState`:
`NotRequired`, `GuaranteeRequired`, `GuaranteePending`, `Guaranteed`, `GuaranteeRejected`, `CapturePending`,
`Captured`, `CaptureRejected`, `ReleasePending`, `Released`.

### Identity and Correlation

Correlation is by the Ordering-supplied operation key plus the provider's own reference, which Ordering
stores but never interprets. Ordering never adopts a Payment primary key as its own identity.

### Idempotency / Stable Operation Identity

Each stage owns its own stable key, derived internally before first dispatch:

```text
exchange-funding-guarantee:{predecessorElectronicTicketId}:{operationId}
exchange-funding-capture:{predecessorElectronicTicketId}:{operationId}
exchange-funding-release:{predecessorElectronicTicketId}:{operationId}
```

The same key with the same intent must answer the same authoritative result. One key can never consume
another operation's result.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

All five are modelled and all five are load-bearing. `WasDispatched = false` is the only safe basis for a
fresh dispatch. `Rejected` at the guarantee stage is terminal and mutates nothing. `Pending` and `Unknown` at
the guarantee stage hold the claim in `AwaitingExternal` and block the document call. `Pending`, `Unknown` or
`Rejected` at the capture stage, which can only happen after the document is confirmed, produce
`NeedsReconciliation`.

### Recovery / Read-back

Recover-first, per stage, always. Before any redispatch the rail calls the matching `Recover*` operation under
the same key. A recovery answer for a key the provider never saw must report `WasDispatched = false` and must
not report `Confirmed`. A guarantee that the provider confirmed but whose answer never reached Ordering is
recovered as `Confirmed`, never repeated.

### WasDispatched Requirement

Required, and the single most important element of this contract. Without it, a lost response is
indistinguishable from a request that never arrived, and the only safe behaviours left are to never retry, or
to risk double-charging the customer.

### Atomicity / Coupling

Each stage's outcome is persisted on `AcceptedExchangePlan` before the next stage is attempted. The plan is
the single durable record of the economic position, so a crash resumes from the last persisted stage rather
than reissuing a financial instruction.

### Irreversible-Step Ordering

```text
accept AirPrice add-collect  ->  persist plan and every stable operation key
  ->  document eligibility
  ->  funding guarantee                     [reversible by release]
  ->  inventory mutation
  ->  document exchange                     [irreversible]
  ->  persist document confirmation and raw successor evidence
  ->  funding capture
  ->  single local finalization transaction
```

Release paths: inventory rejected, or document rejected or denied, release the guarantee under its own key
and then settle terminally or reconcile as the frozen rail already does. There is no release path after a
confirmed document, because the money is then genuinely owed.

### Economic uncertainty after a confirmed document

Once the document host has authoritatively confirmed the successor, Ordering never pretends a later Payment
problem rolls the reissue back. It does not create another successor, does not retry the document exchange,
does not restore the predecessor, and does not report the operation as if no exchange occurred. It persists
the truth — document confirmed, successor identity known, settlement unresolved or refused — and requires
reconciliation. The local finalization has not run, so no successor ticket exists locally and the predecessor
is untouched; the confirmed provider evidence on the plan is what an operator reconciles from.

### Deterministic Simulator

`src/AeroTech.Ordering.Providers.Deterministic/DeterministicExchangeFundingAdapter.cs`. It records one
operation per key, replays it for a repeated call, distinguishes never-dispatched from dispatched, keeps
`Confirmed` and `Rejected` sticky across read-back while letting `Pending` and `Unknown` resolve, and offers
throw-before-dispatch and throw-after-dispatch knobs for the crash boundaries. It is a development and test
substitute and is **not** evidence of production readiness.

### Consumer Contract Tests

`tests/AeroTech.Ordering.Persistence.Tests/Contracts/ExchangeFunding/` —
`ExchangeFundingPortContract.cs` holds the reusable semantic assertions across all three stages and both
recovery directions, `ExchangeFundingPortFixture.cs` the canonical requests and keys, and
`DeterministicExchangeFundingPortTests.cs` binds them to the simulator and adds the crash-boundary and
sticky-outcome properties. A future Payment ACL satisfies the same base class.

Flow-level coverage is `AddCollectExchangeFlowTests`, cases A through X.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — no real Payment adapter for exchange funding exists inside Ordering.
`Providers/Unconfigured/UnconfiguredExchangeFundingProvider.cs` fails closed on all six operations with code
20263 and HTTP 501. The presence of a passing deterministic simulator does not make Payment integration
ready.

### BLOCKED_INTEGRATION

1. **Read-back by caller key, for all three stages.** Required: given the operation key Ordering generated,
   Payment must answer whether it ever saw that operation and what the authoritative outcome is, preserving
   that operation's amount, currency and provider reference. Unverified: the JetPay contracts in this
   repository describe an asynchronous instruction-and-fact flow with no evidence of a query keyed by a
   caller-supplied operation id. An adapter cannot invent this — without it, a lost response leaves Ordering
   unable to distinguish never-dispatched from unknown, and the only safe behaviour is to stop, which is what
   the rail does today. Must be verified or added during real integration for the guarantee, the capture and
   the release alike.
2. **Exact obligation evidence on a confirmation.** Required: a `Confirmed` guarantee or capture states the
   amount, the currency and a provider reference, so Ordering can prove the provider acted on the obligation
   AirPrice priced. Unverified, and the existing `IPaymentProvider` returns none of them in a usable form. An
   adapter must not synthesize the amount from the request it just sent; that would make the check
   tautological and defeat its only purpose, which is detecting a provider that acted on something else.
3. **Two-stage guarantee then capture.** Required: authorize or protect an exact amount, then capture it
   later against the successor document, then release it if the exchange dies first.
   `RequiredGuarantee.AuthorizedBeforeIssuance` and the `Guaranteed → CommittedForIssuance → Capturing → Paid`
   progression suggest this exists in the JetPay design, but no implemented Ordering-facing operation was
   verified. An adapter must not simulate a guarantee by capturing immediately; that would convert a
   reversible step into an irreversible one behind Ordering's back.
4. **Same-key idempotency on money operations.** Required: re-sending a guarantee or capture under an
   already-used key must never move money a second time. Unverified. An adapter cannot safely add this on
   the client side, because a client-side dedupe cache is lost exactly when the process crashes.
5. **Release semantics.** Required: an independently recoverable release that is safe to call once, twice, or
   after an unknown outcome. Unverified.
6. **Capture after an authoritative document.** Required: a reliable completion path for a previously
   guaranteed amount. If Payment cannot guarantee completion after authorization, the reconciliation state
   this bundle persists is the correct terminal representation and an operator must resolve it.

### Known Semantic Gaps

* Only `AddCollect` is funded. `Refund`, `Residual` and `Mixed` are out of scope, so no refund or reversal
  instruction exists in this revision.
* Stored value, wallet and ledger integration are out of scope; the funding-method reference is opaque.
* Reconciliation is represented durably but is not automated. There is no background settlement poller in
  this revision.
* A guarantee left `Pending` or `Unknown` is held rather than expired; no guarantee-expiry handling exists
  yet, although `PaymentIntentGuaranteed.EarliestGuaranteeExpiry` shows the provider design expects one.

### Explicit Non-Responsibilities

Ordering does not select tenders, authenticate cardholders, handle 3-D Secure, retry acquirers, price
currency conversion, settle, reconcile acquirer files, or hold payment credentials. It does not decide whether
money can move; it states the amount, when assurance is required, and what the document outcome was.

---

## ICC-P3-EXCHANGE-REFUND-VALUE

### Capability

Returning value to the original refundable source when an exchange reprices negative.

### Authoritative Owner

Payment, as the owner of every movement, protection and reversal of money.

### Ordering Semantic Requirement

Ordering owns the semantic obligation, the orchestration, the stable operation identity, the immutable
accepted evidence, the durable provider outcome and the reconciliation state. It never chooses the amount, the
currency or the destination.

The only supported destination in this revision is the **original refundable source**, expressed
provider-neutrally as the disposition `OriginalFormOfPayment` that P3-D already uses. Ordering accepts no
arbitrary bank, card or payment destination from the caller and holds no payment instrument details.

**Refund only after the document is authoritatively confirmed.** A failed or unresolved reissue must never
pay value back while the original accountable document may still be usable.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/RefundValue/IRefundValuePort.cs` — **the existing P3-D return-of-value
boundary, reused rather than duplicated.** There is one shared semantic monetary-return port in Ordering, not
a refund port per operation.

It was evolved additively, with no change to P3-D behaviour: the request can now name the exchange successor
document and the source pricing reference, and a result or recovery can now state the `Amount`, `CurrencyId`
and `Disposition` it acted on, plus an `AsResult()` helper matching the other rails. Those fields are optional
and P3-D ignores them. The evolution was necessary because the original confirmation carried no amount or
currency, so an exchange could not have verified that the provider returned the obligation AirPrice priced.

### Request Evidence

Operation key, order id, servicing operation id, predecessor document number, approved amount, currency,
approved disposition, optional disposition reference, successor document number and source pricing reference.
All of it comes from the immutable accepted plan; no mutable order state is passed.

### Outcome Semantics

`ProviderOperationOutcome` plus a value-movement reference and the amount, currency and disposition the
provider acted on. Provider-neutral state is projected as `ExchangeMonetaryState`.

**Confirmed evidence must match the accepted obligation exactly.** The value-movement reference must be
present, and the amount, currency and disposition must equal the accepted refund-due. Any mismatch is
contradictory provider evidence, not a business rejection: it is persisted in `RefundDueDetail`, the operation
becomes `NeedsReconciliation`, nothing is finalized locally and the payout is never retried.
`ExchangeSettlementEvidencePolicy` is the single place this is decided.

### Identity and Correlation

Predecessor document number plus the Ordering-supplied operation key, with the provider's own reference
stored and never interpreted.

### Idempotency / Stable Operation Identity

`exchange-refund-value:{predecessorElectronicTicketId}:{operationId}`, derived internally before first
dispatch. The same key with the same intent answers the same result; the same key with a conflicting amount,
currency, disposition, document, successor document, order or operation must fail closed.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

All five are modelled. `Pending` and `Unknown` are unresolved, never failures: the operation holds the claim
in `AwaitingExternal` and is read back on the next replay. `Rejected` after a confirmed document is an
economic exception and becomes `NeedsReconciliation`.

### Recovery / Read-back

Recover-first. The payout dispatches fresh only when the document confirmation was recorded in this very
attempt; on any resumed attempt the rail reads back under the same key and dispatches only on
`WasDispatched = false`. A read-back must preserve the operation's immutable amount, currency and reference,
and may only resolve `Pending` or `Unknown` to a terminal outcome.

### WasDispatched Requirement

Required. Without it a lost response is indistinguishable from a request that never arrived, and the only
safe behaviours left are to never retry or to risk paying the customer twice.

### Atomicity / Coupling

The payout outcome is persisted on `AcceptedExchangePlan` before local finalization is attempted, so a crash
resumes at the payout read-back rather than repeating a financial instruction.

### Irreversible-Step Ordering

```text
accept AirPrice refund-due  ->  persist plan
  ->  document eligibility
  ->  inventory mutation
  ->  document exchange            [irreversible]
  ->  persist document confirmation and raw successor evidence
  ->  return of value              [irreversible]
  ->  single local finalization transaction
```

There is no release or compensation path after a confirmed document, because the value is genuinely owed.

### Deterministic Simulator

`src/AeroTech.Ordering.Providers.Deterministic/DeterministicRefundValueAdapter.cs`, upgraded for this bundle
and shared with P3-D. It records one operation per key with its intent, replays it for a repeated call with
the same intent and fails closed on a conflicting one, distinguishes never-dispatched from dispatched, keeps
`Confirmed` and `Rejected` sticky, preserves the amount, currency and reference of an accepted-but-unresolved
operation through read-back, and offers throw-before-dispatch, throw-after-dispatch and throw-on-recover
knobs. Its legacy `RecoveredAsDispatched` forcing flag is retained unchanged so P3-D's own tests are
unaffected. It is **not** evidence of production readiness.

### Consumer Contract Tests

`tests/AeroTech.Ordering.Persistence.Tests/Contracts/RefundValue/` — the reusable contract asserts the
defined outcome and exact obligation evidence on a confirmation, never-dispatched recovery, same-key
idempotency, conflicting intent rejected, operation isolation and immutable evidence across read-back. The
deterministic binding adds the crash boundaries and the unresolved-then-resolved property.

Flow coverage is `RefundDueExchangeFlowTests`, cases A through R.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — no real Payment adapter for return of value exists inside Ordering.

### BLOCKED_INTEGRATION

1. **Exact obligation evidence on a confirmation.** Payment must state the amount, currency and a value
   movement reference so Ordering can prove it returned the obligation AirPrice priced. Unverified. An adapter
   must not echo back the request it sent, or the check becomes tautological.
2. **Read-back by caller key.** Payment must answer, for the key Ordering generated, whether it saw the
   operation and what the authoritative outcome is, preserving that operation's evidence. Unverified.
3. **Same-key idempotency, failing closed on conflicting intent.** Unverified, and not addable client-side,
   because a client-side record is lost exactly when the process crashes.
4. **Original-source semantics.** Payment must be able to return value to the original refundable source
   without Ordering naming a destination. The `OriginalFormOfPayment` disposition exists in the P3-D contract
   but has not been verified against a real provider.

### Explicit Non-Responsibilities

Ordering does not choose the amount, the currency or the destination, does not hold or transmit payment
instrument details, does not compute refundability, and does not settle or reconcile acquirer files.

---

## ICC-P3-EXCHANGE-RESIDUAL

### Capability

Preserving value in a provider-authoritative residual instrument when an exchange reprices negative and the
accepted disposition is residual rather than cash.

### Authoritative Owner

**Not yet verified.** Depending on market, carrier and disposition the real owner could be the document host,
Payment, a stored-value service or another provider. Ordering's semantic requirement is frozen; integration
ownership is deliberately not asserted.

### Ordering Semantic Requirement

Residual is not a cash refund and must not be routed through the cash return-of-value port merely because
both return value to the customer. Ordering requires a narrow boundary that creates one authoritative residual
instrument for an accepted obligation and can later identify it.

Residual mechanisms vary by market and may externally be an MCO, an EMD, a voucher, a travel credit or
something else. Ordering keeps the family provider-neutral and hardcodes none of them. Generic EMD servicing
belongs to P3-G, which can enrich this outcome later without redesigning Exchange.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/ExchangeResidual/IExchangeResidualValuePort.cs`

```text
FulfillAsync(ExchangeResidualRequest) -> ExchangeResidualResult
RecoverAsync(ExchangeResidualRecoveryRequest) -> ExchangeResidualRecovery
```

### Request Evidence

Operation key, order id, servicing operation id, quoted exchange id, predecessor document number, successor
document number, beneficiary traveller id, exact accepted amount, currency, accepted disposition, expected
instrument family and source pricing reference. Every value comes from the immutable accepted plan. No
mutable order state and no Ordering-local surrogate keys cross the boundary; correlation is by provider-native
document numbers and the Ordering-supplied operation key.

### Outcome Semantics

`ProviderOperationOutcome`, a provider reference, an instrument reference, a provider-neutral instrument
family, and the amount and currency acted on.

**A meaningless confirmation fails closed.** A `Confirmed` residual must name a provider reference, an
instrument reference and an instrument family, and its amount and currency must equal the accepted
obligation. Anything less is contradictory evidence: persisted in `ResidualDetail`, the operation becomes
`NeedsReconciliation`, and nothing is finalized locally. A confirmation with no identifiable instrument is
useless for later servicing and is treated as such.

### Identity and Correlation

Predecessor and successor document numbers plus the operation key. The instrument reference the provider
returns is stored as the handle to the created value.

### Idempotency / Stable Operation Identity

`exchange-residual:{predecessorElectronicTicketId}:{operationId}`. The same key with the same intent answers
the same instrument; a conflicting amount, currency, disposition, document, successor document, traveller,
quote, order or operation must fail closed. Never create a second instrument because Ordering missed the
first response.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

All five are modelled, with the same discipline as the refund rail. `Pending` and `Unknown` hold the claim in
`AwaitingExternal` and are read back. `Rejected` after a confirmed document becomes `NeedsReconciliation`.

### Recovery / Read-back

Recover-first, under the same key, preserving the instrument reference. A residual created by the provider
whose response was lost is recovered, never re-created.

### WasDispatched Requirement

Required, and load-bearing: without it a lost response could cause a duplicate accountable instrument.

### Atomicity / Coupling

The residual outcome and its instrument evidence are persisted on `AcceptedExchangePlan` before local
finalization.

### Irreversible-Step Ordering

```text
accept AirPrice residual  ->  persist plan
  ->  document eligibility
  ->  inventory mutation
  ->  document exchange           [irreversible]
  ->  persist document confirmation and raw successor evidence
  ->  residual fulfilment         [irreversible]
  ->  single local finalization transaction
```

The instrument is never created before the accountable exchange is known to exist, and no exception to that
was invented for the simulator.

### Deterministic Simulator

`src/AeroTech.Ordering.Providers.Deterministic/DeterministicExchangeResidualAdapter.cs`. One operation per
key with its intent, conflicting intent rejected, never-dispatched distinguishable, `Confirmed` and
`Rejected` sticky, instrument evidence preserved through read-back, throw-before-dispatch,
throw-after-dispatch and throw-on-recover knobs, and overrides that answer with a wrong amount, a wrong
currency or no instrument so the contradiction paths are exercised. **Not** evidence of production readiness.

### Consumer Contract Tests

`tests/AeroTech.Ordering.Persistence.Tests/Contracts/ExchangeResidual/` — the reusable contract asserts
instrument identification on a confirmation, never-dispatched recovery, same-key idempotency, conflicting
intent rejected, operation isolation and immutable evidence across read-back. The deterministic binding adds
the crash boundaries, the unresolved-then-resolved property and refusal stickiness.

Flow coverage is `ResidualExchangeFlowTests`, cases S through AH.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — there is no real residual integration, and even its owner is unverified.
`Providers/Unconfigured/UnconfiguredExchangeResidualProvider.cs` fails closed on both operations with code
20293 and HTTP 501.

### BLOCKED_INTEGRATION

1. **Integration ownership is unverified.** The real owner could be the document host, Payment, a
   stored-value service or another provider, and it may differ by market or disposition. Ordering's semantic
   requirement is frozen; the owner is not asserted. This is an integration question, not an unresolved
   Ordering business rule.
2. **Authoritative instrument identity.** The provider must return a reference that later servicing can use
   to find the created value. An adapter cannot mint one, because a locally invented reference would not
   exist at the provider.
3. **Read-back by caller key preserving the instrument.** Unverified. Without it a lost response risks a
   duplicate accountable instrument.
4. **Same-key idempotency, failing closed on conflicting intent.** Unverified.
5. **Instrument family mapping.** How a market maps a residual disposition onto an MCO, EMD, voucher or
   travel credit is unverified. Ordering carries the family provider-neutrally and does not decide it.

### Known Semantic Gaps

* The residual instrument is not modelled as an Ordering aggregate. It is external value identified by a
  provider reference; the lifecycle belongs to P3-G.
* No forfeit disposition is implemented, and none will be invented as a commercial decision.
* Reconciliation is represented durably but not automated.

### Explicit Non-Responsibilities

Ordering does not create, price, revalue, extend, reissue, refund or void residual instruments, does not
choose the instrument family, and does not implement EMD servicing.

---

## ICC-P3-EXCHANGE-INVENTORY

### Capability

Reservation mutation that replaces the held inventory of exchanged services.

### Authoritative Owner

FlightFlow (inventory / reservation).

### Ordering Semantic Requirement

Only **`Replaced`** services may cross this boundary. A `Continued` coupon keeps its existing sold segment and
must never be re-held, re-priced or re-seated. A `Used` coupon must never appear: it has no successor service
and no inventory consequence. The mutation is **plan-level**: one request carrying a
`ReservationChangeItem[]`, never one request per coupon.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/ReservationChange/IReservationChangePort.cs`

```text
ApplyAsync(ReservationChangeRequest)          -> ReservationChangeResult
RecoverAsync(ReservationChangeRecoveryRequest) -> ReservationChangeRecovery
```

### Request Evidence

`ReservationChangeRequest` carries the operation key, order id, operation id, the external reservation
reference and one `ReservationChangeItem` per replaced service: replaced order-service id, replacement
order-service id, replacement order-segment id, replacement flight-capacity id, replacement booking class and
traveller id.

### Outcome Semantics

`ProviderOperationOutcome` — `Confirmed`, `Pending`, `Unknown`, `Rejected`. `Confirmed` must name a
reservation reference. `Rejected` ends the operation terminally with no document call and no local mutation.
`Pending` and `Unknown` leave the operation `AwaitingExternal` and hold the claim.

### Identity and Correlation

Correlation is by Ordering order-service and order-segment identity plus the external reservation reference.
The replacement capacity reference comes from the accepted AirPrice replacement segment and is passed through,
not invented.

### Idempotency / Stable Operation Identity

`exchange-reservation:{predecessorElectronicTicketId}:{operationId}`, derived internally. The client supplies
no idempotency key. The key is stable across replays of the same operation.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

All five states are modelled. `WasDispatched = false` means the provider never saw the request, so a fresh
`ApplyAsync` is safe. `Pending` and `Unknown` are never blindly redispatched — the rail reads back first.
`Rejected` is terminal. `Confirmed` is durable before the first document call.

### Recovery / Read-back

Recover-first. On resume, the rail calls `RecoverAsync` under the same operation key before considering a new
apply. A recovery answer for an unknown key must report `WasDispatched = false` and must not report
`Confirmed`.

### WasDispatched Requirement

Required. It is the only signal that distinguishes "never reached inventory" from "reached inventory,
outcome unknown", and therefore the only safe basis for re-applying.

### Atomicity / Coupling

The inventory mutation is a single plan-level call. Its confirmation is persisted on
`AcceptedExchangePlan` before the document exchange is attempted, so a crash between the two stages resumes
at the document stage rather than re-holding inventory.

### Irreversible-Step Ordering

Inventory is mutated **after** AirPrice acceptance is persisted and document eligibility has been checked, and
**before** the document exchange. Plan-level Apply and Recover semantics are unchanged.

For an add-collect reissue one step is inserted immediately before it: the funding guarantee. Inventory is
therefore mutated only once the customer's money is assured, and a rejected inventory change releases that
guarantee under its own key before settling terminally. For an even reissue nothing changes — no funding stage
exists and the sequence is exactly as before.

### Deterministic Simulator

`src/AeroTech.Ordering.Providers.Deterministic/DeterministicReservationChangeAdapter.cs`. Behaviour is
unchanged by this bundle; the partially-used contract tests exposed no semantic gap.

### Consumer Contract Tests

`tests/AeroTech.Ordering.Persistence.Tests/Contracts/ReservationChange/` —
`ReservationChangePortContract.cs`, `ReservationChangePortFixture.cs`,
`DeterministicReservationChangePortTests.cs`.

Flow-level coverage: `PartiallyUsedExchangeFlowTests` cases B, C, D and N, plus the existing
`MultiCouponExchangeFlowTests` plan-level assertions and `ExchangeCrashBoundaryTests` F-series.

The partial-use replay proof is
`PartiallyUsedExchangeFlowTests.N_an_unresolved_reservation_change_is_read_back_on_replay_and_never_applied_again`:
with one `Used`, one `Replaced` and one `Continued` coupon, an `Unknown` apply followed by a replay of the
same operation performs exactly one `ApplyAsync`, recovers under the same operation key, never applies again,
makes no document exchange call, creates no successor ticket and no exchange order change, leaves the `Used`
coupon `Used` and every `Open` coupon `Open`, keeps the predecessor `PartiallyUsed`, leaves the commercial
version untouched, stays `AwaitingExternal` and retains the claim.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — no real inventory exchange adapter exists inside Ordering for this operation.

### BLOCKED_INTEGRATION

1. Whether FlightFlow exposes a plan-level replace operation with a caller-supplied stable operation key.
2. Whether it exposes a read-back that distinguishes `WasDispatched` from an unknown outcome.
3. Whether a partially-used document imposes any inventory-side constraint on the remaining Open segments.

### Known Semantic Gaps

* Seat re-selection on a replacement segment is out of scope.
* Compensating release of a confirmed replacement hold after a later document failure is handled by the
  reconciliation path, not by an automatic inventory rollback.

### Explicit Non-Responsibilities

Ordering does not choose inventory, price availability, or manage seat maps. It does not mutate inventory for
`Continued` or `Used` coupons.

---

## ICC-P3-EXCHANGE-DOCUMENT

### Capability

Document host exchange / reissue of the predecessor electronic ticket.

### Authoritative Owner

Document host (electronic ticket authority).

### Ordering Semantic Requirement

The boundary is frozen and clean. Only **`Open` reissue-scope** coupons cross it. A `Used` coupon must never
cross as an exchanged coupon and must never appear in a document-exchange request entry. No Ordering-local
primary key crosses: correlation is **document number + predecessor coupon number**, and each coupon carries
an `IssuedSegmentSnapshot`-equivalent segment snapshot rather than order-service or order-segment ids. A
provider mapping that names a coupon Ordering did not send — including a `Used` coupon — is invalid evidence
and must fail closed into reconciliation rather than being interpreted.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/DocumentExchange/IDocumentExchangePort.cs`

```text
CheckEligibilityAsync(DocumentExchangeEligibilityRequest) -> DocumentExchangeEligibility
ExchangeAsync(DocumentExchangeRequest)                    -> DocumentExchangeResult
RecoverAsync(DocumentExchangeRecoveryRequest)             -> DocumentExchangeRecovery
```

### Request Evidence

`DocumentExchangeRequest` carries the operation key, order id, operation id, predecessor document number,
quoted exchange id, target selection ref, source pricing reference and one `DocumentExchangeCouponRequest`
per Open scope coupon: predecessor coupon number, disposition and a `TicketedSegmentSnapshot`.
`DocumentExchangeEligibilityRequest` carries the same document number and the same predecessor coupon
numbers.

### Outcome Semantics

`ProviderOperationOutcome` plus an optional `SuccessorDocumentIdentity` (document number, issuer carrier,
issuing office, authority, void deadline and `SuccessorCouponIdentity[]`). A successor identity is expected
only on `Confirmed`. Every successor coupon identity must map to a predecessor coupon number present in the
request; mappings must be unique on both sides and must cover every requested coupon.

### Identity and Correlation

Predecessor document number plus predecessor coupon number. Successor coupon numbers are provider-assigned.
Ordering keeps its own ids locally and never sends them.

### Idempotency / Stable Operation Identity

`document-exchange:{predecessorElectronicTicketId}:{operationId}` for the exchange and
`document-exchange-eligibility:{predecessorElectronicTicketId}:{operationId}` for the eligibility check.
Both are derived internally and stable across replays.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

All five are modelled. A non-confirmed outcome is durable, holds the claim and never finalizes locally.
`Rejected` needs reconciliation because inventory has already been mutated. `Confirmed` with unusable
successor evidence also needs reconciliation.

### Recovery / Read-back

Recover-first, always. On any resume the rail calls `RecoverAsync` under the same operation key before
considering a second exchange. The raw confirmed provider evidence is persisted as
`DocumentExchangeSuccessorEvidence` and is authoritative on reload, because the normalized projection is
lossy.

### WasDispatched Requirement

Required, and load-bearing. A second `ExchangeAsync` for a dispatched operation would risk a duplicate
reissue, so the rail only re-dispatches when recovery reports `WasDispatched = false`.

### Atomicity / Coupling

The document exchange is the last external call. Its confirmation plus raw evidence is persisted before the
single local finalization transaction that marks the predecessor `Exchanged`, issues the successor, rebinds
services and writes the order change. The eligibility check is observational and must not dispatch anything.

### Irreversible-Step Ordering

```text
document eligibility (observational)
  -> funding guarantee            [add-collect only, reversible by release]
  -> inventory mutation
  -> document exchange   [irreversible]
  -> persist raw successor evidence
  -> monetary settlement          [add-collect capture, refund-due payout, or residual fulfilment]
  -> single local finalization transaction
```

Local finalization is gated on the monetary leg of whichever outcome AirPrice determined:

```text
Even        document confirmed
AddCollect  document confirmed and capture confirmed with exact evidence
Refund      document confirmed and payout confirmed with exact evidence
Residual    document confirmed and instrument confirmed with exact evidence
```

No monetary leg is ever silently ignored, and an unresolved leg keeps the operation recoverable rather than
finalizing or reconciling prematurely.

The document host is never dispatched without confirmed funding assurance: `EnterDocumentExchangeAsync`
reconciles rather than dispatching if `IsFundingAssured` is false. A definite document rejection or denial
releases the guarantee before the frozen settle-or-reconcile behaviour runs. After a confirmed reissue the
money is owed, so there is no release path and an unresolved or refused capture becomes reconciliation.

After provider confirmation the predecessor becomes `Exchanged`, its `Used` coupons stay `Used`, and the
successor contains successors of the Open scope only. Lineage is A→B, then A→B→C. Never A→C.

### Deterministic Simulator

`src/AeroTech.Ordering.Providers.Deterministic/DeterministicDocumentExchangeAdapter.cs`, including the
malformed-response knobs `UnknownPredecessorCouponNumber`, `DuplicatePredecessorCouponMapping`,
`DuplicateSuccessorCouponNumber` and `OmitSuccessorCoupons`.

### Consumer Contract Tests

`tests/AeroTech.Ordering.Persistence.Tests/Contracts/DocumentExchange/` —
`DocumentExchangePortContract.cs`, `DocumentExchangePortFixture.cs`,
`DeterministicDocumentExchangePortTests.cs`.

Fail-closed mapping and durable-evidence behaviour are covered at flow level by
`DocumentExchangeIdentityTests`, `ExchangeCrashBoundaryTests` (H and I series) and
`PartiallyUsedExchangeFlowTests` cases M and O.

Two partial-use proofs complete that boundary:

* `O_a_durable_document_confirmation_finalizes_in_a_fresh_process_with_no_provider_call` establishes the exact
  durable boundary (accepted plan persisted, eligibility established, inventory confirmed, document outcome
  `Confirmed` with its raw successor evidence stored, local finalization not yet done), disposes the harness,
  and replays the same operation in a fresh one. The fresh process makes zero inventory applies, zero
  inventory recoveries, zero document exchanges, zero document recoveries and zero acceptances, then finalizes
  locally: one exchange order change, one exchange price change set, one successor ticket, the predecessor
  retained and `Exchanged`, the `Used` coupon still `Used` with no successor lineage, only the planned `Open`
  coupons `Exchanged`, no duplicate service or ticket, customer total unchanged, commercial version and
  financial sequence each advanced exactly once. A further replay is provider-free and idempotent.
* `M_a_host_mapping_that_names_the_used_coupon_needs_reconciliation` now also replays. The malformed
  confirmed mapping is neither normalized nor repaired: the plan still carries the raw successor evidence
  naming the `Used` coupon, which is what explains the inconsistency, while the plan's own coupon set never
  contains it. There is no second document dispatch, no successor ticket, unchanged coupon states, a retained
  claim, and the operation stays `NeedsReconciliation`.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — no real document host adapter exists inside Ordering.

### BLOCKED_INTEGRATION

1. Whether the host accepts a coupon-subset reissue of a partially-used document at all.
2. Whether the host correlates on document number plus predecessor coupon number without Ordering-local ids.
3. Whether the host exposes a read-back that distinguishes `WasDispatched`.
4. Whether the host ever reports a mapping for a `Used` coupon, which Ordering treats as invalid evidence.

### Known Semantic Gaps

* Successor coupon numbering is provider-assigned and not validated against any host numbering rule.
* Void-deadline semantics on the successor document are stored but not yet exercised by servicing.

### Explicit Non-Responsibilities

Ordering does not issue, number, or validate documents at the host, does not perform EMD reassociation,
reissue, refund or exchange, and does not interpret provider coupon status codes.

---

## ICC-P3-EXCHANGE-USAGE

### Capability

Authoritative coupon-use (flown / delivered) evidence that makes a document partially used.

### Authoritative Owner

DCS / fulfillment (departure control).

### Ordering Semantic Requirement

Exchange must decide the reissue scope from **durable local state only**. The flow must never ask an external
system at request time whether a coupon is flown. The required local fact is
`TicketCoupon.FinancialStatus == TicketCouponFinancialStatus.Used`, together with
`ElectronicTicket.StatusSummary == PartiallyUsed`. A `Used` coupon is historical pricing context: it gets no
successor coupon, no document-exchange request entry and no inventory mutation, and it is never marked
`Exchanged`, `Continued` or `Replaced`, and never cloned.

The required flow is:

```text
authoritative DCS/fulfillment fact
  -> existing ingestion/projection
  -> Ordering durable state
  -> servicing reads durable state
```

### Ordering Port / Dependency Boundary

`N/A — this is a dependency contract, not a request/response port.` No synchronous DCS call is added and no
DCS port is created. The boundary is an **ingestion** obligation into Ordering's own durable coupon state.

### Request Evidence

`N/A — Ordering makes no request.` The evidence Ordering consumes is its own persisted coupon state.

### Outcome Semantics

Read-side only. `TicketCouponFinancialStatus` values are frozen: `Open = 1`, `Used = 2`, `Void = 3`,
`Exchanged = 4`, `Refunded = 5`, `Suspended = 6`. `ElectronicTicketStatus` values are frozen: `Issued = 1`,
`PartiallyUsed = 2`, `Used = 3`, `Voided = 4`, `Exchanged = 5`, `Refunded = 6`, `Suspended = 7`.

This capability supports `Open` and `Used` only. Any other coupon state on the predecessor is refused as an
application capability limit (`ExchangeCouponStateNotSupported`, code 20270) before any irreversible work. That
refusal is a limit of **this exchange capability**, not a universal domain statement about coupon states.

### Identity and Correlation

Ticket coupon identity and coupon number within the predecessor document.

### Idempotency / Stable Operation Identity

`N/A — no Ordering-initiated operation exists at this boundary.` Idempotency belongs to whatever ingestion
path eventually writes the usage fact.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

`N/A — no dispatch.` Ordering reads a durable fact; there is no provider call to be in one of those states.

### Recovery / Read-back

`N/A — no dispatch to recover.` Correctness depends on the ingestion path being replay-safe, not on a
read-back from Exchange.

### WasDispatched Requirement

`N/A — no dispatch.`

### Atomicity / Coupling

Exchange reads the usage fact inside its own preflight, under the order operation claim, against the same
loaded `ElectronicTicket` aggregate it later mutates. No cross-service transaction is involved.

### Irreversible-Step Ordering

The usage fact is read during preflight, before any external call. A predecessor whose changed service is
covered only by a non-`Open` coupon is refused there with `CouponIsNotExchangeable` (code 20292) — the most
accurate existing exception, chosen over adding a new code.

### Deterministic Simulator

`N/A — nothing to simulate at this boundary.` Tests establish the durable fact directly:
`ExchangeScenarios.FlyCouponAsync` sets the coupon to `Used` and recomputes the document summary to
`PartiallyUsed` or `Used`, and `ExchangeScenarios.SetCouponStatusAsync` sets any other coupon state.

### Consumer Contract Tests

`N/A — no port to contract-test.` The dependency is covered by state-driven flow tests:
`PartiallyUsedExchangeFlowTests` cases B, C, D, E, F, G, H, I, J, K, L, M, N, O, P and Q.

### Real-Service Verification Status

`BLOCKED_INTEGRATION` — and the local writer is missing as well. Verified by inspection of the current code:

* `TicketCoupon` exposes `Void()`, `Refund()`, `RestoreFromRefund()`, `RebindToService()`, `MarkExchanged()`
  and `RecordProviderStatus(...)`. **There is no `Used` transition.**
* No caller of `RecordProviderStatus` exists anywhere in `src/`.
* The only read of `TicketCouponFinancialStatus.Used` in application code is
  `ExchangePreconditions.HistoricalContext`, plus the domain capability checks in `ElectronicTicket` and
  `ExchangeCapabilityPolicy`.
* There is no DCS consumer, poller or ingestion projection under `src/AeroTech.Ordering.Consumers/`.

So today the `Used` state is readable, frozen and enforced, but **nothing inside Ordering writes it**. The
partially-used exchange capability is complete and tested against the durable fact; the fact's producer is
an open integration obligation.

### BLOCKED_INTEGRATION

1. No ingestion path writes `TicketCouponFinancialStatus.Used`. A DCS/fulfillment ingestion (integration
   event plus consumer plus a domain transition on `ElectronicTicket`) is required before real partial-use
   servicing can occur. This is the primary open item of this entry.
2. Exact consumed-operational-segment evidence is not stored. Ordering stores the coupon's frozen
   `IssuedSegment` and can additionally report the `CurrentBoundSegment` it is bound to today, but it does
   not store what was actually flown. Where they differ after a revalidation, both are reported and neither
   overwrites the other.
3. Whether DCS can supply per-coupon usage with a stable correlation to document number plus coupon number.

### Known Semantic Gaps

* `IssuedSegment` must not be read as "the flown segment". After an E1 revalidation the coupon keeps its
  original `IssuedSegment` while its current service association has moved, so
  `HistoricalUsedCoupon.CurrentBoundSegment` is reported whenever it differs and is `null` when it is equal.
* Ordering does not invent DCS flight history to fill the gap above.
* `TicketCouponControlStatus` (`Local`, `External`, `ReleasePending`, `Unknown`) is persisted but has no
  writer either, so coupon control is not yet part of the exchange decision.

### Explicit Non-Responsibilities

Ordering does not perform check-in, boarding, departure control, flight close-out or usage reconciliation. It
does not ask DCS anything synchronously during Exchange and does not derive usage from segment dates, flight
status or the passage of time.
