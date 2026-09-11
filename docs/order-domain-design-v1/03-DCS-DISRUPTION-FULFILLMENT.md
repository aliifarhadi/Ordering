# 03 - DCS, Disruption and Fulfillment Integration

## 1. Integration ownership

The Ordering microservice participates in fulfilment and delivery without becoming the canonical owner of every external domain.

```text
Offer/Pricing      -> accepted product/price/terms
Inventory          -> capacity/reservation truth
Payment            -> payment/refund/chargeback truth
Ordering           -> commercial Order truth
Document module    -> ETKT/EMD fulfilment truth
DCS/Delivery       -> check-in/boarding/consumption truth
Flight Operations  -> current flight/schedule truth
Disruption         -> disruption/recovery truth
Ledger             -> accounting truth
```

Ordering stores durable links/projections where required for practical querying and decision-making.

---

## 2. Reservation / Inventory integration

### 2.1 Outbound request

Ordering initiates fulfilment for Services that require reservation/confirmation.

Required normalized command shape (adapter mapping is explicit):

```text
ReserveOrderServices
--------------------
RequestId
OperationId
StepId
RequestHash
OwnerAirlineId
OrderId
CommercialVersion
Provider/SupplierContext
Services[]
  OrderServiceId
  ServiceType
  TravelerRef
  Segment/coverage snapshot
  ProductRef
  InventoryProductRef?
  Cabin/RBD?
  Quantity?
ExpiresAt?
```

For air, Inventory remains canonical for seat/capacity availability.

### 2.2 Inbound outcomes

```text
ReservationConfirmed
ReservationRejected
ReservationExpired
ReservationReleased
ReservationOutcomeUnknown
ReservationReconciled
```

Each event should carry:

```text
EventId
RequestId/CorrelationId
OrderId
ExternalReservationRef?
AffectedOrderServiceIds[]
OccurredAt
ProviderVersion?
```

### 2.3 Local handling

Create/update FulfillmentReservation and PER-SERVICE outcome rows. Durable operation/step intent and the idempotency key commit before any provider dispatch. A final header status cannot hide mixed Confirmed/Unknown members.

Do not transition global Order to `Confirmed`, `ReserveFailed` or `ReservationUnconfirmed` as the only business truth.

Read model derives:

```text
ReservationSummary:
  Confirmed | Partial | Pending | Unknown | Rejected | Released
```

### 2.4 Unknown outcome

Unknown outcome is a first-class state of the external reservation binding, not an excuse to guess.

Policy:

1. keep affected Services commercially intact;
2. set the unresolved reservation members to Unknown and recompute the root summary;
3. block destructive duplicate retry unless provider idempotency/reconciliation says safe;
4. run reconciliation/query-provider workflow;
5. resolve each reservation member to Confirmed/Rejected/Released/Expired from authoritative evidence;
6. publish resolved event.

Current `FulfillmentTask`/`ProviderInteraction` machinery can implement this workflow.

---

## 3. Payment and value integration

### 3.1 Canonical ownership

Ordering does not own a Payment aggregate or a stored-value balance.

The current platform owner(s) are responsible for the actual economic execution they expose, including authorization/capture/refund/release, tender/payment-method/provider outcome and stored-value movements. Ordering owns only the commercial obligation plus confirmed application/coverage/value-movement evidence needed to decide and audit Order operations.

If Payment versus StoredValue responsibility for a particular tender is not established by the current platform contract, the agent resolves it from existing service contracts or raises `BLOCKED_DECISION`; it does not create a local abstraction that silently becomes the new owner.

### 3.2 Inbound confirmed facts

The boundary must distinguish at least these semantic facts when the external owner exposes them:

- value/payment applied to an Order/servicing obligation;
- application reversed/transferred;
- refund/value-return completed;
- chargeback or other post-payment reversal;
- coverage/authorization guarantee granted, consumed or released;
- pending/unknown/reconciled provider outcome.

Exact event names and wire representation are mapped from the current owner contract. Required semantic identity is a stable external movement/application/guarantee reference, affected Order/operation/scope, confirmed monetary value using the platform contract, source version/effective time and lineage to the original movement when applicable.

A confirmed external movement is applied locally once. The same economic effect cannot be counted once as “application reversal” and again as an independent “refund” unless the owner contract proves they are distinct movements. Contradictory payloads for the same stable external identity are quarantined/reconciled. Missing confirmed value is never replaced with the requested amount or current Order total.

### 3.3 Local visibility and coverage

Ordering keeps a rebuildable projection/evidence view of confirmed external applications/coverage. It does not expose PSP capture methods on the Order aggregate.

A coverage/credit guarantee used to authorize issuance must state enough from its owner contract to prove:

- stable guarantee identity;
- applicable Order/change/service scope;
- available/consumed amount according to the existing monetary representation;
- validity/version;
- whether issuance is permitted against that guarantee.

A configured credit limit, pending payment request or UI “Paid” label is not sufficient evidence.

Whole-Order coverage is valid when the external owner explicitly covers the whole obligation. Scoped coverage is required when restrictions or parallel partial issuance could otherwise promise the same value to more than one irreversible operation. Ordering does not allocate each tender to every fare/tax line merely to fit a domain diagram.

### 3.4 Outbound intents

Payment/refund/release/transfer requests use the current Payment/StoredValue adapter contract and one durable Ordering operation/step identity. Persist the intent before dispatch. A transport retry keeps the same economic identity and equivalent payload; attempt number is diagnostics, not a new charge/refund identity.

A refund request carries only the amount/disposition/source references from the accepted refund/change decision plus the original application/tender/value references required by the owner contract. Ordering never recalculates “what to refund” inside the payment adapter.

### 3.5 Unknown or partial outcome

Timeout/ambiguous transport failure after dispatch is `Unknown` unless the real provider contract guarantees non-execution. Unknown is reconciled through the same operation/reference before any new economic operation is created.

Partial confirmed value remains partial. Excess value requires explicit disposition. Authorization release, capture refund, chargeback and stored-value return are separate owner facts and cannot be inferred from each other.

Commercial cancellation/refund acceptance and actual cash/value completion remain separately observable. This is required for support, audit and `ServicingRecord` redisplay.

### 3.6 Production release gate

A mock provider is not proof of production payment behavior. Before enabling an irreversible Order flow against real money/value, the actual owner adapter must have contract tests for stable idempotency identity, partial/unknown outcomes, authoritative read-back/reconciliation and the exact coverage/refund semantics used by the slice.

---


## 4. Document fulfilment

### 4.1 Ticket issuance

Application flow:

```text
IssueEligibleServices
-> validate commercial/reservation/payment eligibility
-> persist operation/coverage evidence and reserve DocumentStock number if local issuer
-> dispatch same-key provider issuance or perform local issuance
-> reconcile/verify confirmed result
-> persist ElectronicTicket + coupons
-> link coupons to OrderService IDs
-> publish TicketIssued
```

### 4.2 Partial issuance

The domain must represent partial issuance even if the normal UX requests all passengers together.

Reasons include:

- provider partial success;
- provider timeout after partial commit;
- document stock failure between travelers;
- specific traveler/document validation failure.

No global `OrderStatus=Ticketed` can be authoritative.

Read model derives:

```text
DocumentSummary = None | Partial | Issued | Exchanged | Refunded | Mixed
```

### 4.3 Ticketing unknown outcome

Use provider reconciliation before allocating/reissuing another number when there is a possibility that a document was already issued.

Technical workflow records the durable attempt INTENT before dispatch. An uncertain stock number remains reserved/retired, never reused for another document. The issued document and final provider interaction are created/updated only from confirmed evidence. Receipt of a late success after a local cancellation decision creates reconciliation work; do not discard the issued document or allocate a fresh number.

---

## 5. DCS / Delivery integration

## 5.1 Principle

DCS/Delivery is a first-class source of operational observations.

It does not send commands such as:

```text
Set OrderStatus = Flown
```

Instead it sends facts tied to the smallest correlatable Service/Traveler/Segment scope.

### 5.2 Preferred modern correlation

Preferred message identity:

```text
OrderId
OrderServiceId
TravelerId
SegmentId
```

Legacy fallback may use:

```text
PNR/RecordLocator
TicketNumber/CouponNumber
Passenger identity
Carrier/Flight/Date
```

Ordering maintains COMMAND-SIDE correlation indexes in the same transaction as creation/issuance/split, so incoming airport events do not depend on a lagging UI projection. Read-side indexes can mirror them for search. Name-only correlation is never sufficient. Multiple candidate matches are unresolved evidence, not a guessed passenger.

### 5.3 Inbound DCS event catalogue

Minimum supported event families:

```text
PassengerCheckInChanged
  CheckedIn
  CheckInCancelled

PassengerBoardingChanged
  Boarded
  Offloaded

PassengerTravelOutcomeRecorded
  Flown
  NoShow
  DeniedBoarding

PassengerStandbyChanged
  StandbyListed
  StandbyCleared

SeatAssignmentChanged

AncillaryDeliveryChanged
  ServiceStarted
  ServiceConsumed
  ServiceNotClaimed
  ServiceFailed

BaggageHandlingObserved
  Accepted
  Loaded
  Delivered
  Mishandled?          # if required by scope
```

Normalize provider-specific messages into internal integration facts before domain processing.

### 5.4 DeliveryObservation processing

On each inbound event:

1. Deduplicate source message by `(SourceSystem, SourceEventId, Consumer)`; one message may contain many passenger/service facts.
2. Normalize each fact with stable ObservationKey, Aspect, SourceEpoch/Sequence and correction reference when supplied.
3. Resolve canonical current OR historical service correlation; unresolved/ambiguous facts are durably quarantined and replayable after mapping, never dropped.
4. Append each observation uniquely by `(SourceSystem, SourceEventId, ObservationKey)`. Same identity/different payload is an integrity exception.
5. Apply per-aspect ordered transition rules; update ServiceDeliveryState and the necessary coupon/control state under concurrency checks.
6. Commit observations, state, Inbox, required outgoing event and local read projection in one local transaction.
7. Schedule an explicit policy evaluation/operation for commercial consequences. Receipt of a DCS fact is not permission to charge/refund/cancel a service.

### 5.5 Sequencing and corrections

Track source version separately for CheckIn, Boarding, Travel, Seat, Baggage and Consumption aspects. A newer seat assignment does not make a lower-sequence flown fact stale unless they belong to the same documented source stream. Persist SourceEpoch across source reset/restart; do not compare sequence numbers from different epochs/sources as one clock.

If source sequence is present, apply it only within its defined scope. Otherwise use source occurrence time plus allowed transitions, and quarantine ambiguous contradictions. ReceivedAt is audit data, never a last-write-wins business ordering rule. Older evidence is retained but does not blindly overwrite newer state.

A legitimate later Offloaded after Boarded changes the boarding aspect; it does not erase the earlier milestone. `Flown` is not inferred from flight departure or lack of a NoShow message. `NoShow` requires the agreed authoritative travel-outcome fact. Terminal reversal requires an explicit authoritative correction with `SupersedesObservationId` or a traceable source correction reference, reason and newer revision; it is not an ordinary delayed CheckedIn message.

### 5.6 Current state reducer

ServiceDeliveryState stores versioned aspect values and derives a summary: confirmed Flown/complete consumption -> Delivered; authoritative NoShow with no superseding travel completion -> NotClaimed; authoritative DeniedBoarding/provider refusal -> FailedToDeliver; boarding/check-in/standby progression -> InProgress or Ready according to the certified mapping; authorized ready scope with no progress -> Ready; otherwise NotReady. ServiceFailed/Unable/Removed/Suspended remain explicit provider states, not automatic commercial cancellation.

`DeniedBoarding` and `NoShow` are deliberately different facts: the former is airline/provider non-delivery and the latter is passenger non-claim. Through check-in may update several service/check-in aspects in one batch, but each service/aspect retains its own semantic key/version. A terminal `Flown` contradicted by a later authoritative correction is retained as evidence and routed through the certified correction/reconciliation rule; arrival order never silently erases a terminal fact.

Maintain DeliveredQuantity/Unit and coverage-portion status where needed. BaggageLoaded is not automatically BaggageDelivered; baggage acceptance does not mean unused baggage allowance can be refunded blindly. A multi-portion bag/meal/pass is Delivered only when its agreed delivery obligation is satisfied. Airline/DCS-certified event mappings supply the actual meaning; unknown mappings remain Unresolved.

Local current state and observations are state-plus-evidence, not a requirement to rehydrate the Order from an event stream. Rebuilding a reducer is an operational tool, not the normal GetOrder execution path.

---

### 5.7 Commercial consequences

Examples:

#### No-show

```text
DCS NoShow
-> record observation
-> DeliveryState = NotClaimed
-> NoShowPolicyEvaluator identifies affected active downstream Services
-> request Pricing/servicing calculation if policy requires action
-> explicit OrderChange cancels/reprices remaining Services
```

#### Flown

`Flown` normally prevents voluntary cancellation/void of the consumed air Service/coupon, but this is enforced by servicing/document policy, not by replacing Order commercial state.

---

## 6. Ordering -> DCS/Delivery messages

Ordering must also publish changes needed by delivery systems.

Recommended normalized outbound event families:

```text
OrderServiceReadyForDelivery
OrderServiceChangedForDelivery
OrderServiceCancelledForDelivery
TravelerDetailsChangedForDelivery
SeatProductChangedForDelivery
BaggageEntitlementChangedForDelivery
DocumentIssuedForDelivery
DocumentVoidedForDelivery
OrderSplitForDelivery
OrderReaccommodatedForDelivery
RequestDocumentControlRelease
DocumentControlReleaseAcknowledged
DeliveryChangeAcknowledged
DeliveryChangeRejected
```

Payload should contain enough immutable delivery context to avoid synchronous callback to Ordering for every airport action:

```text
EventId
OrderId
OrderReference
OrderServiceId
TravelerSnapshot
ServiceType
Segment/current flight correlation
Relevant service details
Relevant document/coupon ref
Relevant accepted entitlement/product terms
CommercialStatus
OccurredAt
Version
```

Do not publish unrelated pricing internals to DCS unless delivery needs them. Publish only the protected passenger/service data that the certified DCS actually requires; PII payload retention is managed as described in `01` section 22. Full snapshots for delivery are not permission to duplicate all passports in permanent financial outbox history.

Every delivery update carries a monotonically increasing DeliveryPublicationVersion for the stable ServiceId and current owner mapping. DCS acknowledgment identifies EventId/ServiceId/version and its applied or rejected result. Sending a cancellation notification is not evidence that airport control was released. RequestDocumentControlRelease is a command with a correlated acknowledgment; serving permission is not fabricated from notification publication.

Initial readiness is published only after required reservation/document/coverage eligibility is met. A price-only update that does not affect delivery does not resend a full passenger manifest. Split and reaccommodation publish old/new service and document bindings plus dependent ancillary treatment so late DCS facts remain correlatable.

---

## 7. Flight Operations integration

### 7.1 Inbound events

```text
FlightScheduleChanged
FlightDelayed
FlightCancelled
FlightReinstated
FlightDiverted
FlightReturnedToOrigin
AircraftChanged
OperatingCarrierChanged
DepartureAirportChanged
ArrivalAirportChanged
```

### 7.2 Processing and high-fan-out rule

1. deduplicate and persist the FlightOps source event/current flight state once;
2. create/continue a durable `FlightImpactFanoutJob` keyed by source event/version;
3. use `ExternalFlightId`/flight key index to resolve affected JourneySegments/OrderService IDs without loading every Order;
4. process affected Orders in resumable batches, but obtain a separate short per-Order projection/operation fence and SQL transaction for each Order;
5. update that Order's `SegmentOperationalState`/impact view and commit its local read projection atomically;
6. forward/emit impact context to Disruption when policy requires it;
7. never hold one transaction/fence across all N affected Orders and never perform provider I/O inside an Order transaction;
8. do not rewrite SoldScheduleSnapshot.

A flight-wide event is therefore **asynchronous across Orders but atomic within each completed Order update**. The fan-out job stores cursor/counts/failures and is safe to resume/replay. A duplicate source event does not create a second business impact for an already-applied `(SourceEventId, OrderId)`.

`SegmentOperationalState` may bind one sold JourneySegment to multiple current operational legs. A technical/intermediate stop therefore updates leg evidence without manufacturing a second commercial AirTransportService. Conversely, two separately sold passenger segments remain two Services even if FlightOps reports one through-flight identity. Flight identity correlation must include carrier/flight/date/board/off or the platform's stable `ExternalFlightId`; flight number alone is never sufficient.

**P5 capacity release gate (baseline, production-like deployment):** a `FlightCancelled` affecting 1,000 indexed Orders must persist/acknowledge the source event without waiting for all Orders, complete local fan-out within 30 seconds, perform no external provider call under an Order transaction/fence, and keep every normal per-Order transaction below 500 ms. The load test also asserts no lost projection update when DCS/servicing writes race the fan-out. A deployment may tighten these numbers; loosening them requires measured evidence and an ADR. This gate is not an airline-domain invariant.

### 7.3 Schedule change vs disruption

A schedule update is an operational fact.

A disruption is a business/operational impact that may require recovery.

```text
ScheduleChanged != DisruptionImpact
```

A five-minute time adjustment may not require any servicing; a cancellation, airport change or broken connection likely does.

Diversion and return-to-origin update operational outcome/actual-airport evidence without rewriting the sold origin/destination. `FlightReinstated` restores the operational flight fact only; it does not resurrect Services already replaced by a committed reaccommodation. Cabin downgrade/upgrade and oversale/denied-boarding are explicit impact reasons. A free involuntary upgrade creates no customer charge unless an accepted pricing/policy result says otherwise.

---

## 8. Disruption integration

### 8.1 Canonical owner

If the platform has a dedicated Disruption/Recovery service, it owns:

- disruption definition;
- affected-passenger analysis policy;
- recovery strategy/options;
- reaccommodation optimization;
- operational recovery state.

Ordering owns the resulting commercial Order change and its durable application progress, NOT the global disruption optimization. It returns accepted/pending/rejected/completed outcomes to the Disruption owner with InstructionId/OperationId; both sides can reconcile without guessing.

### 8.2 Inbound disruption messages

```text
DisruptionImpactDetected
DisruptionImpactUpdated
DisruptionImpactResolved
RecoveryOptionSelected
ReaccommodationInstructionIssued
```

Minimum impact payload:

```text
ExternalDisruptionId
OrderId
AffectedOrderServiceIds[]
AffectedTravelerIds[]
AffectedSegmentIds[]
Reason
ImpactType
Severity?
RecoveryReference?
OccurredAt
```

### 8.3 Local projection and customer-decision phase

Update DisruptionImpact with its own SourceVersion so GetOrder can show context locally. A stale RecoveryOptionSelected cannot replace a newer accepted option. Correlate InstructionId, ImpactVersion and ExpectedCommercialVersion. A cancelled flight blocks new readiness/issuance even if the separate impact-analysis message has not yet arrived; no automatic fare cancellation is implied.

Planned schedule change/disruption can require **customer decision** before commercial finalization. The local projection must be able to show, when supplied by the recovery owner, that customer action is required, the offered/recommended recovery references, the applicable decision deadline, the selected/declined option and final source-directed disposition. This is a projection of recovery truth, not a new generic workflow engine or a locally invented status model.

At a decision deadline the action may be accept/revalidate, cancel/release/refund, release capacity but keep document/value open for future use, or manual handling according to carrier/recovery policy. Ordering must not choose the default. The recovery instruction/policy supplies it; if the actual contract does not, that automated timeout path is blocked.

### 8.4 Involuntary servicing flow

```text
DisruptionImpactDetected / ScheduleChange
-> Ordering exposes affected commercial context
-> Recovery owner may mark CustomerDecisionRequired and supply deadline/options
-> customer accepts current proposal, requests alternative, requests refund/respend, or declines
-> Recovery/Pricing owner supplies the selected involuntary commercial/document/value treatment
-> prepare durable ApplyInvoluntaryOrderChange operation
-> reserve replacement and obtain required coverage/control guarantees
-> execute declared exchange/revalidation plan and reconcile unknown outcomes
-> finalize successor segment/service/item and PriceChangeSet once
-> emit OrderReaccommodated
-> resolve local impact when external owner confirms
```

### 8.5 Misconnection

For a broken connection:

- consumed first segment remains historically unchanged;
- unconsumed downstream Service is replaced;
- new segment/service lineage is created;
- fare construction may be retained or replaced depending pricing/recovery policy;
- document coupon exchange/revalidation is separate;
- delivery history remains immutable.

---

## 9. Ledger integration

### 9.1 Principle

Order owns commercial pricing facts; Ledger owns accounting postings.

Do not publish only `OrderId` and force Ledger to query mutable Order state for a financial event.

For immutable commercial changes, publish the relevant monetary snapshot.

### 9.2 Recommended outbound event

```text
OrderPricingChanged
-------------------
EventId
OrderId
CommercialVersion
FinancialSequence
PreviousFinancialSequence
PriceChangeSetId
ChangeId?
Reason
SaleCurrency
Lines[]
  PricingLineId
  OriginalPricingLineId?
  ComponentType
  Code?
  Effect
  Direction
  OriginalValue : [platform monetary representation]
  SaleValue : [platform monetary representation]
  LineRole, OriginalPricingLineId?, TransferGroupId?
  Basis
  Refundability
Allocations[]
  AllocationSet purpose/version/source/method
  target refs
  amount/currency
SalesContext snapshot/ref
OccurredAt
```

Ledger receives enough immutable component/effect/valuation/party data to apply its OWN posting policy. This is not a claim that a generic sum of all order lines is airline revenue. Commission and tax have distinct treatment; SplitTransfer is reclassification, not another taxable sale. Full FX and original-line links are mandatory. A reversal delivered before its source is durably staged for sequence repair, not dropped.

### 9.3 Accounting acknowledgment

Ledger acknowledgment/rejection may be projected for operations, but it must not rewrite the commercial price.

Accounting exceptions are reconciled separately.

---

## 10. Offer/Pricing/AirPrice integration

### 10.1 Initial sale

The accepted commercial source must provide or allow retrieval of the authoritative accepted result required to create the Order, including as applicable:

- Offer/OfferItem/source identities and versions;
- product/service definitions and beneficiary/coverage scope;
- traveler/journey/segment sale snapshots;
- commercial terms/rule references;
- accepted monetary component breakdown and customer total;
- optional fare-construction context;
- optional source allocations;
- tax/fee/surcharge/discount/commission facts supplied by the source;
- original/sale/converted monetary provenance supplied by the source where relevant;
- supplier/provider references.

Ordering validates that the accepted snapshot is internally usable and persists the authoritative facts. It does not recalculate fares, taxes, penalties, currency conversion or rounding rules that belong to Pricing/AirPrice/other owners.

### 10.2 Servicing quote / reshop

Refund/change/cancel servicing uses the same ownership principle:

```text
Ordering supplies current + historical commercial/fare/document/use context
-> Pricing/Offer/AirPrice or another approved owner evaluates the requested servicing operation
-> source returns an informative/accepted decision with scope, monetary treatment, penalties/waivers and document/value plan
-> user/system accepts a specific decision/version
-> Ordering executes and records the result
```

For partially used refunds/exchanges, Ordering supplies used-service/coupon/history evidence but does not calculate `fare used` or refundable value itself. For true RT vs OW+OW it supplies stored source fare construction so the pricing owner can apply the correct coupling.

The servicing decision must preserve enough source identity/version/expiry to reject stale acceptance. It may return even exchange, add-collect, refund, residual/reusable value or combinations, plus penalty/waiver and ancillary dispositions. See `12`.

### 10.3 Servicing record and customer artifacts

Once servicing finalizes, Ordering can expose/publish an immutable redisplay record built from the accepted Pricing decision plus confirmed document/payment outcomes. A refund/exchange notice is a presentation artifact derived from that record. Rendering/storage/delivery ownership must be discovered from the current platform; absence of a confirmed owner is a `BLOCKED_DECISION`, not permission to build a notification/document subsystem inside Ordering.

---


## 11. Message identity, financial ordering and consumer behavior

Normalized envelope:

```text
EventId, MessageType, SchemaVersion, SourceSystem, OwnerAirlineId
AggregateType, AggregateId, StreamKind, StreamSequence
OccurredAt, PublishedAt?, CorrelationId, CausationId?, OperationId?
CommercialVersion?, FinancialSequence?, SourceEpoch?, SourceVersion?
PayloadHash
```

Transport delivery is at-least-once. Persist EventId and exact payload in Outbox with the owning state commit; every republish uses the same ID/payload. Inbox `(SourceSystem, EventId, Consumer)` and the domain semantic key (MovementId, InstructionId or ObservationKey) are both checked. Same key/different economic payload is quarantined, not treated as a harmless retry.

For commercial events, CommercialVersion is the resulting Order revision. Multiple events from one mutation share it and have EventOrdinal. For financial events, one PriceChangeSet produces one complete OrderPricingChanged envelope and the next contiguous FinancialSequence; nonfinancial events do not create apparent financial gaps. Document/delivery streams have their own versioned identities.

The publisher serializes each `(OwnerAirlineId, AggregateId, StreamKind)` stream using a durable claim and sends only its next unacknowledged message. Broker acknowledgments do not imply consumer posting completion. Consumers also enforce ordering: received sequence <= applied -> idempotent no-op only if identity agrees; == applied+1 -> apply; > applied+1 -> persist gap, request/replay missing immutable messages and wait. A filtered subscriber uses its declared stream, not the all-events sequence. Never rely only on arrival time or RabbitMQ routing to make financial dependencies correct.

SplitTransfer includes TransferGroupId and BOTH source/child component legs in the immutable business payload. Ledger stages/applies the pair once by TransferGroupId; independently received duplicate per-order envelopes do not duplicate the transfer. Missing source lines or currency/party mismatch prevents posting and opens reconciliation, not a silent best-effort balance.

Publishing and local projections do not dispatch external side effects inside a retryable database transaction. Normalized messages are internal adapter contracts; preserve existing wire versions until an explicit coordinated contract migration is made.

---

## 12. Failure and reconciliation matrix

| Failure | Domain handling |
|---|---|
| Inventory request timeout | FulfillmentReservation=Unknown; reconcile before unsafe retry |
| Payment provider timeout | Payment owns Unknown; Ordering shows pending/unknown projection and does not mark Order failed |
| Ticket issue request timeout | ProviderInteraction unknown; reconcile document number before reissue |
| DCS duplicate event | Inbox/SourceEventId dedup; no duplicate observation/state change |
| DCS out-of-order event | Append observation, transition policy prevents state regression |
| Flight event duplicate | version/idempotency check; projection remains current |
| Cancel succeeded at supplier but Order update failed | reconciliation/event retry applies confirmed outcome idempotently |
| DB commit succeeded but message publish failed | transactional outbox publishes later |
| Ledger rejects accounting event | commercial Order unchanged; accounting exception/reconciliation flow |
| Split provider operation partial | keep operation/finalization state explicit; do not fabricate completed split |

---

## 13. Processing claims and reconciliation

The complete minimal protocol is in `07`. v1 serializes conflicting business operations with ONE durable Order-scoped claim, not unrelated `order-pay`, `order-cancel`, `order-split` locks that can all succeed together. Do not build a speculative compatibility matrix.

Persist claim, OperationId, ClaimGeneration, affected scope and external step intents. Technical lease expiry triggers takeover/reconciliation of the SAME operation; it does not make an unknown charge disappear. External observations are always recorded even while a claim is held. Reevaluate changed document/control/delivery evidence before irreversible finalization.

`ReconciliationWorkItem` is a lightweight operational row, not a new rich domain aggregate. It stores WorkItemId, Owner, OperationId/source fact ref, ReasonCode, Status, NextActionAt, Attempts, EvidenceRefs and Resolution. Supported causes include ambiguous correlation, unknown provider outcome, payment mismatch, financial sequence gap and conflicting terminal observation. Resolve/retry commands retain audit and stable semantic IDs.

A caller retry can discover the existing operation, its per-target outcomes and the exact pending action. No workflow relies on a human remembering a message in the chat or a transient exception log.

---

## 14. Security/authority boundary

Ordering snapshots who sold/changed the Order but authorization remains in Aegis/Identity/Gateway policy.

Domain checks may enforce structural authority facts such as:

- airline override;
- owning seller/office constraints;
- explicit delegated servicing if that feature is enabled later.

Do not duplicate role/permission management inside Order.


---

## 15. Partner contracts required before external execution

All integrations are implementable through the current Providers/Consumers pattern, but signatures alone do not establish a connected external capability. Release certification is per adapter and requires:

| Owner | Required behavior / evidence | Safe unavailable behavior |
|---|---|---|
| Pricing/Offer/AirPrice | immutable accepted initial/servicing decision, amount basis/provenance, affected services/fare context, tax/fee/penalty/waiver/refund/change treatment and any conversion evidence required by its contract | Block priced action with a named missing-contract reason; no fabricated fare/tax/penalty/FX/refund result |
| Inventory/FlightFlow | stable operation key, per-member outcomes, hold expiry, commit/release/divide behavior, query for unknown result | Retain pending intent; no duplicate seat consumption |
| Payment/StoredValue owner | stable economic intents/movements, confirmed monetary value using its actual contract, coverage reservation, refund/value return, transfer and authoritative outcome query | No real-money/value action; mock is dev/test only |
| Local/external issuer | document format/stock policy, control and coupon transitions, issue/void/exchange/revalidate, exact outcome lookup | Block unsupported document action; no random number/assumed success |
| DCS | correlation keys, per-aspect ordering/corrections, passenger/ancillary data, control-release and delivery acknowledgments | Preserve facts/quarantine unresolved mapping; withhold unsafe readiness |
| Disruption | versioned impact/instruction and recovery selection, supported replacement, accepted treatment | Surface unresolved impact and manual recovery; never silently remove integration scope |
| Ledger | source financial sequence, accepted component/provenance semantics, staged gaps, balanced split transfer and acknowledgment according to the agreed contract | Queue durable events; commercial change is not rewritten by posting failure |

These are deployment/configuration gates rather than unresolved domain-model choices. They must be tested against actual adapters; the design pack is not evidence that these sibling services are already production-ready.
