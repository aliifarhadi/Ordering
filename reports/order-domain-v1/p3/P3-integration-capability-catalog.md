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
`ICC-P3-EXCHANGE-DOCUMENT`, `ICC-P3-EXCHANGE-USAGE`, `ICC-P3-ANCILLARY-EXCHANGE-DISPOSITION`,
`ICC-P3-EMD-ASSOCIATION`, `ICC-P3-EMD-REFUND`, `ICC-P3-EMD-EXCHANGE`,
`ICC-P3-ANCILLARY-RETENTION`, `ICC-P3-ANCILLARY-CANCEL`, `ICC-P3-ANCILLARY-MANUAL-REVIEW`,
`ICC-P3-SERVICING-FEE-DOCUMENT`.

Capability scope of this revision: **even, add-collect, refund-due, residual and mixed reissue**, each over
both supported exchange shapes (fully unused and partially used) and over repeated A→B→C lineage. This closes
P3-F.

`Mixed` means one authoritative pricing result carrying **more than one independently executable monetary
obligation**. Exactly two shapes are supported: one collection with one refund-due, or one collection with one
residual. Nothing else — no second collection leg, no refund and residual together, no three-leg shape, no
split tender and no multiple forms of payment.

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

**Ordering never nets monetary legs.** This is load-bearing. Given an authoritative plan that collects 137.43
and refunds 21.17, the collection provider receives 137.43 and the refund provider receives 21.17. Ordering
never computes 116.26, and never collapses a collection and a residual into one net value. `Mixed` is a
classification saying more than one obligation exists; it is not authority to calculate a net. AirPrice
decides the legs, their kinds, their amounts and their currencies, and Ordering executes them.

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

0. **Multi-leg settlement representation.** Required: AirPrice states whether the result is single-leg or
   mixed, and for a mixed result names each obligation with its kind, amount, currency and disposition, under a
   stable accepted-pricing correlation that survives re-acceptance. Unverified against the real service. An
   adapter must not synthesize a second leg, and must never hand Ordering a single netted amount, because
   Ordering would then be unable to execute the two obligations the carrier actually intends.
1. Whether AirPrice can accept an explicit Open-scope / historical-`Used`-context split in one request.
2. Whether AirPrice can consume the stored fare-construction snapshot in this shape.
3. Whether AirPrice honours an Ordering-supplied operation key with replay-safe acceptance.
4. Whether AirPrice returns a partially-used `Even` outcome at all, or always prices an add-collect.
5. Whether AirPrice returns an explicit authoritative add-collect amount and currency alongside its pricing
   lines, rather than leaving the caller to total the lines. Ordering requires the explicit value and will not
   derive it, so an adapter cannot fill this gap by summing lines — that would silently make Ordering the
   pricing authority.

### Known Semantic Gaps

* `Even`, `AddCollect`, `Refund`, `Residual` and the two supported `Mixed` shapes are accepted. Netted
  outcomes are not a shape at all, because Ordering never nets. A forfeit disposition remains out of scope and
  Ordering will not invent it as a commercial decision.
* An accepted plan carries at most one collection leg and at most one return-of-value leg. A duplicate leg of
  the same kind is not rejected at runtime — it is **unrepresentable**, because each settlement is a single
  optional record rather than a list. That is a stronger guarantee than validation.
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

**This same rail is one leg of a mixed exchange.** When the accepted outcome is `Mixed`, the collection
obligation is executed by exactly this guarantee-then-capture rail, with unchanged semantics, unchanged
operation keys and unchanged recovery. There is no separate mixed payment capability and no second collection
port. Current scope is one collection leg funded by one funding-method reference; split tender and multiple
forms of payment are out of scope. Each monetary leg owns its own stable economic operation key, so the
collection guarantee, the collection capture and the return-of-value leg can never collide.

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

**In a mixed exchange the refund is a dependent second leg.** It becomes eligible only when the document is
confirmed **and** the collection capture is confirmed with exact evidence. Ordering must never return value
while the companion collection is not dispatched, pending, unknown or refused, because that would reissue the
document and give value back without collecting what is owed. The port, its evidence rules and its recovery
guarantees are identical whether the refund is the only leg or the second one.

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
both return value to the customer.

**This entry now covers only one of the two residual fulfilments.** The accepted residual carries
`AcceptedResidual.Fulfillment`, authoritative source evidence, with two values:

| Fulfilment | Who creates the value | Where it is executed |
| --- | --- | --- |
| `DocumentCoupled` | the document authority, inside the exchange transaction | `ICC-P3-EXCHANGE-DOCUMENT` — see the coupled-residual section there |
| `ExternalValue` | an external value owner, after the exchange | **this entry** |

The split was forced by primary evidence, not by convenience. IATA *Airline Guide to EMD Implementation*
§5.2.2.3 requires a residual/refundable balance to be an **EMD-S**; §5.2.2.4 requires that EMD-S document
number to be "included in the **same** Change of Status request message"; §5.3.5 requires a "**single**
exchange/reissue request message … including … **any EMD-S value document number(s) issued for refundable
balance or penalty fee**"; and Amadeus refuses a reissue unless ticket and residual are issued in one entry
(Service Hub 911593). A document-coupled residual therefore cannot be created by a later operation, because
its document number must already exist inside the exchange message.

`ExchangePricingPolicy.EnsureResidualFulfillmentIsCoherent` refuses a `DocumentCoupled` residual that names a
non-document instrument family with `ExchangeResidualFulfillmentMalformed` (20305, 422) before anything is
persisted. Ordering never infers the category from the disposition string or from the instrument family alone.

**For an external residual in a mixed exchange it remains a dependent second leg**, eligible only after the
document is confirmed and the collection capture is confirmed with exact evidence, for the same reason as the
refund leg. The port, its instrument-evidence rules and its recovery guarantees are unchanged. Ordering
requires a narrow boundary that creates one authoritative residual instrument for an accepted obligation and
can later identify it.

External residual mechanisms vary by market — a voucher, a travel credit or something else. Ordering keeps the
family provider-neutral and hardcodes none of them. Generic EMD servicing belongs to P3-G, which can enrich
this outcome later without redesigning Exchange.

**This port must never execute a document-coupled residual.** `SettleMonetaryAsync` dispatches it only when
`plan.RequiresExternalResidual`, so one accepted residual can never be fulfilled through both this port and
the document-exchange port. A document-coupled residual left unsettled after a confirmed exchange reconciles;
it is never retried here.

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

The port is reached only for `ResidualFulfillment.ExternalValue`, so a request on this boundary is itself
evidence that the accepted plan did not couple the residual to the document act.

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

### Exchange-coupled residual document

When the accepted plan carries `AcceptedResidual.Fulfillment = DocumentCoupled`, this capability produces
**two** accountable outcomes under one operation:

```text
successor ETKT  +  residual accountable document (EMD-S)
```

**Request.** `DocumentExchangeRequest.Residual` is an `ExchangeCoupledResidualRequest` carrying the approved
amount, currency, disposition and expected instrument family. It is present only when the accepted plan says
the residual is document-coupled, and it is absent otherwise.

**Result and recovery.** `DocumentExchangeResult.Residual` and `DocumentExchangeRecovery.Residual` carry a
`ResidualDocumentIdentity`: document number, instrument family, amount, currency, issuer carrier, issuing
office, document authority, reason for issuance code and sub code, and an optional provider reference.
Correlation to the accepted exchange is the shared operation key; nothing Ordering-local crosses the boundary,
and no IATA, Amadeus or host DTO enters the Domain. How a real ACL preallocates document numbers or formats
BSP/GDS messaging is entirely its own concern.

**One durable operation.** A document-coupled residual has no operation key of its own:

```text
ONE document exchange key   ONE dispatch   ONE recover   ONE WasDispatched decision
```

A read-back returns the successor **and** the residual document together, so a crash after the host executed
recovers both and redispatches neither.

**Evidence rule.** `ExchangeSettlementEvidencePolicy.CoupledResidualContradiction` is the single place this is
judged. A confirmed exchange is contradictory when it returns no residual document for a document-coupled
residual, returns one with no document number or no reason for issuance, or returns an amount, currency or
instrument family that differs from the accepted obligation. It is also contradictory to return a residual
document at all when the accepted plan owes none or fulfils it externally. A contradiction persists into
`ResidualDetail`, leaves the residual unsettled, keeps the confirmed ticket truth, and moves the operation to
`NeedsReconciliation`. Ordering never invents the document and never re-exchanges.

**Local representation.** The returned residual document is materialized on the **existing**
`ElectronicMiscDocument` aggregate — `Standalone` type, one coupon with `EmdCouponPurpose.ResidualValue`, the
returned document number as its `ExternalValueReference`, and **no** `OrderServiceId`, preserving the frozen P2
rule that an EMD-S fee/penalty/residual document needs no deliverable `OrderService`. No second residual
aggregate, no wallet and no stored-value accounting were created. It is written in the same transaction that
records the document confirmation, so the outcome and the local document commit together; a replay that finds
the document number already present does nothing.

**Ordering-step position.**

```text
funding guarantee -> inventory -> document exchange (+ residual document)
-> persist confirmation and residual evidence + materialize the residual document
-> local post-document materialization checkpoint (ticket, lineage, EMD-A disassociation)
-> capture the collection
-> external return of value if any
-> EMD-A reassociation if any
-> complete
```

The capture now follows the residual document for a coupled residual, because the host protocol requires the
residual inside the exchange transaction. The funding **guarantee** still precedes the irreversible exchange.

**Consumer contract tests.** `ResidualDocumentCouplingTests` — RD1 to RD11: confirmed pair, replay, host
`Pending`/`Unknown`, host `Rejected`, confirmation with a missing residual document, three contradictory-evidence
shapes, crash-after-dispatch recovering both, mixed collection with the capture unresolved and refused, the
external residual staying downstream, and the incoherent accepted fulfilment failing closed.

### BLOCKED_INTEGRATION — coupled residual

1. No real document host is wired, so whether it can return the residual document number in the exchange
   response — as IATA §5.3.5 requires of the request — is unverified from inside Ordering.
2. Whether document numbers are preallocated by Ordering, by the host, or by BSP stock control. The
   `ResidualDocumentIdentity` contract assumes the host names the document it issued.
3. Whether penalty-fee EMD-S documents ride the same message. IATA §5.3.5 mentions them in the same sentence
   as the refundable balance. This revision implements the residual case only and does not model penalty EMD-S
   issuance.

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

Local finalization is gated on every monetary obligation the accepted plan carries:

```text
Even                    document confirmed
AddCollect              document confirmed and capture confirmed with exact evidence
Refund                  document confirmed and payout confirmed with exact evidence
Residual                document confirmed and instrument confirmed with exact evidence
Mixed collect + refund  document confirmed and capture confirmed and payout confirmed
Mixed collect + residual document confirmed and capture confirmed and instrument confirmed
```

The settlement stage walks the legs in a fixed order — collection first, then the single return of value —
and never dispatches a return leg while the collection is unsettled.

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

---

## ICC-P3-ANCILLARY-EXCHANGE-DISPOSITION

### Capability

Deciding, per affected associated ancillary coupon, what must happen to that ancillary when the accountable
ticket coupon it is attached to is reissued.

### Authoritative Owner

AirPrice, as the owner of every commercial and fare-rule decision about a priced product. Ordering does not
own this decision and must not infer it.

### Ordering Semantic Requirement

Ordering owns the **affected scope**, the **structural validation**, the **immutable accepted record** and the
**fail-closed gate**. It never decides a disposition, never defaults one and never carries an ancillary
forward silently.

The governing invariant of this entry is:

```text
every affected ancillary carries an explicit authoritative disposition
before any irreversible exchange work begins
```

An ancillary is *affected* when, and only when, all of the following hold, mechanically:

* the miscellaneous document is `ElectronicMiscDocumentType.Associated`;
* the document's `StatusSummary` is not `Voided`;
* the coupon's `EmdCouponStatus` is `OpenForUse`;
* the coupon's `AssociatedTicketCouponId` names a coupon inside the current accountable predecessor's
  **reissue scope**, which is the set of `Open` coupons of the resolved predecessor document.

Nothing else makes an ancillary affected. A standalone EMD, a voided document, a voided coupon and an
ancillary attached to a `Used` coupon that is historical pricing context are all outside the scope by
construction, and the disposition source is not consulted at all in those cases.

**Every value of `AncillaryExchangeDisposition` is now executable**, each through its own capability:
`ReassociateExisting` here (G1), `Refund` in `ICC-P3-EMD-REFUND` (G2), `ExchangeToNewEmd` in
`ICC-P3-EMD-EXCHANGE` (G3), `RetainAsResidual` in `ICC-P3-ANCILLARY-RETENTION` (G4), `Cancel` in
`ICC-P3-ANCILLARY-CANCEL` and `ManualReview` in `ICC-P3-ANCILLARY-MANUAL-REVIEW` (both G5). A disposition
value outside that set is still a recognised, recorded, *not executable* decision that stops the exchange
before the first irreversible operation with `AncillaryDispositionNotExecutable` (20298). No disposition is
ever silently carried forward, guessed at or downgraded to a different one.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/AncillaryDisposition/IAncillaryExchangeDispositionPort.cs`

One method, `DecideAsync`, which is **side-effect-free**: it obtains a decision and mutates nothing anywhere.
It therefore carries no operation key, no recovery method and no `WasDispatched` concept. A separate port was
created rather than extending the existing `IExchangeQuotePort` because no extension point for a per-ancillary
decision existed on the accepted-exchange contract, and because acceptance of a priced exchange and the
disposition of an attached ancillary are two different authoritative answers with different lifetimes.

### Request Evidence

Order id, servicing operation id, quoted exchange id, predecessor document number, the reissue-scope coupon
numbers, and per affected coupon: EMD document number, EMD coupon number, EMD type, coupon purpose, reason for
issuance sub code, predecessor document number and predecessor coupon number.

Accountable-document terms only. No Ordering surrogate identity — no order service id, no EMD coupon id, no
ticket coupon id — crosses this boundary.

### Outcome Semantics

A decision reference, a decision version and one `AncillaryCouponDisposition` per affected coupon, each naming
the EMD coupon, the predecessor coupon it was attached to, the disposition and — for `ReassociateExisting` —
the `TargetPredecessorCouponNumber`.

The target is expressed as a **predecessor** coupon number on purpose. The successor document number and its
coupon numbers do not exist when the decision is obtained; the predecessor coupon number is resolvable at
acceptance time and maps deterministically to the accepted successor coupon through the accepted plan.

### Structural validation — fail closed

`ExchangeAncillaryPlanner` is the single place this is decided. A decision is refused, before anything is
persisted and before the order is mutated, when:

| Condition | Failure |
| --- | --- |
| an affected coupon has no decision | `AncillaryDispositionMissing` (20296, 422) |
| two decisions name the same coupon | `AncillaryDispositionMalformed` (20297, 422) |
| a decision names an ancillary outside the affected scope | 20297 |
| a decision names another predecessor document or coupon | 20297 |
| `ReassociateExisting` carries no target coupon | 20297 |
| the target is outside the accepted successor scope | 20297 |
| the target is a `Used` coupon, i.e. historical context | 20297 |
| the decision carries no reference of its own | 20297 |
| the decision is not a defined disposition value | `AncillaryDispositionNotExecutable` (20298, 422) |

All of these are durable rejections: the accepted plan is stored with `AcceptedExchangeDisposition.Rejected`
and the same code and HTTP status replay terminally under the same command receipt.

### Identity and Correlation

The decision's own `DecisionReference` and `DecisionVersion`, stored verbatim on every accepted disposition
and carried into the reassociation request and into the EMD association history. Ordering never interprets
them.

### Idempotency / Stable Operation Identity

N/A — the call has no side effect, so it needs no stable operation identity. It happens once per fresh
execution, before the accepted plan is persisted; a replay reads the persisted plan and never asks again.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

N/A — a decision lookup is not a durable operation. An unreachable or unconfigured source is not an outcome:
the servicing operation is left `AwaitingExternal`, no plan is persisted, nothing is mutated and the command
is retryable from the start.

### Recovery / Read-back

N/A — see above. The *decision* is made durable instead: once accepted it lives in
`AcceptedExchangePlanAncillaries` and is never re-asked.

### Atomicity / Coupling

The decision is obtained after AirPrice acceptance and strictly **before** `AcceptedExchangePlanStore.SaveAsync`,
so an accepted exchange plan is never persisted without the complete ancillary plan it implies. Accepted
dispositions are stored in the same transaction as the plan they belong to.

### Irreversible-Step Ordering

```text
quote -> accept -> obtain dispositions -> validate -> persist complete plan
-> eligibility -> funding guarantee -> inventory -> document exchange
-> monetary settlement -> EMD-A reassociation -> local finalization
```

The disposition lookup sits before every irreversible step in that chain, which is what makes 20296/20297/20298
safe to raise: no inventory has moved, no document has been exchanged and no money has moved.

### Deterministic Simulator

`DeterministicAncillaryDispositionAdapter` in `AeroTech.Ordering.Providers.Deterministic`. It defaults to
`ReassociateExisting` onto the same coupon number and can be steered to omit a decision, duplicate one, add an
unrelated one, report another predecessor document or coupon, drop the target, retarget, drop its own
reference, return any disposition per coupon, or throw.

### Consumer Contract Tests

`AncillaryDispositionGateTests` — cases A–T. Scope calculation (A–F), every structural refusal (G–O), every
non-executable disposition (P–R), and an unreachable or unconfigured source (S–T).
`UnconfiguredAncillaryProviderTests` covers the 501 refusal.

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. AirPrice exposes no per-ancillary exchange disposition endpoint today. The Ordering-side boundary,
   validation and fail-closed gate are complete and deterministic; the authoritative source is not wired.
2. Whether AirPrice will return the target as a predecessor coupon number, as a successor coupon number, or
   as a segment reference. Ordering's boundary currently requires the predecessor coupon number, which is the
   only one resolvable at decision time.
3. Whether the decision reference is stable across a re-ask for the same quoted exchange. Ordering does not
   depend on it being stable, because it never re-asks after acceptance.

### Known Semantic Gaps

* No partial acceptance. Ordering accepts a decision set for all affected coupons or none.
* An `Associated` EMD whose coupon points at a ticket coupon of **another** accountable document in the same
  order is not in this scope, because the reissue scope is resolved per accountable document.
* Ordering does not ask for a disposition during `QuoteAsync`. The quote stays side-effect-free and the
  disposition is obtained only when an exchange is actually executed.

### Explicit Non-Responsibilities

Ordering does not price ancillaries, does not decide whether an ancillary survives a reissue, does not choose
a target coupon, does not decide EMD refundability and does not substitute one disposition for another.

---

## ICC-P3-EMD-ASSOCIATION

### Capability

Moving an open associated EMD coupon from the predecessor ticket coupon it documents to the successor ticket
coupon produced by a reissue, in the authoritative accountable-document record.

### Authoritative Owner

The accountable-document authority that owns EMD association — the same authority that owns the electronic
miscellaneous document itself.

### Ordering Semantic Requirement

Ordering owns the stable per-coupon operation identity, the immutable accepted target, the durable provider
outcome, the evidence check and the reconciliation state. It never invents an association and never mutates
its own EMD aggregate before the authority has confirmed the move.

The governing invariant is:

```text
the local association follows the authoritative one, never leads it
```

Concretely: the local `ElectronicMiscDocument.ReassociateCoupon` runs only inside the finalizing transaction,
in the same unit of work that mints the successor ticket. Until then the ancillary still points at the
predecessor coupon, which is the truthful state while the reissue may still fail.

**The move happens after all monetary settlement and before local finalization.** An ancillary is not moved
onto a successor document whose money has not settled, and a successor document is not created locally while
an ancillary it must carry is unresolved.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/EmdAssociation/IEmdAssociationPort.cs` — `ReassociateAsync` plus
`RecoverReassociationAsync`, the same two-method durable-operation shape as every other P3 provider rail.

### Request Evidence

Operation key, order id, servicing operation id, EMD document number, EMD coupon number, predecessor document
number, predecessor coupon number, successor document number, successor coupon number, beneficiary traveller
id, issuer carrier id and the decision reference that authorised the move.

Accountable-document terms only. No EMD coupon id, no ticket coupon id and no order service id cross the
boundary. The beneficiary traveller id and issuer carrier id are the only identities, and both are already
shared identity, not Ordering surrogate keys.

### Outcome Semantics

`ProviderOperationOutcome` plus a provider reference and, optionally, the EMD document and coupon it moved and
the document and coupon it attached them to. Provider-neutral state is projected as `ExchangeAncillaryState`
(`NotRequired`, `NotStarted`, `Pending`, `Confirmed`, `Rejected`, `NeedsReconciliation`).

**A confirmation must not contradict what was requested.** `ExchangeSettlementEvidencePolicy.ReassociationContradiction`
is the single place this is decided. A `Confirmed` result is contradictory when it carries no provider
reference, or when it names a different EMD document, a different EMD coupon, a different associated document
or a different associated coupon than the accepted plan asked for. A contradiction is not a business
rejection: it is persisted in `AssociationDetail`, the operation becomes `NeedsReconciliation`, the local
association is left untouched and the move is never retried.

### Identity and Correlation

EMD document number plus EMD coupon number plus the Ordering-supplied operation key. The provider's own
reference is stored and never interpreted; it is also written into the EMD association history as the
evidence of the move.

### Idempotency / Stable Operation Identity

```text
emd-reassociate:{emdDocumentNumber}:{emdCouponNumber}:{operationId}
```

derived internally by `OrderOperationCoordinator.ProviderOperationKey` before first dispatch. **One key per
affected coupon**, so a multi-coupon exchange has one independently recoverable durable operation per coupon
and never one aggregate operation whose partial completion is unrepresentable. The same key with the same
intent answers the same result; the same key with a conflicting document, coupon, predecessor, successor,
traveller, carrier, decision, order or operation must fail closed.

### NotDispatched / Pending / Unknown / Rejected / Confirmed

All five are modelled. `Pending` and `Unknown` are unresolved, never failures: the operation holds its claim
in `AwaitingExternal`, the successor document is not created, and the coupon is read back on the next replay.
`Rejected` becomes `NeedsReconciliation` — after a confirmed document exchange, a refused ancillary move is an
economic exception for an operator, not something Ordering resolves by itself.

### Recovery / Read-back

`RecoverReassociationAsync` answers per operation key. The stage dispatches fresh **only** when its
prerequisite confirmed in the same attempt; otherwise it reads back first and dispatches only when the
recovery says `WasDispatched = false`. In practice the first coupon of an even exchange may dispatch fresh,
and every later coupon and every replay reads back first, which is the safe direction.

### WasDispatched Requirement

Required and load-bearing. A recovery for a key that never left Ordering must answer `WasDispatched = false`
with no provider reference, so the stage can dispatch once rather than assume a move that never happened.
A recovery for a dispatched key must answer `WasDispatched = true` and must never rewrite the evidence of an
already resolved move.

### Atomicity / Coupling

The provider outcome is persisted per coupon in `AcceptedExchangePlanAncillaries` by
`RecordAncillaryAssociationOutcomeAsync` and committed before the next coupon is attempted. The local
aggregate mutation and the successor-ticket issuance share one transaction, so the local association can never
move without a successor document to move it to.

### Irreversible-Step Ordering

```text
... document exchange confirmed -> monetary settlement confirmed
-> for each affected coupon in (document number, coupon number) order:
       reassociate -> record outcome -> commit
-> local finalization: successor ticket + local association move + completion
```

Ordering is `(EMD document number, EMD coupon number)` ordinal, so the dispatch sequence is reproducible
across replays and restarts.

### Deterministic Simulator

`DeterministicEmdAssociationAdapter` in `AeroTech.Ordering.Providers.Deterministic`. It remembers one
operation per key with an intent fingerprint, refuses a conflicting intent on a known key, and can be steered
to any outcome globally or per coupon, to throw before or after dispatch, to fail a read-back, to omit its
provider reference, and to report a different EMD document, EMD coupon, associated document or associated
coupon for contradiction testing.

### Consumer Contract Tests

`EmdAssociationPortContract` — the reusable kit any implementation of the port must pass: confirmed evidence
names what it moved, a never-dispatched key answers `WasDispatched = false`, a dispatched key is recoverable
under its own key only, a repeated key never moves the coupon twice, a conflicting intent on a known key fails
closed, one key never consumes another operation's move, a read-back never rewrites resolved evidence, and no
local Ordering identity is ever reported back.

Bound implementations: `DeterministicEmdAssociationPortTests`, plus `UnconfiguredAncillaryProviderTests` for
the 501 refusal.

Flow coverage: `EmdReassociationFlowTests` — G1-C1 through G1-C12 plus the retained G1 matrix. G1-C1 walks
`Associated -> DisassociatedByReissue -> Reassociated`; G1-C2 to G1-C5 prove ticket truth survives every
unresolved, refused and contradictory ancillary outcome; G1-C6 to G1-C10 cover the five crash boundaries.

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real EMD association authority is wired. `UnconfiguredEmdAssociationProvider` fails closed with
   `EmdAssociationSourceNotConfigured` (20295, 501). The reissue stays authoritative and the ancillary stays
   detached with its `DisassociatedByReissue` evidence; nothing is attached anywhere.
2. Whether the real host requires a **separate disassociation command** before or during the reissue. Ordering
   currently treats disassociation as mechanically implied by the confirmed reissue and issues no provider
   call for it. If a real contract proves otherwise, the fix is an ACL-level adaptation, not a change to this
   semantic.
2. Whether the authority exposes reassociation as its own operation or only as a void-and-reissue of the EMD.
   If it is only the latter, `ReassociateExisting` is not implementable against that provider and the
   disposition becomes `ExchangeToNewEmd`, which this revision deliberately does not execute.
3. Whether the authority echoes the EMD coupon and the associated coupon it acted on. Ordering's evidence
   check treats every echoed field as optional but verifies each one that is present.
4. Whether a reassociation is idempotent under Ordering's operation key on the real provider.

### Known Semantic Gaps

* Only `ReassociateExisting` is executed. EMD refund, EMD exchange, EMD residual value and EMD cancellation
  are out of scope for this revision and are refused explicitly, not approximated.
* A reissue whose ancillary never reattaches leaves a **detached open EMD-A coupon**. That is the truthful
  state and it is deliberately visible rather than hidden: the operation is `AwaitingExternal` or
  `NeedsReconciliation`, the coupon carries its `DisassociatedByReissue` provenance, and the accepted plan
  retains the target it was supposed to reach.
* No new `ServicingOperationKind` was introduced. The reassociation is a stage of the Exchange operation, not
  an operation of its own, so it inherits the Exchange claim, receipt and replay semantics.
* A `NeedsReconciliation` ancillary has no automated operator remediation command yet. The durable evidence
  needed for one — accepted target, decision identity, provider outcome, provider reference and contradiction
  detail — is all persisted.
* `ExchangeBlockedByAssociatedMiscDocument` (20272) is retained in `ExceptionFactory` but is now unreachable:
  the blanket refusal it expressed has been replaced by this capability. It is kept so the numbering stays
  contiguous within 20000–29999.

### Explicit Non-Responsibilities

Ordering does not revalue an ancillary, does not reprice it, does not decide which successor coupon it belongs
on, does not void or reissue an EMD as part of an exchange, and does not reconcile a refused move on its own.

---

## ICC-P3-EMD-REFUND

### Capability

Refunding one or more coupons of an associated electronic miscellaneous document in the authoritative
accountable-document record, after the ticket coupon the ancillary documented has been reissued, and moving
the approved value back to the passenger.

### Authoritative Owner

Two distinct authorities, never conflated:

| Authority | Owns |
| --- | --- |
| the ancillary disposition source | whether the coupon is refunded at all, the approved amount, currency, disposition and the pricing lines that justify it |
| the accountable-document authority | the `Refunded` (`'R'`) coupon status in the authoritative EMD record |
| the return-of-value authority | the movement of money back to the original form of payment |

Ordering adjudicates none of the three. It carries identity, evidence, sequencing and recovery.

### Ordering Semantic Requirement

```text
one Exchange servicing operation
    -> one OrderChange
    -> one initial Exchange PriceChangeSet
    -> zero..N later ancillary-refund PriceChangeSets
```

A confirmed ancillary refund is a **dependent commercial consequence of the same Exchange operation**, not a
second servicing operation. `OrderChange` is the servicing-operation envelope; `OrderPriceChangeSet` is the
independently committed financial consequence. The persistence model already expresses exactly this:

```text
OrderChange            UNIQUE (OrderId, OperationId)
OrderPriceChangeSet    ChangeId is NOT unique;  UNIQUE (OrderId, FinancialSequence)
```

so one Exchange operation may own several price change sets without owning several order changes.

**Every confirmed group owns exactly one consequence.**

```text
one confirmed EMD exchange group
  => exactly one dependent PriceChangeSet
  => exactly one OrderPricingChanged
  => exactly one CommercialVersion advance
```

Therefore every executable `ExchangeToNewEmd` group must carry source-approved pricing lines, an even exchange
included — expressed as an authoritative zero-net withdrawal/grant pair. A group with no pricing evidence is
refused at acceptance. Ordering never invents zero-value lines, and replay appends nothing.

The commit rail follows the already-frozen Refund pattern:

```text
document authority confirms
 -> EMD coupon/document refund truth + ancillary refund PriceChangeSet + commercial consequence
    become durable in ONE local checkpoint
 -> value movement follows
```

If the value movement is later `Pending`, `Unknown`, `Rejected` or contradictory:

```text
EMD stays Refunded
the ancillary refund PriceChangeSet stays committed
the ticket stays Exchanged
the successor stays authoritative
the operation is AwaitingExternal or NeedsReconciliation
```

There is no rollback of any of it.

**Disassociation precedes refund.** Every executable affected ancillary — refund and reassociation alike — is
`DisassociatedByReissue` inside the materializing transaction, before any downstream stage. A refund therefore
always acts on a coupon that is already detached from the exchanged predecessor coupon, which is both the
truthful state and the benchmarked precondition of EMD-A refund.

### Ordering Port / Dependency Boundary

No new port. Two existing, document-neutral rails are reused:

| Act | Port |
| --- | --- |
| document refund | `src/AeroTech.Ordering.Domain/Ports/DocumentRefund/IDocumentRefundPort.cs` |
| value movement | `src/AeroTech.Ordering.Domain/Ports/RefundValue/IRefundValuePort.cs` |

`IDocumentRefundPort` addresses a document by **document number plus coupon numbers**, which is aggregate-neutral
— an EMD is not a different capability from a ticket at this boundary, only a different document. No
`IEmdRefundPort` was created: the aggregate type differing is not a reason for a second port.

### Request Evidence

Operation key, order id, servicing operation id, EMD document number and the EMD coupon numbers being refunded
for the document act; operation key, order id, servicing operation id, EMD document number, approved amount,
approved currency, approved disposition, source reference, successor document number and the source pricing
reference for the value act.

Operation keys are server-derived and stable per coupon:

```text
emd-refund:{EmdDocumentNumber}:{EmdCouponNumber}
emd-refund-value:{EmdDocumentNumber}:{EmdCouponNumber}
```

### Response Evidence

The document act must echo the document number and the coupon numbers it acted on, and carry a provider
reference on a `Confirmed` outcome. The value act must echo amount, currency and disposition. Every echoed
field is optional, and every field that is present is verified against the accepted decision. A `Confirmed`
outcome that contradicts the accepted decision is treated as contradictory evidence, not as a business
rejection: it is persisted, the operation becomes `NeedsReconciliation`, and neither the document nor the
money is mutated locally.

### Recovery

Recover-first on both rails. `IDocumentRefundPort.RecoverAsync` returns
`DocumentRefundRecovery(WasDispatched, ...)`; a refund is dispatched fresh only when the prerequisite was
confirmed in the same attempt, otherwise the key is recovered first and dispatched only when the authority
reports it never received it. The same discipline already governs `IRefundValuePort`.

### Idempotency Proof From Persisted State

The accepted plan's ancillary row is keyed by `(OperationId, EmdCouponId)` and carries the accepted refund
decision plus `RefundPriceChangeSetId`. That is the durable correlation: the specific *exchange operation +
EMD document + EMD coupon + accepted refund decision* provably already owns its price change set. No
in-memory flag participates.

### Deterministic Verification

`DeterministicDocumentRefundAdapter` and `DeterministicRefundValueAdapter` in
`AeroTech.Ordering.Providers.Deterministic`; the ancillary decision comes from
`DeterministicAncillaryDispositionAdapter`, which now also declares the **pricing source** of the approved
refund. `PricingSource.OrderingDerived` is refused at acceptance — Ordering never authors ancillary refund
economics. A line naming a reversed pricing line other than the EMD coupon's own declared `PricingLineId` is
refused with `RefundReversalOutsideDocumentScope` (20227, 422).

Contract coverage: `tests/AeroTech.Ordering.Persistence.Tests/Contracts/DocumentRefund/` — added in this
revision, because `IDocumentRefundPort` had no contract kit and `UnconfiguredDocumentRefundProvider` had no
test. The kit asserts the same nine invariants the other P3 rails already assert: a defined eligibility
answer, a confirmed refund naming the document and coupons it acted on, a never-dispatched key answering
`WasDispatched = false`, a dispatched key recoverable under its own key only, a repeated key never refunding
twice, a conflicting intent on a known key failing closed, one key never consuming another operation's
refund, a read-back never rewriting resolved evidence, and no local Ordering identity ever reported back.
`DeterministicDocumentRefundAdapter` was upgraded to the durable-key shape the other deterministic adapters
already use, so a key that already owns a different refund intent now fails closed instead of silently
overwriting an irreversible act. Value coverage stays `.../Contracts/RefundValue/`.

Flow coverage: `AncillaryRefundFlowTests` — G2H1–H5 (happy paths and coupon-level partial refund), G2S1
(seven source-validation shapes), G2S2 (out-of-document reversal), G2D1–D5 (document act recovery and contradiction), G2V1–V3 (value act
recovery and contradiction), G2C1–C6 (the commercial consequence, replay, multi-coupon, mixed disposition,
unsettled value and crash recovery), G2F1–F2 (frozen invariants).

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real ancillary disposition source is wired, so no real carrier ever adjudicates a `Refund`.
   `UnconfiguredAncillaryProvider` fails closed with 501.
2. No real document-refund host is wired for miscellaneous documents. Whether the host accepts a **partial
   coupon refund** of a multi-coupon EMD is provider-specific: IATA's own guidance permits it, Travelport
   forbids it, Sabre lets the validating carrier choose. Ordering implements per-coupon refund and will
   accept a host that refuses it only by having that host reject the act, which reconciles rather than
   corrupts.
3. Whether the real host echoes the document number and coupon numbers it refunded. Ordering verifies each
   echoed field that is present and treats absence as unverified, not as agreement.
4. Whether a document refund is idempotent under Ordering's operation key on the real host.
5. Whether the real return-of-value authority settles an ancillary refund on the original form of payment
   without a separate instrument.

### Known Semantic Gaps

* **Refund Cancel is not implemented.** A `Refunded` (`'R'`) EMD coupon has a documented carrier-side
  *Refund Cancel* before the revenue lift. Ordering does not model it and never un-refunds a coupon. This is
  deliberately deferred, not approximated.
* **"A refunded coupon cannot be reassociated" is inferential.** No consulted primary source states it
  literally. Ordering enforces it because `'R'` is a final coupon status and a final coupon cannot carry a
  live association. It is recorded as inference, not as a quoted rule.
* **Service-level refund marking is effectively unreachable in this revision.** `ApplyDocumentRefund` is
  invoked with the refunded EMD coupon's delivering `OrderServiceId`, and `MarkDocumentRefunded` correctly
  no-ops unless that service's `DocumentStatus` is still `Issued`. Every genuinely EMD-documented ancillary
  service in the current model *covers* the air service it is associated with, and
  `EnsureNoActiveServiceDependsOn` already refuses the reissue outright (20258) while such a service is
  active. So the reachable G2 shapes are coupons with no delivering service, or coupons naming a service the
  exchange itself supersedes — where the no-op is the correct answer, because that service was **exchanged,
  not refunded**. G2H5 asserts exactly that. A positive service-level refund marking needs an ancillary that
  survives the reissue, which no current shape produces.
* `ExchangeToNewEmd`, `RetainAsResidual`, `Cancel` and `ManualReview` are executed by their own
  capabilities — `ICC-P3-EMD-EXCHANGE`, `ICC-P3-ANCILLARY-RETENTION`, `ICC-P3-ANCILLARY-CANCEL` and
  `ICC-P3-ANCILLARY-MANUAL-REVIEW`. Refund never becomes any of them, and none of them becomes a refund.
* A `NeedsReconciliation` ancillary refund still has no automated operator remediation command. Every piece
  of evidence one would need is persisted.

### Explicit Non-Responsibilities

Ordering does not decide whether an ancillary is refundable, does not compute the refund amount, does not
derive it from the EMD issue value, the order service price or any pricing allocation, does not choose the
disposition, does not un-refund a coupon and does not reconcile a refused or contradicted act on its own.

---

## ICC-P3-EMD-EXCHANGE

### Capability

Exchanging one or more coupons of an electronic miscellaneous document for a **new** miscellaneous document in
the authoritative accountable-document record, as a dependent consequence of a ticket reissue, including any
accountable residual document that exchange result itself produces.

### Authoritative Owner

Three distinct authorities, never conflated:

| Authority | Owns |
| --- | --- |
| the ancillary disposition source | whether the coupon is exchanged at all, the successor document type, RFIC, coupon RFISC/purpose/value, beneficiary binding, the exchange-group composition and the approved economics |
| the accountable-document authority | the `E` / Exchanged predecessor coupon status, the **successor document number**, and the successor coupon identities in the authoritative record |
| the funding / return-of-value authority | any collection or external return of value the exchange result carries |

Ordering adjudicates none of the three. It carries identity, grouping, evidence, sequencing and recovery.

### Ordering Semantic Requirement

```text
one Exchange servicing operation
  -> one Exchange OrderChange
  -> originating ticket-exchange PriceChangeSet
  -> G2 ancillary-refund PriceChangeSet(s), if any
  -> G3 ancillary-EMD-exchange PriceChangeSet(s), if any
```

The ticket Exchange operation stays the envelope. No child servicing operation and no second `OrderChange`
exist for the same reissue. Each independently confirmed EMD exchange **group** appends exactly one dependent
`PriceChangeSet` under the existing Exchange `OrderChange`, through the frozen G2 `CommitDependentPriceChange`
capability.

**An exchange group, not a coupon, is the unit of work.** The source decides that one or more of its coupons
are exchanged in one accountable-document transaction; that decision is durable, and Ordering issues exactly
one provider act per group:

```text
one accepted exchange group = one IEmdExchangePort operation = one successor document = one consequence
```

**Disassociation precedes exchange.** Every executable affected ancillary is `DisassociatedByReissue` inside
the materializing transaction (G1, frozen), so an exchange always acts on a coupon already detached from the
exchanged predecessor ticket coupon — the benchmarked precondition.

**Supported monetary shapes are frozen, and none is collapsed into a signed delta.**

```text
supported   Even, AddCollect, RefundDue, Residual, AddCollect + RefundDue, AddCollect + Residual
refused     RefundDue + Residual, AddCollect + RefundDue + Residual,
            a non-positive leg amount, a leg currency that is not the group currency,
            a blank refund or residual disposition
```

Each leg keeps its own accepted value object, its own group-scoped operation key and its own durable outcome.
A duplicate leg of one kind cannot be expressed: the accepted terms hold at most one collection, one refund
and one residual. `RefundDue` keeps its original-refundable-source semantics and a residual is never
reinterpreted as a refund.

**A document-coupled residual can only be an EMD in this slice.**

```text
ResidualFulfillment.DocumentCoupled  =>  ExpectedInstrument == ResidualInstrumentKind.Emd
```

Any other instrument — `Mco`, `Voucher`, `TravelCredit`, `Other`, `Unknown` — is refused at acceptance, before
any irreversible ticket or document work. No MCO lifecycle exists, no MCO is mapped onto an EMD, and no
unsupported instrument is silently downgraded to external value.

**Source-to-successor mapping is one-to-one, and that is a representation limit, not an industry rule.**

```text
SourceCouponNumbers.Count == SuccessorCoupons.Count

sorted source coupon order  <->  accepted successor-coupon order  <->  normalized provider result order
```

Merge (N:1) and split (1:M) EMD exchanges are industry-valid. They require an explicit authoritative mapping
in the accepted terms, which this contract does not carry. Ordering must not infer one, so any other
cardinality is refused before the ticket exchange. Supporting them is a future extension of the accepted
disposition contract.

**Provider coupon numbers are authoritative.** The successor document is created with the exact coupon numbers
the authority returned, never renumbered `1..N`; a non-positive or repeated returned number is a contradiction.
Normalized result order corresponds to the request successor-coupon order. Both lineage directions carry the
actual provider number.

**Association at issuance, not by a second act.** When the source approves an `Associated` successor, the
successor EMD is created already bound to the approved successor ticket coupon and records that association as
its issuance history. `IEmdAssociationPort` is **not** called: creating a document and then "reassociating" it
would misrepresent one accountable act as two.

**Ordering never invents a document number.** The request carries no successor document number; the confirmed
result supplies it, and it is authoritative.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/EmdExchange/IEmdExchangePort.cs` — `ExchangeAsync` plus `RecoverAsync`, the
same two-method durable-operation shape as every other P3 provider rail. `IEmdIssuancePort` is deliberately
**not** reused: issuance plus a local status flip cannot prove the atomic accountable-document exchange the
predecessor's `E` status depends on.

`ExchangeCoupledResidualRequest` and `ResidualDocumentIdentity` are **reused** from
`Ports/DocumentExchange/` rather than duplicated — the semantics of "an exchange-coupled residual obligation"
and "the accountable residual document that answered it" are identical for a ticket and for an EMD.

### Request Evidence

Operation key, order id, servicing operation id, accepted exchange-group reference, source document number,
source coupon scope, beneficiary/traveller, successor document type, successor RFIC, currency, the accepted
successor coupons (purpose, RFISC, value, and for `Associated` the **successor** ticket coupon number), the
successor ticket document number for an `Associated` successor, the source decision reference, the source
pricing reference, and any coupled residual obligation.

Operation keys are server-derived and stable per group:

```text
emd-exchange:{ExchangeGroupRef}
emd-exchange-guarantee:{ExchangeGroupRef}
emd-exchange-capture:{ExchangeGroupRef}
emd-exchange-refund:{ExchangeGroupRef}
emd-exchange-residual:{ExchangeGroupRef}
```

Monetary sequencing per group:

```text
guarantee (when the group collects)
 -> EMD exchange act
 -> one local checkpoint
 -> capture
 -> refund value  (IRefundValuePort, when the group owes a refund)
 -> external residual  (only when the source says the residual is non-document value)
```

so ticket monetary acts, different EMD groups, and the G2 refund rails can never collide.

### Response Evidence

A `Confirmed` result must carry a provider reference and a successor identity: document number, type, issuer
carrier, issuing office, authority, RFIC, currency, and one coupon identity per accepted successor coupon
(number, purpose, RFISC, value, currency, and the associated ticket coupon number for `Associated`). A coupled
residual obligation must be answered in the same result with its own document identity.

A `Confirmed` result must additionally carry the beneficiary the request bound, coupon numbers that are
positive and unique, and — for an `Associated` successor — the associated ticket **document** and a per-coupon
association, both exact. A `Standalone` successor must carry neither. A confirmed coupled residual must carry
its document number, reason-for-issuance code and sub code, an exact amount and currency, and an instrument
equal to the accepted `ExpectedInstrument`. A confirmed **external** residual must carry a provider reference,
an instrument reference, an instrument, an exact amount and currency, and an instrument matching the accepted
expectation whenever that is not `Unknown`. A confirmed refund value must carry a value movement reference, an
exact amount and currency, and an exact disposition when echoed.

Every echoed field is verified against the accepted group. A `Confirmed` result that contradicts it is treated
as contradictory evidence, not as a rejection: it is persisted, the operation becomes `NeedsReconciliation`,
nothing local is materialized, and the act is **not** re-dispatched.

When the returned successor document number already exists locally, it is accepted as a replay only on exact
identity: origin servicing operation, beneficiary, type, currency, issuer, issuing office, authority, RFIC,
coupon count, and per returned coupon number the purpose, RFISC, value, currency, predecessor EMD
id/document/coupon mapping, ticket association, order service and external value reference. Any mismatch
reconciles; no other document number is generated and the confirmed exchange is not re-dispatched.

### Recovery

Recover-first. A group is dispatched fresh only when its prerequisite was confirmed in the same attempt;
otherwise the key is recovered and dispatched only when the authority reports `WasDispatched = false`. An
unresolved outcome resolves on read-back; a resolved one is never rewritten.

### Idempotency Proof From Persisted State

`AcceptedExchangePlanAncillaryExchangeGroups` is keyed `(OperationId, ExchangeGroupRef)` and carries the
accepted terms plus `ExchangeOutcome`, `ExchangeProviderReference`, `SuccessorElectronicMiscDocumentId`,
`SuccessorDocumentNumber`, `PriceChangeSetId` and the per-stage monetary evidence. Bidirectional lineage is
persisted on the documents themselves: the predecessor coupon's `Exchange*` record names the successor
document and coupon, and each successor coupon carries `Predecessor*`. Nothing depends on an in-memory flag.

### Coupled Residual Semantics

If the source-approved exchange result fulfils a residual as an accountable EMD-S coupled to this exchange, it
is requested and recovered in the **same** `IEmdExchangePort` operation and materialized in the same local
checkpoint. No second document or value issuance is ever dispatched for that obligation. A confirmed exchange
whose required coupled residual evidence is missing, or which returns a residual document for an obligation
that owes none, reconciles without re-dispatching the exchange. Only an explicitly external/non-document
residual reaches `IExchangeResidualValuePort`, and only after document truth is durable.

### Deterministic Verification

`DeterministicEmdExchangeAdapter` in `AeroTech.Ordering.Providers.Deterministic`, in the durable-key shape:
a repeated key returns the remembered result, a conflicting immutable intent on a known key fails closed, an
unresolved outcome resolves on read-back, and a resolved refusal is never rewritten. The intent fingerprint
covers every materially binding field — source scope, beneficiary, successor semantics and targets, ticket
document, decision and source pricing references, and every coupled-residual field including its expected
instrument.

Contract coverage: `tests/AeroTech.Ordering.Persistence.Tests/Contracts/EmdExchange/` — 18 cases across the
reusable kit, the deterministic binding and the unconfigured 501 refusal.

Flow coverage: `EmdExchangeToNewEmdFlowTests` — 48 cases: G3H1–H6 (associated and standalone successors,
grouping, two independent groups, mixed G1/G2/G3 dispositions, multi-coupon partial exchange), G3S1–S3
(acceptance refusals), G3P1–P6 (provider lifecycle and crash recovery), G3R1–R4 (replay and identity),
G3M1–M6 (monetary and coupled residual), G3F1–F3 (frozen invariants).

Correction coverage: `EmdExchangeFreezeGateCorrectionTests` — 43 cases: C1–C4 (refund-due execution, unresolved,
refused and contradictory, and the `AddCollect + RefundDue` sequence), C5 (frozen monetary shapes), C6
(document-coupled non-EMD refusal), C7–C8 (coupled and external residual evidence), C9–C10 (provider coupon
number fidelity and malformed numbers), C11 (mapping cardinality), C12 (an existing number from another
operation), C13 (association and beneficiary evidence), C14 (deterministic request identity), C15 (mandatory
consequence), C16 (exact replay of a full monetary group). Plus
`ElectronicMiscDocumentIdentityPolicyTests` — 16 Domain cases covering each existing-document identity facet
directly, because the deeper facets are unreachable through the servicing flow: the only path that reaches the
policy is a fresh dispatch against a pre-existing document, which by construction came from a different
operation, so the origin-operation check always fires first.

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real EMD exchange authority is wired. `UnconfiguredEmdExchangeProvider` fails closed with
   `EmdExchangeSourceNotConfigured` (20313, 501). The reissue stays authoritative and the source ancillary
   stays detached and open.
2. Which exchange shapes the real authority permits. Ordering supports EMD-A → EMD-A, EMD-A → EMD-S,
   EMD-S → EMD-A and EMD-S → EMD-S because the industry permits all four; a provider that restricts them
   (for example to even exchanges only) must express that in its own result or ACL, not in a Domain rule.
3. Whether the real authority accepts a **multi-coupon group** as one accountable transaction, or requires one
   act per coupon. Ordering models the group because the source decides it; a per-coupon provider is an
   ACL-level adaptation.
4. Whether the real authority echoes the successor coupon identities and the association target. Ordering
   verifies every echoed field that is present and treats absence of the successor identity itself as
   contradictory.
5. Whether an EMD exchange is idempotent under Ordering's operation key on the real authority.
6. Whether the real authority returns a coupled residual EMD-S inside the exchange result, or expects a
   separate issuance. Ordering requires the former and reconciles rather than guessing.
7. Whether collection for an EMD exchange group is accepted on the same funding rail as a ticket exchange.
   Ordering reuses `IExchangeFundingPort` with group-scoped keys; its request field names (`QuotedExchangeId`,
   `PredecessorDocumentNumber`, `SuccessorDocumentNumber`) carry the exchange-group reference and the EMD
   document numbers, which is a naming imprecision in a frozen P3-F contract, not a semantic one.
8. Whether a refund arising from an EMD exchange is accepted on the same return-of-value rail as a ticket
   refund-due. Ordering reuses `IRefundValuePort` with a group-scoped key and the EMD source document number.
9. Whether the real authority returns coupon numbers that are stable across a read-back. Ordering persists the
   numbers from the first confirmation and treats a later differing set as a contradiction.

### Known Semantic Gaps

* `RetainAsResidual`, `Cancel` and `ManualReview` are executed by their own capabilities —
  `ICC-P3-ANCILLARY-RETENTION`, `ICC-P3-ANCILLARY-CANCEL` and `ICC-P3-ANCILLARY-MANUAL-REVIEW`.
* An exchange is never reversed. There is no EMD "exchange cancel", mirroring the G2 decision on Refund Cancel.
* A `NeedsReconciliation` EMD exchange has no automated operator remediation command. Every piece of evidence
  one would need is persisted on the group row and on both documents.
* A successor coupon of `Fee` purpose binds to the first pricing line of its own G3 consequence, because the
  aggregate requires a fee coupon to name a pricing line and that line does not exist until the consequence is
  committed. The consequence is therefore committed first **within the same local checkpoint**. A source that
  approves a fee-purpose successor with no commercial consequence at all is refused at acceptance.
* Ordering does not revalue, reprice or re-derive any successor term. A source that supplies incomplete terms
  is refused before the ticket document exchange is dispatched, never approximated.
* Merge and split EMD exchanges are not supported. The accepted disposition contract carries no explicit
  source-to-successor mapping, so only 1:1 is executable and any other cardinality is refused. This is a
  representation limit of the current contract, not an industry restriction.
* A standalone successor coupon carrying a ticket association is unconstructible: the aggregate already refuses
  it at issuance. The identity policy keeps the branch as defence, and it is unreachable by design rather than
  untested.

### Explicit Non-Responsibilities

Ordering does not decide whether an ancillary is exchangeable, does not choose the successor document type,
RFIC, RFISC, purpose or value, does not derive any successor term from the predecessor EMD, does not generate
a provider document number, does not compose exchange groups, does not un-exchange a coupon, and does not
reconcile a refused or contradicted act on its own.

---

## ICC-P3-SERVICING-FEE-DOCUMENT

### Capability

Documenting a servicing **Fee or Penalty that the accepted exchange pricing already contains** as a
stand-alone accountable document (EMD-S), on the explicit instruction of the source, without inventing an
`OrderService` and without moving any money.

### Authoritative Owner

Two authorities, and neither may be substituted for the other.

The **accepted pricing / exchange source** owns the economics: the fee or penalty exists, and its amount, only
because that source said so. The **same source** separately owns the *documentation decision*: whether that
charge is documented as an EMD-S at all, under which document reference, RFIC and RFISC, and with which
attributions.

Ordering owns neither. It owns only the accountable-document act and its evidence.

### Ordering Semantic Requirement

```text
an explicit source instruction
  -> one Standalone EMD per DocumentReference
  -> Fee coupons only, each with OrderServiceId null and AssociatedTicketCouponId null
  -> each coupon's PricingLineId is the exact committed primary exchange pricing line
  -> each explicit attribution becomes one EmdPriceLink to its exact committed line
  -> no new OrderChange, PriceChangeSet, OrderPricingChanged or CommercialVersion move
  -> no funding, refund or residual call of its own
  -> never issued before ticket exchange truth is durable
  -> ticket truth is never rolled back if the documentation later fails
```

**A Fee or Penalty component does not imply an EMD-S.** The repository benchmark freezes four legitimate
treatments — netted, separately collected, added to the replacement document, documented via EMD-S — and
Ordering records the approved one rather than assuming one. The G6 execution path contains no reference to
`PricingComponentType` at all; component type is read only when validating a line the source explicitly named.

**This is not an ancillary disposition.** No `AncillaryExchangeDisposition` value was added, the
`IAncillaryExchangeDispositionPort` is not consulted, `IEmdExchangePort` is not used and no residual coupling
is reused. It is an accountable-document consequence of the accepted pricing result.

**RFIC and RFISC are source/provider facts.** They are never derived from component type, description,
airline or any local table.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/DocumentIssuance/IEmdIssuancePort.cs` — the **frozen issuance port**,
reused unchanged. Document numbers come only from the `DocumentStock` aggregate; no number is generated in
application code. No `IServiceFeeEmdPort`, no `IPenaltyDocumentPort`, no second EMD aggregate.

```text
stable operation key   ProviderOperationKey(operation, "emd-fee:{DocumentReference}")
stock role             Emd:emd-fee:{DocumentReference}
document type          Standalone
coupons                Fee purpose, source RFISC, source amount, no service, no association
```

`ElectronicMiscDocumentIssuer` was neither reused nor modified: its plan builder is driven entirely by
`OrderService` + `EmdIssuanceSnapshot`, which G6 does not have. Only its stock-reservation protocol was
replicated.

### Request Evidence

The accepted exchange may carry `FeeDocuments`. Each document needs a non-blank stable `DocumentReference`, a
non-blank `SourceReference`, a valid issuer, a non-blank RFIC, a currency and at least one coupon. Each coupon
needs a non-blank RFISC, a non-blank `PrimarySourceLineRef`, a positive source-approved `DocumentedAmount` and
at least one explicit attribution, with the primary line among its own attributions. Malformed shapes are
`ServicingFeeDocumentMalformed` (20324, 422), all before any irreversible ticket work.

Every named line must resolve uniquely inside the accepted exchange pricing result by exact `SourceLineRef`.
The primary must be `Fee` or `Penalty`, `CustomerBalance`, `Debit` and not a `Transfer` role; additional
explicit attributions may add `Tax` under the same rule, so tax-on-penalty is documentable when the source
says so and never inferred. Anything else — `Commission`, `Discount`, `Fare`, `Adjustment`, `SettlementOnly`,
`Informational`, `Credit`, `Transfer`, a foreign currency — is `ServicingFeeDocumentLineNotEligible`
(20325, 422).

### Amount And Currency Conservation

Ordering calculates nothing. Per coupon, the attributions must sum exactly to the documented amount; per
accepted line, the attributions across **all** documents of the operation must not exceed its accepted debit;
per document, the coupon amounts sum to the total sent to the port. Violations are
`ServicingFeeDocumentAmountDoesNotReconcile` (20326, 422). No currency is converted and no missing remainder
is invented. One line may legitimately fund two documents while staying within its accepted amount.

### Recovery

The frozen stock protocol is the recovery mechanism: the document number is committed **before** the provider
call, so an existing non-retired allocation means a call may already have reached the provider and only
`RecoverAsync` may run. G6 is one notch stricter than the frozen issuer, which recovers only on `Reserved`.

```text
Confirmed          local EMD-S materialized, stock -> Issued, checkpoint settled
Pending / Unknown  AwaitingExternal, reservation retained, no local EMD-S, no second IssueAsync
Rejected           ticket and pricing truth retained, no local EMD-S, NeedsReconciliation
```

Before local materialization after a Confirmed, the document number, operation, type, issuer, traveller,
RFIC, currency, coupon count, RFISCs, amounts and pricing-line identities are revalidated. An exact match is
adopted idempotently; a conflict retains the provider confirmation, the stock evidence and the ticket/pricing
truth and reconciles without a second issue and without overwriting.

Price links are resolved from committed state only — the order's pricing lines filtered by the exchange's own
`PriceChangeSetId` and the exact `SourceLineRef` — on both the fresh and the replay path, so the two cannot
diverge. A missing, duplicated or out-of-set line reconciles after ticket truth.

### Local Truth

```text
ElectronicMiscDocument   Standalone
coupons                  Fee purpose, OrderServiceId null, AssociatedTicketCouponId null,
                         ExternalValueReference null, PricingLineId = exact committed primary line
price links              one EmdPriceLink per explicit attribution, exact line, source-approved value
provider evidence        provider reference recorded on the document
money                    none — no G6-owned value movement of any kind
```

These are the aggregate's already-frozen coupon rules, not new parallel ones: `EnsureCouponIsWellFormed`
already refuses a Standalone document carrying a ticket-coupon association and a `Fee` coupon whose
`PricingLineId` is null or whose `OrderServiceId` is set.

### Deterministic Verification

`ServicingFeeDocumentPolicyTests` (41, pure unit, no database) and `ServicingFeeDocumentFlowTests` (25,
end-to-end). The deterministic EMD issuance adapter gained only test configuration — crash-before and
crash-after hooks alongside its existing per-document outcome, recovery outcome and observed-request lists.

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real EMD-S provider adapter is wired. `UnconfiguredEmdIssuanceProvider` fails closed, and G6 is not
   treated as complete merely because no real adapter exists.
2. Carrier and location capability for EMD-S is unknown from inside Ordering.
3. Real RFIC/RFISC support is unverified; Ordering carries what it is given and interprets none of it.
4. Cross-carrier stock is unsupported. Stock is resolved by the instruction's own `IssuerCarrierId`, so the
   wrong carrier's stock can never be allocated; an instruction naming a carrier with no active EMD stock
   reconciles instead.
5. A provider needing a separate EMD-S **related-ticket** field is not representable. The
   `AssociatedTicketDocumentNumber` / `AssociatedTicketCouponNumber` / `AssociatedTicketCouponId` fields mean
   EMD-A association in this domain and were deliberately not overloaded to emulate one.
6. `RecoverAsync` cannot distinguish never-dispatched from unknown. The committed stock reservation preserves
   the no-duplicate fail-safe, at the cost that a crash between reserving the number and the call reaching
   the provider parks the operation at `AwaitingExternal` until readback resolves it. `DocumentIssuanceResult`
   was not widened.

### Known Semantic Gaps

* **Forfeiture, GL posting and receipt rendering are out of scope.** Ordering records the accountable document
  and its links to committed pricing; it posts nothing to accounting and renders nothing.
* **Components beyond `Fee`, `Penalty` and explicitly-attributed `Tax` cannot be documented.** Widening that
  set is a business ruling, not an implementation detail, so the shapes are refused rather than guessed.
* **A fee requiring value movement the accepted exchange monetary plan does not represent has no home here.**
  G6 creates no second collection, refund or residual.
* Three replay/conflict shapes — a Confirmed provider result against a conflicting local document number, an
  exactly-already-materialized identity, and a committed pricing line missing on resume — are implemented and
  guarded but not yet covered by an executable test, because each needs a state the atomic
  materialization-plus-checkpoint transaction does not naturally produce.
* A `NeedsReconciliation` fee document has no automated operator remediation command. Every piece of evidence
  one would need is persisted. That is P3-H.

### Explicit Non-Responsibilities

Ordering does not decide that a fee or penalty exists, does not compute one, does not decide whether it should
be documented, does not choose between EMD-S and tax or new-fare treatment, does not derive RFIC or RFISC,
does not convert currency, does not create an `OrderService` to carry the document, does not move any money
for it, and does not reconcile a refused or contradicted issuance on its own.

---

## ICC-P3-ANCILLARY-CANCEL

### Capability

Terminating a dependent ancillary after a ticket reissue **without returning any value**, on the authority of
the servicing source, by voiding the issued accountable document that carries it.

### Authoritative Owner

Two authorities, and neither may be substituted for the other.

The **ancillary disposition source** decides that the ancillary is cancelled without refund, under what
cancellation reference and with what document action. The **document void authority** — the issuer — decides
whether that document may actually be voided, and whether a refund is required instead. Ordering adjudicates
neither and derives neither.

### Ordering Semantic Requirement

```text
Cancel
  -> the issued EMD is voided as a whole document, and every coupon on it becomes Void
  -> no value returns to the customer, to a wallet, to a voucher or to any instrument
  -> no PriceChangeSet and no OrderPricingChanged
  -> each cancel-group service coupon cancels its exact dependent ancillary OrderService
  -> FinancialStatus and DeliveryStatus on that service are never rewritten
  -> no air service is ever mutated
  -> frozen ticket truth is never rolled back if the cancel later fails
```

**Cancel is not Refund.** Travelport states the distinction directly: `REFUND` voids the EMD and returns value
to the original form of payment, `VOID` voids the EMD and forfeits residual value. If the issuer answers
`RefundRequiredInstead`, Ordering does **not** run a refund — that is a new economic decision the source must
make.

**Cancel is not Retention.** Retention leaves the coupon `OpenForUse` and reusable; cancel voids it.

**Cancel is not a coupon-level act.** The frozen EMD void is whole-document, so a cancel group is executable
only when voiding the document cannot reach a coupon the source did not approve. Partial cancel-without-refund
is `BLOCKED_DECISION`, not a widened void.

### Ordering Port / Dependency Boundary

`src/AeroTech.Ordering.Domain/Ports/DocumentVoid/IDocumentVoidPort.cs` — the **frozen P3-C port**, reused
unchanged. No `IEmdCancelPort` was created and no service-specific void port exists.

```text
AccountableDocumentKind   ElectronicMiscDocument
document number           the source EMD
issuer                    the EMD's actual IssuerCarrierId
operation key             ProviderOperationKey(operation, "emd-cancel:{documentNumber}")
recovery                  frozen P3-C RecoverAsync semantics
```

G5 creates no child `ServicingOperationKind.VoidDocument` and no second `OrderChange`. The cancel group is a
checkpoint inside the one Exchange servicing operation.

### Request Evidence

The accepted decision must carry `AncillaryCancellationTerms`: a cancellation reference, a source reference
and a document action, alongside the binding decision reference, decision version and context fingerprint.
Only `AncillaryCancellationDocumentAction.VoidWithoutRefund` is executable; a future action is expressible
precisely so it can be **refused** — `AncillaryCancellationDocumentActionNotExecutable` (20319, 422).
Incomplete terms are `AncillaryCancellationTermsMissing` (20318, 422).

The target coupon must be an affected EMD-A coupon that is `OpenForUse`, carries `EmdCouponPurpose.Service`,
names an `OrderServiceId` and resolves to a non-air service of the same order, else
`AncillaryCancellationTargetNotEligible` (20320, 409). Voiding an accountable document is attributable, so a
caller with no actor is refused with `AncillaryCancellationRequiresAuthority` (20323, 422). Every one of these
fails before the reservation change and before the ticket document exchange.

### Whole-Document Scope Rule

Before any irreversible ticket work, for each EMD carrying `Cancel` dispositions, every `OpenForUse` coupon on
that document must be represented in the accepted decision with disposition `Cancel` and coherent terms, and
no coupon may already be terminal. Otherwise `AncillaryCancellationScopeWiderThanApproved` (20321, 409). The
rule lives once, on the aggregate, as `ElectronicMiscDocument.WholeDocumentCancellationConflict`, so
acceptance and execution cannot disagree.

### Response Evidence

| Provider answer | Ordering |
| --- | --- |
| eligibility `Allowed` | proceed to the void act |
| eligibility `PendingEvidence` | `AwaitingExternal`, claim retained, nothing dispatched, nothing local |
| eligibility `Denied` | `NeedsReconciliation`, ticket truth retained |
| `RefundRequiredInstead` | `NeedsReconciliation`, **no refund port, no value movement, no local void** |
| void `Confirmed` | local compatibility re-verified, then EMD `Voided`, coupons `Void`, one void record, one `DocumentVersion` move |
| void `Pending` / `Unknown` | `AwaitingExternal`, readback only, never a blind redispatch |
| void `Rejected` | `NeedsReconciliation`, EMD unvoided |

A provider `Confirmed` against a local document that no longer matches the approved group retains both the
ticket truth and the provider confirmation, reconciles, and never dispatches a second void or fabricates a
local one.

### Recovery

`DocumentVoidResult` carries no `WasDispatched`, unlike `IEmdExchangePort`. Rather than widen a frozen
contract, Ordering writes and commits its own dispatch claim, `VoidDispatchedAt`, **before** the provider call:
a null claim means nothing was ever sent and the void may be dispatched; a set claim means a call may have
reached the provider and only `RecoverAsync` may run. A duplicate provider void is therefore impossible.

### Durable Evidence And Idempotency

One additive table, `Order.AcceptedExchangePlanAncillaryCancelGroups`, keyed on
`(OperationId, CancelGroupRef)` where the reference is derived as `emd-cancel:{documentNumber}` — the source
cannot split or merge cancel scope. It carries the source references, the document action, the member coupon
numbers, the exact order service ids, the eligibility evidence, the dispatch claim, the void evidence and
`CancellationSettledAt`. Six additive columns on the accepted disposition carry the per-coupon terms.

`CancellationSettledAt` is written with `??=` and is the only proof that a cancel actually settled. A
pre-existing `Cancelled` service is a conflict, never proof. The EMD void, the service transitions and the
settlement checkpoint commit in one `SaveChangesAsync`.

**One `CommercialVersion` move per committed cancel group**, not one per service.

### Deterministic Verification

`AncillaryCancelFlowTests` (29). The deterministic and unconfigured `IDocumentVoidPort` adapters keep their
frozen production contract; the deterministic one gained only test configuration.

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real ancillary disposition source is wired, so no real carrier ever returns `Cancel`.
   `UnconfiguredAncillaryProvider` fails closed with 501.
2. No real document-void authority is wired for miscellaneous documents.
   `UnconfiguredDocumentVoidProvider` fails closed. Whether a real issuer distinguishes "void, forfeit
   residual" from "void, refund to FOP" the way Travelport documents is unverified from inside Ordering.
3. `RecoverAsync` cannot report whether a call was ever dispatched. Ordering's own pre-dispatch claim
   guarantees no duplicate void, at the cost that a crash between writing the claim and the call reaching the
   provider parks the operation at `AwaitingExternal` until the provider's readback resolves it. Adding
   `WasDispatched` to the frozen P3-C result would remove that, and is a P3-C decision.
4. Whether a real issuer's void is idempotent under the `emd-cancel:{documentNumber}` operation key.
5. Whether a real issuer returns `RefundRequiredInstead` as `Denied` plus the flag or `Allowed` plus the flag.
   Ordering honours the flag either way and refunds in neither.

### Known Semantic Gaps

* **Partial cancel-without-refund is `BLOCKED_DECISION`.** One coupon forfeited while its siblings survive
  cannot be represented by a whole-document void, and the benchmark authorises no coupon-level forfeiture
  state. The shape is refused, not approximated.
* **`Fee`, `Deposit` and `ResidualValue` coupons cannot be cancelled by G5** — they carry no delivering
  service, so there is no commercial consequence and no source answer to where the value went.
* `VoidReason` has no member describing "cancelled without refund as a consequence of a reissue". The void is
  recorded as `VoidReason.Other` with a reason detail naming the approved action and both source references.
  Adding an enum member is a wire-contract change and was not made here.
* Forfeiture accounting is **not** implemented. Ordering records that no value returned; it writes no
  breakage, forfeiture or revenue-recognition entry.
* A `NeedsReconciliation` cancel group has no automated operator remediation command. Every piece of evidence
  one would need is persisted. That is P3-H.

### Explicit Non-Responsibilities

Ordering does not decide whether an ancillary may be cancelled, does not decide whether a document may be
voided, does not convert a cancel into a refund, does not compute or account for forfeited value, does not
issue any fee or penalty document, does not roll back ticket truth for a failed cancel, and does not reconcile
a refused or contradicted void on its own.

---

## ICC-P3-ANCILLARY-MANUAL-REVIEW

### Capability

Recording that a dependent ancillary was explicitly **excluded from automated servicing** by the source,
because the supplier or provider cannot be automated and a human must handle it.

### Authoritative Owner

The ancillary disposition source alone, and there is **no second authority**, because there is no external
act at all.

### Ordering Semantic Requirement

```text
ManualReview
  -> a valid, source-approved servicing outcome, not an error and not a failure
  -> the ticket exchange is allowed to proceed
  -> the ancillary coupon stays OpenForUse and detached, with its G1 provenance intact
  -> no provider operation, no value movement, no commercial service mutation
  -> the operation ends NeedsReconciliation once every automatable disposition has settled
  -> the disposition stays intentionally unresolved and visible for reconciliation tooling
```

It is never marked `Confirmed` merely to make the operation completable, and it never blocks unrelated safe
automated ancillary work in the same reissue.

### Ordering Port / Dependency Boundary

None. `ManualReview` rides on the existing
`src/AeroTech.Ordering.Domain/Ports/AncillaryDisposition/IAncillaryExchangeDispositionPort.cs` result. No
manual-review integration port was invented, no operation key exists because there is no provider operation to
key, and no `Pending`/`Unknown`/`Recover`/`WasDispatched` rail was created for it.

### Request / Response Evidence

The accepted decision must carry a non-blank actionable reason. The source `Detail` is used, persisted to its
own `ManualReviewReason` column so it is unambiguous rather than sharing a free-text field. A `ManualReview`
with no reason is `AncillaryManualReviewReasonMissing` (20322, 422) before any irreversible ticket work; a
reason is never fabricated. A `ManualReview` arriving with a refund, exchange, retention or cancellation
consequence attached is refused as a malformed decision (20297).

No new manual-review aggregate exists.

### Durable Evidence And Idempotency

`Disposition`, `DecisionReference`, `DecisionVersion`, `DecisionContextFingerprint` and `ManualReviewReason`
on the accepted ancillary disposition. Replay of an operation already in `NeedsReconciliation` redispatches
no ticket work and no ancillary work, and leaves the manual evidence untouched.

### Ordering And Isolation

`ManualReview` is deliberately excluded from the settlement selector, so it can never be picked, can never
block and can never starve later automation — even when it sorts first by document number. It **is** included
in mechanical G1 detachment, because an affected coupon's association must not survive the reissue. Completion
is gated once, centrally: a plan carrying an unresolved manual review reconciles instead of completing.

A `ManualReview` on one coupon of an EMD prevents cancelling another coupon of the same EMD, because the
whole-document void would terminate the manual-review coupon. That shape fails closed before the ticket
exchange with 20321; the cancel is never processed first.

### Deterministic Verification

`AncillaryManualReviewFlowTests` (7).

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real ancillary disposition source is wired, so no real carrier ever selects `ManualReview`.
   `UnconfiguredAncillaryProvider` fails closed with 501.
2. Whether a real source expresses "cannot be automated" as a disposition at all, rather than by omitting the
   ancillary or by failing the call. Ordering supports only the explicit disposition and refuses the others.
3. Whether a real source supplies a reason an operator can act on. Ordering requires a non-blank reason and
   interprets none of it.

### Known Semantic Gaps

* **Resolution is not implemented.** Nothing in Ordering resolves a manual review, and no servicing command
  exists to close one out. The operation stays `NeedsReconciliation` until a future explicit servicing
  decision addresses it. That is P3-H.
* The reason is free text. Ordering neither classifies it nor routes on it, so no read model can group manual
  reviews by cause.

### Explicit Non-Responsibilities

Ordering does not decide that an ancillary needs manual handling, does not invent a reason, does not resolve
the review, does not notify anyone, does not mutate the ancillary or its service while the review is open, and
does not complete the servicing operation while one remains.

---

## ICC-P3-ANCILLARY-RETENTION

### Capability

Recording that an existing ancillary accountable value stays **reusable** after a ticket reissue, on the
authority of the servicing source, without creating any new accountable document or value instrument.

### Authoritative Owner

The ancillary disposition source alone. It decides whether the ancillary is retained at all, under what
retention reference, and in what retention mode. Ordering adjudicates none of it and derives nothing from it.

There is **no second authority in this capability**, because there is no external act: no document authority is
called, no funding authority, no return-of-value authority. This is the only executable ancillary disposition
with no provider rail.

### Ordering Semantic Requirement

```text
RetainAsResidual
  -> no new EMD, no EMD-S, no voucher, no wallet, no travel bank, no credit shell
  -> the existing source EMD coupon remains the accountable value carrier
  -> that coupon stays OpenForUse and detached, with its G1 DisassociatedByReissue provenance intact
  -> no money or value moves now
  -> no PriceChangeSet and no OrderPricingChanged
  -> the old dependent ancillary OrderService becomes non-deliverable
  -> durable retention evidence is persisted on the accepted disposition
```

**This is not an accountable-document exchange.** If the source wants `old EMD -> new residual EMD-S`, that is
`ExchangeToNewEmd` and it must go through `IEmdExchangePort` — see `ICC-P3-EMD-EXCHANGE`. The two state
machines and their persistence are deliberately separate.

**The exact future reusable amount is never calculated or guaranteed by Ordering.** No amount appears in the
retention terms, the accepted disposition or persistence. Nothing is derived from the EMD issuance value, a
pricing allocation, used value, penalty or fee. The value of the retained coupon is re-evaluated by an
authoritative source when it is next used, which this capability does not perform.

**Wallet, voucher, stored-value and credit-shell balances are outside Ordering entirely.** A source that asks
for one through this disposition is refused, not translated.

### Ordering Port / Dependency Boundary

None. The retention terms ride on the existing
`src/AeroTech.Ordering.Domain/Ports/AncillaryDisposition/IAncillaryExchangeDispositionPort.cs` result, so this
capability adds no provider surface. No operation key exists for retention because there is no provider
operation to key, and no `Pending`/`Unknown`/`Recover`/`WasDispatched` rail was invented for it.

### Request / Response Evidence

The accepted decision must carry `AncillaryRetentionTerms`: a retention reference, a source reference and a
retention mode, alongside the existing binding decision reference, decision version and context fingerprint.

Only `AncillaryRetentionMode.ExistingEmdCouponReusable` is executable. `NewMiscellaneousDocument`, `Voucher`,
`StoredValue`, `ExternalInstrument` and `CreditShell` are expressible precisely so they can be **refused**
rather than being inexpressible — `AncillaryRetentionModeNotExecutable` (20317, 422), before the reservation
change and before the ticket document exchange. Incomplete terms, or retention arriving together with an
immediate monetary or exchange consequence, are `AncillaryRetentionTermsMissing` (20316, 422).

### Durable Evidence And Idempotency

Four additive fields on the accepted ancillary disposition — `RetentionReference`,
`RetentionSourceReference`, `RetentionMode`, `RetentionSettledAt` — and nothing else. No generic
servicing-consequence table, no EMD document-history mutation, no in-memory flag. `RetentionSettledAt` is the
proof that retention actually settled, written with `??=` so a replay never moves it, which is what makes a
retained ancillary distinguishable from one merely left detached by accident.

Retention settles only when the exact source coupon is `OpenForUse`, carries no association, has no refund or
exchange record, and was disassociated **by this servicing operation**. Any other state reconciles: ticket
truth stays authoritative, no document is mutated, no value moves, and the coupon is never pulled back from
another association.

### Real-Service Verification Status

`BLOCKED_INTEGRATION`.

### BLOCKED_INTEGRATION

1. No real ancillary disposition source is wired, so no real carrier ever returns a retention decision.
   `UnconfiguredAncillaryProvider` fails closed with 501.
2. Whether a real source expresses retention as a distinct disposition at all, or instead as a residual EMD-S
   exchange. Ordering supports both shapes and keeps them separate; which one a carrier uses is its choice.
3. Whether the real source supplies a stable retention reference that can later be presented to redeem the
   retained value. Ordering persists whatever reference it is given and interprets none of it.
4. Whether a retained EMD coupon is honoured by the real accountable-document authority on a later reshop.
   Ordering asserts only that the coupon remains open and detached; it makes no promise about redemption.

### Known Semantic Gaps

* Redemption of retained value is **not** implemented. Nothing in Ordering consumes a retained coupon, reshops
  it, or converts it to money or a new document. The retained coupon is simply left open, detached and
  evidenced.
* No reusable amount is stored, so no report or read model can state "how much" is retained. That is
  deliberate: any number Ordering wrote would be derived, and a derived residual balance is exactly what this
  capability refuses to invent.
* `Cancel` and `ManualReview` are executed by `ICC-P3-ANCILLARY-CANCEL` and
  `ICC-P3-ANCILLARY-MANUAL-REVIEW`. Retention never becomes a cancel: retention leaves the coupon open and
  reusable, cancel voids the whole document and forfeits its value.
* A retained coupon moved onto an association this exchange did not detach it from is handled in two ways
  depending on when it happens, corrected in the G4 freeze correction. **Before** any document truth exists,
  the frozen G1 association guard refuses the reissue outright
  (`ElectronicMiscDocumentAssociationMoved`, 20302, 409). **After** the ticket exchange has confirmed and the
  successor is durable, the operation reconciles instead: ticket truth stays authoritative and no fresh
  business refusal is raised at that stage.
* A retention with no delivering `OrderServiceId` produces no commercial service consequence and no
  `CommercialVersion` move. That is correct — there is no service to withdraw — but it means such a retention
  leaves no trace on the order aggregate beyond the accepted plan's evidence.

### Explicit Non-Responsibilities

Ordering does not decide whether an ancillary is retainable, does not compute or guarantee a reusable amount,
does not create or hold any voucher, wallet, travel-bank or credit-shell balance, does not convert the
existing EMD into another document, does not refund or move value at retention time, does not redeem retained
value, and does not reconcile a conflicting coupon state on its own.
