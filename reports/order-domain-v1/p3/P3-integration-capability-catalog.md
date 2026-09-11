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

Entries in this revision: `ICC-P3-EXCHANGE-AIRPRICE`, `ICC-P3-EXCHANGE-INVENTORY`,
`ICC-P3-EXCHANGE-DOCUMENT`, `ICC-P3-EXCHANGE-USAGE`.

Capability scope of this revision: **partially-used even reissue** — a predecessor electronic ticket with at
least one `Used` coupon and at least one `Open` coupon. The reissue scope is **all** `Open` coupons. `Used`
coupons are historical pricing context only. The governing invariant is:

```text
pricing context != document mutation scope
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

### Known Semantic Gaps

* Only `Even` is accepted. Add-collect, residual and penalty settlement are out of scope for this bundle.
* `FareConstructions` is a pass-through snapshot. Ordering does not validate it against the reissue scope and
  intentionally omits `BrandName`, `CreatedAt`, foreign keys and line items from the projection.
* The historical context carries no consumed-operational-segment evidence. See `ICC-P3-EXCHANGE-USAGE`.

### Explicit Non-Responsibilities

Ordering does not price, re-price, calculate `FareUsed`, apply fare or tax rules, compute penalties or
residual value, perform FX, or derive a monetary outcome. `PricingSource.OrderingDerived` is never produced.
No payment, stored value, wallet or ledger movement belongs to this capability.

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
**before** the document exchange. Plan-level Apply and Recover semantics are unchanged by this bundle.

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
  -> inventory mutation
  -> document exchange   [irreversible]
  -> persist raw successor evidence
  -> single local finalization transaction
```

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
application capability limit (`ExchangeCouponStateNotSupported`, code 2976) before any irreversible work. That
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
covered only by a non-`Open` coupon is refused there with `CouponIsNotExchangeable` (code 2997) — the most
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
