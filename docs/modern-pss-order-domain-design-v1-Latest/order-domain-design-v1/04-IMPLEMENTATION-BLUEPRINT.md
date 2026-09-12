# 04 - Implementation Blueprint

## 1. Implementation objective

Implement the final domain model inside the existing AeroTech Ordering solution without discarding proven framework/infrastructure patterns.

Preserve where possible:

- current solution/project layout;
- .NET / EF Core / MediatR / MassTransit conventions already used;
- platform IdGen/Snowflake IDs;
- distributed lock and idempotency infrastructure;
- Outbox/Inbox;
- ProviderInteraction and FulfillmentTask mechanisms;
- command/query separation;
- PascalCase API route convention already established in the platform.

Default execution path: build the new domain in the existing framework/project layout, with no legacy business-data migration or API compatibility work, per the questionnaire. Reuse infrastructure but fix unsafe patterns before reuse. A separate CONDITIONAL live-migration path is specified in section 13; it is not an automatic scope expansion.

---

## 2. Logical implementation areas within the existing solution

The following tree is a **navigation aid only**. It does not require these folder names, namespaces or an additional SharedKernel. The agent must first inspect the existing solution and follow its current organization. Create/move folders only when that is the smallest change consistent with the existing repository:

```text
AeroTech.Ordering.Domain
  Ordering/
    OrderAggregate/
    Pricing/
    Policies/
  Fulfillment/
    FulfillmentReservationAggregate/
    ElectronicTicketAggregate/
    ElectronicMiscDocumentAggregate/
    DocumentStockAggregate/
  GroupBooking/
  Delivery/
    Policies/
    Models/
  Disruption/
    Models/
  SharedKernel/

AeroTech.Ordering.Application
  Ordering/
  Fulfillment/
  Delivery/
  Disruption/
  GroupBooking/
  Integration/

AeroTech.Ordering.Persistence
  Ordering/
  Fulfillment/
  GroupBooking/
  Operations/

AeroTech.Ordering.Consumers
  Inventory/
  Payment/
  Dcs/
  FlightOperations/
  Disruption/
  Ledger/

AeroTech.Ordering.Query
  OrderDetails/
  Search/
  CorrelationIndexes/
```

Do not create new deployable microservices for these modules in v1.

---

## 3. Domain-semantic cleanup from the current repository

The current implementation mixes fulfilable services with monetary/technical concepts. The list below describes **business classification only**; it does not require an enum rename, new inheritance hierarchy or a particular type representation. Preserve current framework/API representation where it remains semantically correct and map at boundaries where necessary.

### Concepts that are fulfilable Order Services

```text
AirTransportation
SeatAssignment
BaggageAllowance / Baggage
Meal
LoungeAccess
Cip
SimCard
HotelStay
TransferRide
InsurancePolicy
Other/ThirdParty
```

Do not rename or redesign representation merely to match these labels. The implementation must preserve the semantic distinction using existing repository conventions.

### Concepts that are not Order Services

These are not independently deliverable customer Services by default:

```text
Penalty
ServiceFee
Credit
TaxAdjustment
ManualAdjustment
Notification
```

Map them to:

```text
Penalty          -> PricingLine.ComponentType.Penalty
ServiceFee       -> PricingLine.ComponentType.Fee
Credit           -> PricingLine CustomerBalance/Credit or external StoredValue, according to economic purpose
TaxAdjustment    -> PricingLine.ComponentType.Tax/Adjustment
ManualAdjustment -> PricingLine.ComponentType.Adjustment
Notification     -> Notification integration, not OrderService
```

`Voucher` should be treated as a stored-value/payment product unless there is a real fulfilable voucher product requirement.

Likewise, Product Catalog `ProductType` should represent products, while financial adjustments use Pricing component types.

---

## 4. Command-side logical persistence model

The command-side model needs the following **logical persistence areas**. Existing database/schema conventions discovered in P0 take precedence over the illustrative grouping below:

```text
Order
Fulfillment
GroupBooking
Operations
ReadModel
```

### 4.1 Order schema

#### Orders

```text
Id bigint PK
UniqueIdentifier uniqueidentifier/string optional compatibility key
OrderReference varchar
RootOrderId bigint
ParentOrderId bigint null
SplitFromChangeId bigint null
OwnerAirlineId bigint NOT NULL
FinancialCustomerId bigint NOT NULL
SaleCurrencyRef / monetary currency fields using the existing platform mapping NOT NULL
CommercialSummary varchar(24) NOT NULL
CustomerTotal using the existing platform monetary mapping NOT NULL
CommercialVersion bigint NOT NULL
FinancialSequence bigint NOT NULL DEFAULT 0
ClosedAt datetimeoffset null
CloseReason varchar null
RowVersion rowversion
SalesContextJson + normalized SellerCustomerId/OfficeId/Channel/POS fields
BuyerSnapshotJson
CreatedAt datetimeoffset
```

Indexes:

- unique `(OwnerAirlineId, OrderReference)`;
- `(OwnerAirlineId, CommercialSummary, CreatedAt)`;
- `(RootOrderId)`;
- `(ParentOrderId)`;
- `(CreatedAt)`.

#### OrderTravelers

Stable Id, current OrderId and PassengerTypeCode, InfantParentTravelerId, protected PersonalDataPayload refs and optional Core CustomerId. Index `(OrderId, PassengerTypeCode)` and validate same-current-Order infant/beneficiary references. Historical references survive split; do not cascade-delete moved passengers or their financial history.

#### OrderContacts

Index `(OrderId, Role)`.

#### Journeys

Index `(OrderId, Sequence)`.

#### JourneySegments

Store SegmentKind (ScheduledAir/OpenAir/Surface), sold schedule snapshot, source flight identity/version, explicit timezone data and optional `SoldOperationalLegsJson`/child rows when the accepted source supplies planned physical legs. A JourneySegment is the passenger's sold board-to-off segment; several physical legs therefore do not imply several Services. Current operational times/leg composition are a separate projection; new child segments on divide preserve OriginalSegmentId in change mappings.

Persist adjacent topology explicitly in `JourneyConnections(FromSegmentId, ToSegmentId, ConnectionKind, ProtectionType, SourceSystem, SourceReference)` with uniqueness per ordered segment pair. Do not derive Connection/Stopover or Protected/Unprotected from elapsed time.

Indexes:

- `(OrderId, JourneyId, Sequence)`;
- `(ExternalFlightId)`;
- `(MarketingCarrier, MarketingFlightNumber, DepartureLocalDate)` for legacy correlation if required.

#### OrderItems

```text
Id bigint PK
OrderId FK
SourceOfferId
SourceOfferItemId
ProductType
ProductCode
ProductSnapshotJson
CommercialTermsJson
CommercialStatus
CreatedByChangeId
AcceptedPrice using the existing platform/source monetary mapping null
CancelledByChangeId null
ReplacedByChangeId null
CreatedAt
CancelledAt null
ReplacedAt null
```

Indexes:

- `(OrderId, CommercialStatus)`;
- `(SourceOfferId, SourceOfferItemId)` where useful for idempotency.

#### OrderServices

```text
Id bigint PK
OrderId FK
OrderItemId FK
ServiceType
ServiceCode
Name
TravelerId null
ServiceVersion bigint NOT NULL
SupplierPartyRef
DeliveryProviderRef null
FulfillmentProfileJson
PriceTreatment
CommercialStatus
CreatedByChangeId
PredecessorServiceId null
ReplacedByServiceId null
CreatedAt
CancelledAt null
ReplacedAt null
```

Indexes:

- `(OrderId, CommercialStatus)`;
- `(OrderItemId)`;
- `(TravelerId)`;
- `(PredecessorServiceId)`;
- `(ServiceType)`.

#### ServiceCoverage

```text
Id
OrderServiceId
CoverageKind
JourneyId null
SegmentId null
LocationCode null
StartAt null
EndAt null
```

Index `(SegmentId, OrderServiceId)` for flight/DCS/disruption correlation. Use common ServiceCoverage as current coverage authority; typed-detail SegmentId, if mapped, must be a validated mirror, not an independently mutable conflicting reference.

Additional current/historical association tables:

```text
OrderServiceBeneficiaries(ServiceId, TravelerId, Role) PK(ServiceId, TravelerId)
ServiceDependencies(ServiceId, RelatedServiceId, Kind, PolicyRef)
OrderItemServiceLinks(Id, OrderIdAtAssociation, ItemId, ServiceId,
                     ScopeSnapshotJson, LinkedByChangeId)
```

Beneficiaries and dependencies reference current valid scope when accepted; immutable membership records retain original scope after split. OrderService has exactly one current owner; historical links can span predecessor Orders/items. No destructive cascade from old items to a moved Service.

#### Typed detail tables

```text
AirTransportServiceDetails
SeatServiceDetails
BaggageServiceDetails
MealServiceDetails
LoungeServiceDetails
HotelServiceDetails
GroundTransportServiceDetails
GenericServiceDetails
```

Each uses `OrderServiceId` as PK/FK.

### 4.2 Fare construction tables

```text
AirFareConstructions
FareConstructionItemLinks
AirPricingGroups
AirPricingGroupTravelers
PricingUnits
FareComponents
FareComponentSegments
FareComponentServices
```

Key indexes:

- `AirFareConstructions(OrderIdAtCreation, CreatedByChangeId)`;
- `PricingUnits(PricingGroupId)`;
- `FareComponents(PricingUnitId)`;
- `FareComponentSegments(SegmentId)`.

Do not force these rows for dynamic/charter items lacking fare construction. Construction snapshots are Order-owned and can bind several items. Current FareConstructionItemLinks are identified by active acceptance/ChangeId; historical construction references are immutable. FareComponent has explicit service/traveler coverage, so a P1 repricing cannot silently affect P2.

### 4.3 Commercial change tables

```text
OrderChanges
OrderItemPredecessors
OrderServiceLineage
OrderLineage
TimeLimits
ExternalReferences
```

Indexes:

- `OrderChanges(OrderId, OccurredAt)`;
- `ExternalReferences(ProviderType, ReferenceType, Value)`;
- `TimeLimits(OrderId, Status, DueAt)`.

### 4.4 Pricing tables

```text
PriceChangeSets
PricingLines
PricingAllocationSets
PricingAllocations
```

#### PricingLines indexes

```text
(OrderId, PriceChangeSetId)
(OrderItemId)
(OriginalPricingLineId)
(ComponentType, Code)
(BasisType, BasisReferenceId)
```

#### PricingAllocations indexes

```text
(AllocationSetId)
(PricingLineId, Purpose, Version)      # on the parent set
(OrderServiceId)
(TravelerId)
(SegmentIdAtAllocation)
(OrderItemIdAtAllocation)
```

All pricing rows are append-only after commit. Required physical definitions:

```text
PriceChangeSets
  Id, OrderId, ChangeId, Reason, Source, SourcePricingRef, SourceOfferId null
  FinancialSequence, CreatedAt, CommittedAt
  UNIQUE(OrderId, FinancialSequence), UNIQUE(OrderId, ChangeId, Reason)

PricingLines
  Id, OrderId, PriceChangeSetId, OrderItemId null
  ComponentType, Code null, Effect, Direction, LineRole
  OriginalValue fields using the existing platform/source monetary mapping
  SaleValue fields using the existing platform/source monetary mapping
  AppliedConversionProvenance/source reference null, RefundTreatmentJson, ApplicationLevel null
  Quantity/unit/unit-rate fields using the authoritative source/platform representation when applicable
  BasisType, BasisReferenceId null, CalculationSnapshotJson null, TaxDetailsJson null
  SourceLineRef null, OccurrenceKey, OriginalPricingLineId null
  OriginalAllocationId null
  TransferGroupId null, RelatedOperationId null, SettlementPartyRef null,
  SettlementCategory null, CreatedAt
  CHECK(OriginalAmount >= 0 AND SaleAmount >= 0)
  UNIQUE(PriceChangeSetId, SourceLineRef, OccurrenceKey) when SourceLineRef is present

PricingAllocationSets
  Id, OrderIdAtCreation, PricingLineId, Purpose, Version
  SupersedesAllocationSetId null, Source, Method, Completeness,
  PolicyVersion null, CreatedAt
  UNIQUE(PricingLineId, Purpose, Version)

PricingAllocations
  Id, AllocationSetId, OrderItemIdAtAllocation null, OrderServiceId null
  TravelerId null, JourneyIdAtAllocation null, SegmentIdAtAllocation null
  CoveragePortionRef null, OriginalValue null using platform/source mapping,
  SaleValue using platform/source mapping
  OriginalAllocationId null
```

These are **logical persistence requirements**, not authorization to invent monetary/FX types or SQL precision. Map monetary/currency/conversion fields using the already-established AeroTech and owning-service representation discovered in P0. Cumulative reversal, allocation reconciliation and same-scope rules are transactionally validated where the authoritative source contract makes them decidable. Committed commercial monetary history is append-only in normal service execution; corrections append new facts.

### 4.4A Finalized servicing read/audit records

P3 requires a redisplayable finalized servicing view for refund/void/revalidation/exchange and corrective operations. This is **not a new aggregate mandate**. Persist or project it using the existing query/projection pattern so it can be rebuilt from immutable Ordering facts plus retained authoritative external references. Required semantics are in `01` section 16A, `08` section 6 and `12` section 15.

At minimum it must correlate operation/change, affected scope, accepted Pricing decision/PriceChangeSet, original/successor document references, penalty/waiver evidence, confirmed/unresolved payment/value references and actor/provider timestamps. It never recalculates current fare/tax/refund values.

---

### 4.5 Fulfillment schema

```text
FulfillmentReservations
FulfillmentReservationServices
ReservationCouplingGroups
ReservationCouplingGroupServices
ElectronicTickets
TicketCoupons
ElectronicMiscDocuments
EmdCoupons
DocumentStocks
DocumentStockAllocations
```

Required additions and constraints:

- Per-reservation member ObservedStatus/ProviderVersion/ConfirmedQuantity/ValidUntil; root counts plus HasUnknown/Mixed summary.
- ReservationCouplingGroups store `Independent|MarriedSegments|ProviderAtomicSet`, provider group reference and exact member Services; membership table is unique `(CouplingGroupId, OrderServiceId)`. Issue/readiness checks the whole coupled set, not only one confirmed member.
- ETKT/EMD retain an immutable issue-time monetary snapshot using the platform/source representation plus document concurrency/version evidence. DocumentPriceLinks retain issue-time line/allocation values. Monetary-purpose EMD-S can reference a fee/deposit/residual/value context without fabricating a deliverable service; exact issuer representation follows the certified provider contract.
- Coupon uniqueness `(DocumentId,CouponNumber)`; active service-document association conflicts checked within the protected operation, with explicit exchange/conjunction exceptions.
- Document number uniqueness includes the issuer's configured NumberingNamespace. Issuer/type alone is not assumed universally sufficient.
- DocumentStockAllocations uniquely `(StockId,Number)` and `(OperationId,DocumentRoleKey)`; required Reserved/Issued/Retired status.
- DocumentRoleKey identifies document type, traveler/monetary purpose, issuance group and ordinal inside the immutable operation plan. It is not merely the word Ticket; multiple passengers can require multiple documents in one operation.
- Stock range overlap checked under serialized namespace reservation; a unique RangeFrom index does not prove non-overlap.
- External reservation identity indexed by Provider/ExternalReference/Generation. No FK reaches another service's database.

### 4.6 GroupBooking schema

```text
GroupBookings
GroupSeatBlocks
GroupNameSlots
GroupNameSlotBlocks
GroupMaterializationRequests
GroupTimeLimits
GroupSpawnedOrders
GroupDepositReferences
GroupMaterializationRows
```

GroupMaterializationRows have a unique `(GroupBookingId,BatchId,ClientRowId)` business identity, RowRevision, protected payload/ref, target slots, allocated OrderIds and status. Revision-specific command receipts support correction of rejected/no-effect rows only; completed rows do not rematerialize. See `08` section11.

### 4.7 Operations schema

```text
ServicingOperations
OperationClaims
CommandReceipts
ReconciliationWorkItems
DeliveryObservations
ServiceDeliveryStates
SegmentOperationalStates
DisruptionImpacts
DisruptionImpactServices
FlightOperationalEvents
FlightImpactFanoutJobs
FlightImpactFanoutItems
PaymentApplicationFacts
PaymentApplicationProjections
CoverageGuarantees
CoverageClaims
FulfillmentTasks
ProviderInteractions
InboxMessages
OutboxMessages
```

Critical indexes:

```text
DeliveryObservations UNIQUE(SourceSystem, SourceEventId, ObservationKey)
DeliveryObservations(OrderServiceId, OccurredAt)
ServiceDeliveryStates PK(OrderServiceId)
SegmentOperationalStates PK(SegmentId)
DisruptionImpactServices(OrderServiceId)
FlightOperationalEvents UNIQUE(SourceSystem, SourceEventId)
FlightImpactFanoutJobs UNIQUE(SourceSystem, SourceEventId, SourceVersion)
FlightImpactFanoutItems UNIQUE(FanoutJobId, OrderId)
PaymentApplicationFacts UNIQUE(PaymentOwner, MovementId)
PaymentApplicationProjections(OrderId, ApplicationId)
CommandReceipts UNIQUE(OwnerAirlineId, CallerScope, OperationName, IdempotencyKey)
OperationClaims UNIQUE(OrderId) WHERE IsBlocking=1
OutboxMessages UNIQUE(StreamKey, StreamSequence)
InboxMessages UNIQUE(SourceSystem, Consumer, MessageId)
```

---

## 5. Query model, one-writer rule and rebuild

### 5.1 Minimal physical model

Use the existing CQRS framework. Baseline storage is:

```text
ReadModel.OrderDetails(OrderId PK, ProjectionRevision, SnapshotJson, UpdatedAt)
ReadModel.OrderSearch(OrderId PK, OwnerAirlineId, OrderReference,
                     FinancialCustomerId, SellerId, OfficeId, CreatedAt,
                     CommercialSummary, ReservationSummary, PaymentSummary,
                     DocumentSummary, DeliverySummary, HasDisruption,
                     CustomerTotalAmount, SaleCurrency, ProjectionRevision)
ReadModel.OrderTravelerSearch(OrderId, TravelerId, protected/minimized search fields)
```

Flight/service/document/external-reference lookup uses indexed local correlation tables already needed by the command/integration side. Create separate search projections only when a measured query or isolation requirement needs them; seven new projection tables are not a domain invariant. GetOrder uses one local details read, with independently authorized PII expansion where needed, not a fan-out to external services.

SnapshotJson contains current commercial/services, relevant current pricing, independent facets, current/sold schedules, references and summary totals. Full change history is paginated separately by ChangeId/FinancialSequence. Do not attach every historical price/observation to ordinary GetOrder.

### 5.2 Exactly one local projection writer

**v1 decision: synchronous LOCAL projection in the SAME SQL transaction as the source update**, because command and query models initially share one database and the user wants immediate local reads. This is not a synchronous transaction with external services.

OrderingUnitOfWork must share an explicit DbConnection/DbTransaction across command and query DbContexts, or one context may persist both. Two independent SaveChanges calls are NOT atomic. Use one `OrderViewProjector.ProjectCurrentOrder(orderId)` implementation; remove event consumers that also write these same rows. External events go through their authoritative local state handler, which invokes this same projector in its transaction.

Every source update affecting the view obtains the same per-Order transactional projection fence before loading/projecting; split obtains source/child fences in ascending ID order. Projector rereads current local canonical state inside the transaction and increments ProjectionRevision. A timestamp or CommercialVersion alone is insufficient: a DCS change can affect the view without changing CommercialVersion. Keep the lock short, never hold a SQL transaction across provider I/O.

**Bulk FlightOps exception is only to the cross-Order scheduling, not to per-Order atomicity.** `FlightCancelled`/diversion/etc. persists one source fact and durable fan-out job, then applies the SAME projector under one short transaction/fence per affected Order. Never acquire N Order fences or rebuild N full views inside one transaction. The P5 release gate is defined in `03` section 7.2 and scenarios S-150/S-218/S-219.

### 5.3 Read consistency and indexed status

Persisted CommercialSummary and normalized status facets have indexes and deterministic recomputation. Domain derivation does not imply in-memory aggregate scans for search. Commit success returns the new Order/Operation identifiers and commercial/projection versions. A subsequent local GetOrder can observe that committed version.

If read storage is later physically separated, a NEW explicit decision changes the writer to one outbox-driven asynchronous projector, with projection lag/read-your-write handling. Do not run both approaches concurrently and assume they will converge.

### 5.4 Rebuild is an operation, not just a test

Expose restricted `RebuildOrderReadModel(OrderId? | Range, RequestId, DryRun)` and `GetProjectionRebuildStatus(JobId)` application commands. Rebuild job stores cursor/counts/errors, is resumable, and rebuilds each Order under the SAME projector fence from current canonical tables plus required retained money/application facts. Upsert the three view tables in one local transaction; no external payment/issuance side effects or new commercial events are replayed.

A batch rebuild works per Order, not as a mandatory database-wide outage. Live updates and rebuild serialize through one projector. Missing source evidence opens reconciliation instead of fabricating current values. Privacy erasure markers are applied before projection and remain authoritative during rebuild. Compare semantic totals/statuses/key counts in dry run; record mismatch results.

### 5.5 Practical load contract

GetOrder does not call Payment/DCS/Inventory. SearchOrdersByPassenger uses the authorized traveler index plus OrderSearch. FindOrdersByFlight uses current/historical FlightKey correlations. GetOrderForServicing loads only relevant current commercial scope and decision evidence; it cannot treat partial collections as the entire Order. Lists are paginated; large group name imports do not create an unbounded full-order JSON response.

---

## 6. Application command model

Commands are business intents, not CRUD setters. Each user/business command invokes a policy in `07`. The normalized required payload and result types are specified in `08-CONTRACTS-AND-DATA-DICTIONARY.md`; the lists here are not substitute implementation signatures.

### Core commercial commands

```text
CreateOrderFromOffer
AddOrderItemsFromOffer
AcceptOrderChangeOffer
CancelOrderServices
CancelOrder
ApplyInvoluntaryOrderChange
SplitOrder
CloseOrder
CorrectTravelerDetails
AddOrderRemark
RequestOrderPayment
AcceptRefundQuote
ExtendTimeLimit
ExpireTimeLimit
LinkRelatedOrders
```

### Pricing/internal commands

```text
CommitAcceptedPrice
CommitServicingPriceChange
ApplySplitPriceTransfer
```

These normally consume trusted Pricing/Offer output rather than calculate fares.

### Fulfillment commands

```text
ReserveOrderServices
ReleaseOrderServices
IssueOrderDocuments
VoidTicket
VoidEmd
ExchangeTicket
RefundDocument
ReconcileUnknownFulfillment
RevalidateDocument
GetOperationStatus
```

### Operational integration commands

```text
RecordDeliveryObservation
ApplyFlightOperationalUpdate
ApplyDisruptionImpact
ResolveDisruptionImpact
ApplyPaymentApplicationFact
```

### Group commands

```text
CreateGroupBooking
ConfirmGroupBlocks
AllocateGroupName
DeallocateGroupName
SetGroupTimeLimit
RecordGroupDepositReference
MaterializeGroupOrder
CancelGroupBooking
ImportGroupNames
ReleaseGroupSeats
```

---

## 7. Domain events

Domain events inside the service may include the following. Event count never drives CommercialVersion; one business mutation may emit several facts.

```text
OrderCreated
OrderItemAdded
OrderServiceActivated
OrderServiceCancelled
OrderServiceReplaced
OrderChanged
OrderSplit
OrderClosed
TravelerCorrected

PriceChangeCommitted
PricingAllocationSetCreated

FulfillmentReservationChanged
FulfillmentOutcomeUnknown

TicketIssued
TicketCouponStatusChanged
TicketVoided
TicketExchanged
EmdIssued
EmdVoided
DocumentStockAllocated

GroupBookingCreated
GroupBlocksConfirmed
GroupNameAllocated
GroupOrderMaterialized

DeliveryObservationRecorded
ServiceDeliveryStateChanged
DisruptionImpactRecorded
DisruptionImpactResolved
```

These are internal domain events. Not every domain event needs to become a public integration event.

---

## 8. Integration events

### Outbound core events

```text
OrderCreatedV1
OrderCommercialChangeCommittedV1
OrderServicesChangedV1
OrderSplitV1
OrderClosedV1
OrderPricingChangedV1
OrderDocumentIssuedV1
OrderDocumentChangedV1
OrderDeliveryUpdatedV1
OrderReaccommodatedV1
```

### Delivery/DCS-facing events

```text
OrderServiceReadyForDeliveryV1
OrderServiceChangedForDeliveryV1
OrderServiceCancelledForDeliveryV1
TravelerChangedForDeliveryV1
DocumentIssuedForDeliveryV1
DocumentVoidedForDeliveryV1
OrderSplitForDeliveryV1
```

### Inbound integrations

Inventory:

```text
ReservationConfirmed
ReservationRejected
ReservationExpired
ReservationReleased
ReservationOutcomeUnknown
```

Payment:

```text
PaymentAppliedToOrder
PaymentApplicationReversed
PaymentRefundCompleted
PaymentChargebackRecorded
```

DCS:

```text
PassengerCheckInChanged
PassengerBoardingChanged
PassengerTravelOutcomeRecorded
SeatAssignmentChanged
AncillaryDeliveryChanged
BaggageHandlingObserved
```

Flight Operations:

```text
FlightScheduleChanged
FlightDelayed
FlightCancelled
FlightReinstated
AircraftChanged
AirportChanged
```

Disruption:

```text
DisruptionImpactDetected
DisruptionImpactUpdated
DisruptionImpactResolved
ReaccommodationInstructionIssued
```

Ledger:

```text
OrderPricingAcceptedByLedger
OrderPricingRejectedByLedger       # operational reconciliation only
```

---

## 9. Integration event payload rule

### Use full immutable payload for financial/commercial facts

For `OrderPricingChanged`, include all data Ledger needs to post deterministically. Do not send only OrderId and require a later mutable query.

### Use identity + relevant snapshot for operational facts

For DCS/Delivery, send OrderService identity plus the delivery-relevant snapshot; do not dump all Order/pricing data.

### Version contracts independently

Integration contracts live in Contracts project and are versioned. Domain must not directly depend on message DTOs; use mapping in Application/Consumers.

This also removes the current Domain -> Contracts dependency inversion issue.

---

## 9A. Version semantics for the replacement Ordering contract

The implementation described by this pack is allowed to replace the old producer contract semantics; current external consumers are not a compatibility constraint. The new contract MUST use distinct, purpose-specific sequences:

```text
CommercialVersion  = once per accepted committed commercial Order mutation
EventOrdinal       = ordinal among events emitted by that same mutation/version
FinancialSequence  = once per committed PriceChangeSet for an Order
ServiceVersion     = once per accepted current OrderService definition/ownership mutation
ObligationVersion  = payment/payable staleness version; changes only when payment-critical obligation changes
RowVersion         = persistence concurrency token only
```

Rules:

1. `Order.Create` creates `CommercialVersion=1`.
2. A successful commercial mutation increments it exactly once, even when several integration/domain events are emitted.
3. Duplicate/idempotent replay and rejected/no-op commands do not increment it.
4. Financial-only ordering uses `FinancialSequence`; consumers must not infer missing monetary facts from gaps in broad commercial events.
5. Payment staleness uses `ObligationVersion`, not every spelling/contact/operational change in the Order.
6. Do not reintroduce a generic event-count version simply because the old contracts had one.

Downstream consumers are updated after this producer contract is implemented and frozen; no P0 consumer inventory or dual-publish compatibility layer is required by this design.

---

## 10. HTTP/API surface principles

The domain design does not require exposing entities as CRUD and does not prescribe controller/class/DTO representation. API implementation follows the existing AeroTech RestApi/versioning/channel/framework conventions discovered in P0. Route examples below use the platform's current PascalCase/action convention but are **not a reason to redesign the framework**.

The externally useful business operations must include, as the corresponding slices land:

- create/get/search Order;
- add accepted items/ancillaries;
- request payment/coverage and issue;
- pre-ticket cancel/cancel selected services;
- side-effect-free change/reshop quote and explicit acceptance;
- side-effect-free refund quote and explicit acceptance;
- document void when explicitly requested/eligible;
- split and traveler correction;
- operation-status retrieval for Pending/Unknown execution;
- finalized servicing-record retrieval for support/audit/notice generation.

Revalidation versus exchange/reissue is normally an **execution outcome of an accepted change plan**, not a UI caller setting a coupon status directly. Likewise, payment refund is an external value execution step, not an unrestricted `MarkRefunded` endpoint.

Existing examples that can remain if they fit the repository surface:

```text
POST /backoffice/v1/Order/Create
POST /backoffice/v1/Order/AddItems
POST /backoffice/v1/Order/CancelServices
POST /backoffice/v1/Order/AcceptChange
POST /backoffice/v1/Order/Split
POST /backoffice/v1/Order/Issue
POST /backoffice/v1/Order/Refund
POST /backoffice/v1/Order/RequestPayment
GET  /backoffice/v1/Order/Operations/{operationId}
GET  /backoffice/v1/Order/{id}
GET  /backoffice/v1/Order/Search
```

Before P3 route/DTO coding, freeze the exact existing-platform request/response mapping for quote/accept, void and servicing-record retrieval in the P3 implementation spec. Do not invent cross-service Pricing/Payment/document fields merely to complete an OpenAPI schema.

OTA/NDC or other surfaces may expose different wire contracts while invoking the same application use cases. Do not shape the aggregate around one API representation. Completed/idempotent/pending/stale/denied outcomes must map to the current platform's established HTTP/result conventions; if those conventions conflict across the repository, P0 records the evidence and resolves it rather than this document creating a new envelope.

### 10.1 Client command idempotency

CommandReceipt is keyed by `(OwnerAirlineId, authenticated CallerScope, OperationName, IdempotencyKey)`, scoped to the caller BEFORE an OrderId exists. Persist RequestHash and allocated OrderId/OperationId before external effects. A request with the same key and same normalized payload returns/resumes the ORIGINAL operation/result. Same key with different economic payload returns409. Network retry, reconnect and process restart do not create a new receipt or provider key.

The hash includes route business intent, targets, amount/currency, accepted quote and expected version; exclude transport-only timestamps/tracing. Authenticate every replay; a key is not a bearer token. Keep receipts for the operation's business retention period; do not expire uncertain money operations under a generic short HTTP cache TTL. A planned new action gets a new key, while a duplicate passenger-on-flight rule is a separate business check.

---

## 11. Versioning, claims and source ordering

Order uses RowVersion for optimistic concurrency and CommercialVersion for one committed business mutation. Create=1; accepted mutation increments once; no-op/rejected/replayed and projection-only events do not. PriceChangeSet also advances FinancialSequence once. Multiple emitted events share CommercialVersion and use EventOrdinal or separate stream sequence. See `01` section21 and `03` section11.

Use ONE durable unresolved servicing claim per Order, not different locks for pay/cancel/issue. Application OperationId and claim fencing survive Redis lease loss. DCS/source facts are not blocked from being recorded; irreversible finalization rechecks fresh evidence. Detailed policies/transitions are in `07`.

Financial delivery must tolerate duplicate and out-of-order messages. Producer stream serialization PLUS consumer gap staging is required; broker arrival order alone is insufficient. Persist SourceVersion per provider entity/aspect, not a timestamp-based global LastProjectedAt.

## 12. Local transaction implementation

For a local commercial commit:

1. Start explicit SQL transaction and obtain ordered projection/operation fences.
2. Load current Order state and evidence; check RowVersion/ExpectedCommercialVersion and eligibility.
3. Apply accepted domain mutation, append immutable money/change rows and advance versions once.
4. Dispatch domain events only to local handlers that append Outbox rows; NO remote calls. Persist command rows/outbox.
5. Rebuild affected current local views with the single projector using the SAME DbConnection/DbTransaction; persist query rows.
6. Commit, then return successful result. Outbox dispatch happens after commit outside this transaction.

For an inbound fact: local canonical state + semantic dedup + Inbox completion + Outbox + relevant local views commit together. For batched input, either commit the batch together or persist a parent receipt plus per-fact durable work; do not mark the whole envelope completed after only its first passenger.

The current framework clears collected domain events before dispatch/save. On failure, DISCARD the failed DbContext/unit of work and resume/reexecute from durable command/operation data; do not reuse a mutated object with cleared events and assume the outbox will regenerate. DB execution-strategy retries wrap only idempotent local transactions, never payment/provider calls.

For external execution: transaction A persists operation/step intent and stock/coverage reservation; then call provider with stable key; transaction B records confirmed/unknown result, applies eligible local changes and outbox. No external ACID promise. A crash between provider success and transaction B leads to reconciliation of the SAME step, not new issuance/payment.

Append-only pricing permissions, unique semantic keys and RowVersion supplement domain validation. For local ticket issue, stock allocation, document/coupons, operation completion and outgoing facts share the local transaction. For external issue, persist reserved stock before dispatch and retain uncertain numbers; see `03`.

Privacy purges are authorized exceptions to PII snapshot retention, not UPDATE/DELETE access to financial records. Rebuild never resurrects erased fields.

---

## 13. Conditional migration and immediate safety gate

### 13.1 Default scope: no legacy data migration

The questionnaire explicitly says no existing data migration and no API backward compatibility requirement. Therefore DO NOT build a compatibility migration framework merely because an old repository exists. Reuse platform infrastructure and replace/refine domain modules using section14. Never assume an existing Payment service is missing, or that a MockPaymentProvider proves production money has been charged.

### 13.2 M-0: mandatory before connecting real money or reusing legacy execution

If any existing Ordering instance handles live operations while development proceeds, this gate precedes domain refactoring. For a fresh build the same invariants are P0/P1 acceptance gates, not wasted migration code.

| Gate | Required change | Exit evidence |
|---|---|---|
| M-0.1 | Remove independent command/query commits and competing view writers; use section12 | Force second-context failure and crash injection; neither source nor view is partially committed |
| M-0.2 | Persist pending economic intent/receipt BEFORE provider call; same business key for all retries | Provider succeeds then process crashes: replay uses same intent/key, one economic effect |
| M-0.3 | Validate confirmed amount/currency/authority; separate partial, unknown, capture, release/refund and guarded transitions | Partial capture never produces full coverage; late/duplicate results cannot re-capture refunded money |
| M-0.4 | Immutable reversal lineage + deterministic sign/rounding; preserve true cancellation reason | Cancellation penalty is not a reversal; mixed-currency addition rejected; totals reconcile |
| M-0.5 | Controlled stock/number reservation before production issue | Concurrent issue allocates unique numbers; retry keeps same number; stock ranges do not overlap |

These are static risk findings, not claimed observed production incidents. The review's FlightFlow HTTP error classifier is not, by itself, proof of the Payment adapter's error mapping. The direct payment-before-commit window remains unsafe if a real-money adapter is connected.

### 13.3 Conditional payment coexistence/cutover

Select exactly one ExecutionOwner for each durable payment/operation: LegacyOrderingPayment or ExternalPaymentOwner. Route retries by the stored owner, never by whichever adapter is currently default. New economic intents switch only after the external owner is certified.

1. Inventory in-flight intents, provider keys/refs, confirmed applications, authorizations, refunds and unknown results; reconcile unknowns first.
2. While coexistence is required, legacy execution is hardened by M-0. New requests go only to the configured new owner. Do not dual-execute or mirror capture commands.
3. Import confirmed opening applications/references with unique migration source IDs and a cutoff; importing an opening balance is NOT a new capture/sale/revenue event. Check original currency and net movement totals per Order and per provider intent.
4. Retain lookup/routing for unresolved legacy operations; drain or transfer with explicit provider-supported authority and verified audit. Compare source/application/PSP reports.
5. Switch eligibility reads only when coverage/reconciliation agree. Disable legacy new-capture paths; remove the legacy aggregate from active writes only after no unresolved ownership remains. Keep mandated audit history.
6. Rollback routes new operations back only under a controlled freeze; never replay the same migrated charge as a new legacy Payment.

If no real legacy financial state exists, skip all import/coexistence work and certify the external adapter directly. This branch is conditional, not a new product version.

### 13.4 Conditional data evolution

After safety: migrate/refine money/stock and provenance first, then commercial state ownership, then servicing/lineage, then operational integration. Old Credit/Debit conventions MUST be translated, not copied into the new sign contract. Every conversion preserves source IDs/amounts and reconciles balances. Existing document numbers are preserved; they are not regenerated. Destructive migration is never run merely because these documents were downloaded.

---

## 14. Implementable vertical slices within v1

**Mandatory P0 precondition:** before material domain coding, produce `docs/order-domain-design-v1/implementation/P0-DISCOVERY-AND-DECISIONS.md` as specified in `13`. P0 is allowed to implement only unambiguous foundation work. A missing cross-service/shared representation is not filled by an agent-created abstraction.

These are implementation batches, not promises of duration. DCS, disruption and group remain v1 scope. No slice is considered production-ready until its failure paths and adapter contracts pass.

| Slice | Deliverable | Blocking acceptance |
|---|---|---|
| P0 | repository/framework discovery; ownership/contract map; existing monetary/currency/ID/time/reference-data representations; command receipts, operation claims, local atomic projector/outbox and version-purpose semantics where unambiguous | `P0-DISCOVERY-AND-DECISIONS.md` complete; no invented shared primitive; all blocking business/cross-service ambiguities raised as `BLOCKED_DECISION`; unblocked foundation tests pass; no duplicate framework subsystem |
| P1 | Create -> reserve -> verified coverage -> stock/ETKT issue -> GetOrder; only pre-ticket withdrawal/release where current Inventory/Payment contracts make the semantics unambiguous | Full and partial/unknown outcomes, duplicate create/payment/issue and recovery; document eligibility; no general refund/exchange servicing in P1 |
| P2 | Fare context, ancillary types/bundles, taxes/charges/commission, EMD-A and monetary EMD-S | RT vs OW+OW, through fare, inclusive tax, price scope and quantity; no extra charge on retry |
| P3 | pre-ticket cancel, void, voluntary cancel/refund, change, revalidation, exchange/reissue, no-show disposition, ancillary servicing, split/value transfer and traveler correction; finalized ServicingRecord | benchmarked `12` flows; protected irreversible steps; old/new price/document lineage; penalty/waiver/add-collect/refund/residual treatment from authoritative source; payment result tracked separately |
| P4 | DCS receive AND send, correlation, control release/acks, delivery aspects/corrections/no-show | Duplicates, batched messages, late events after split, no false flown/readiness; no-show consequences priced explicitly |
| P5 | FlightOps updates, disruption impact/instruction, same-airline recovery, ancillary treatment, resumable flight fan-out | Aircraft-seat change, cancellation/diversion/RTO, misconnection, late recovery instruction, unknown replacement outcome; **1,000-Order cancellation fan-out <=30s baseline and no long cross-Order lock** |
| P6 | Group blocks/deposits/name deadlines, 50-name import/materialization | Per-block capacity, per-row idempotency, one bad row without repeating successful issue/capture |

For bounded implementation reading, each slice MUST use `11-SLICE-READING-MAP.md`. The map identifies mandatory shared sections plus slice-specific scenarios; unrelated later slices are optional context, not hidden prerequisites.

Cross-carrier recovery, interline settlement automation, NDC wire certification and physical commercial merge are not claimed shipped by P6. Data and extension points are preserved; an unsupported action fails explicitly until its adapter/workflow is certified. Hotel/third-party selling requires a working supplier adapter; the typed data model alone does not satisfy its release gate.

No performance targets, timeouts or staffing estimates from the exploratory questionnaire become mandatory domain constants. Add them as measured/configured operational goals when a vertical slice is runnable.

---

## 15. Test strategy as implementation gate

The prior repository paused tests. This specification explicitly requires automated invariant, safety and integration tests as acceptance gates for this work. No production code tests were executed merely by editing this design. Existing framework behavior must be inspected before reuse; the old pause is not evidence of correctness.

Minimum automated layers:

### Domain invariant tests

- item/service scope;
- pricing reconciliation;
- fare-construction references;
- status derivation;
- lineage;
- document transitions.

### Scenario tests

Use `05-SCENARIO-VALIDATION.md` as acceptance-test specifications. DESIGN-REVIEWED is NOT an executed test pass. Only explicit test execution evidence may be labeled EXECUTED; production code, SQL and provider tests are still required.

### Integration contract tests

- Inventory idempotency;
- Payment fact replay;
- DCS duplicates/out-of-order;
- FlightOps versions;
- Ledger full pricing payload;
- provider unknown outcome/reconciliation.

### Persistence tests

- unique document number;
- concurrency conflicts;
- allocation reconciliation;
- outbox/inbox atomicity;
- read-model projection replay.

---

## 16. Definition of implementation-ready

A slice may start material implementation only when:

1. the relevant business/domain invariants are covered by this pack and its scenario IDs;
2. the exact existing AeroTech Framework/patterns to reuse have been inspected;
3. every external/shared fact has a confirmed owner and treatment (reference/snapshot/projection/Ordering-owned);
4. any monetary/currency/FX/time/ID/security representation required by the slice is mapped to existing platform contracts rather than guessed;
5. unresolved decisions that affect business semantics, public contracts, financial correctness or irreversible provider behavior are recorded as `BLOCKED_DECISION` and the affected code is not written;
6. no new aggregate/entity is introduced merely to mirror a vendor message or convenience DTO;
7. no external state is duplicated as Order truth;
8. price history remains immutable/append-only while current projections/caches remain rebuildable;
9. partial servicing uses stable service/document lineage and distinguishes commercial, document and payment outcomes;
10. true RT and OW+OW remain distinguishable when the accepted Pricing source provides construction;
11. DCS/disruption/payment/provider inputs are idempotent/reconcilable under their actual contracts;
12. normal `GetOrder` remains local/read-model based;
13. implementation follows the existing AeroTech framework and improves it in place only when a demonstrated generic gap warrants that change;
14. production readiness is proved by .NET/SQL/concurrency/fault/provider tests, not by document completeness alone.

Ordinary implementation choices (file/class organization, enum/value-object mechanics, private helpers, EF configuration organization) follow current repository conventions and do not require product-owner confirmation unless they expose a durable business/public-contract semantic.

## 17. Documentation handoff and framework boundary

Install the full pack under `docs/order-domain-design-v1/`. `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` and `IMPLEMENTATION-INSTRUCTIONS.md` are mandatory before coding; use `AGENT-START-PROMPT.md` for the first agent session.

The existing AeroTech Framework/repository is the implementation platform. Reuse its CQRS/application patterns, aggregate/entity bases, UnitOfWork/transactions, outbox/inbox, ID generator, clock, locks, synchronizers, error/result handling, validation, logging/tracing and host composition. Do not create parallel infrastructure because the design uses a new domain concept.

If a real generic framework limitation blocks the Ordering design, inspect sibling usage and improve/extend the **existing** framework/pattern with compatible tests and impact analysis. If the missing concept is cross-service/business-specific (currency/FX/tax/payment/provider semantics, for example), it is not a framework gap; use the authoritative owner contract or `BLOCKED_DECISION`.

The design pack specifies behavior/ownership. It does not mandate C# enum/value-object names, SQL numeric precision or a new monetary type. Logical persistence examples must be mapped to the actual platform representation discovered in P0.

## 18. Implementation support records

`08-CONTRACTS-AND-DATA-DICTIONARY.md` completes required fields for command receipts, operations, claims, evidence, source messages, document links and privacy. Together with sections4/5, it is the normative logical database contract. EF configurations may group storage differently only if uniqueness, ownership, history and transaction guarantees remain testable. No new aggregate is needed for an individual money line, observation, eligibility result or fare component.
