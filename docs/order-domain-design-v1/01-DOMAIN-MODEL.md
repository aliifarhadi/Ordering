# 01 - Final Domain Model

## 1. Domain ownership map

The Ordering microservice hosts several domain modules, but only one is the commercial source of truth.

```text
Ordering (commercial)
  Order

Fulfillment (within same microservice initially)
  FulfillmentReservation
  ElectronicTicket
  ElectronicMiscDocument
  DocumentStock

Group
  GroupBooking

Operational projections
  DeliveryObservation
  ServiceDeliveryState
  SegmentOperationalState
  DisruptionImpact
  PaymentApplicationProjection

Technical workflow
  FulfillmentTask
  ProviderInteraction
  Outbox / Inbox
```

The separation above is semantic. It does not require separate deployables in v1.

---

## 2. Order aggregate

### 2.1 Purpose

`Order` is the commercial master record describing:

- who bought;
- for whom;
- which products/services were accepted;
- the sold journey/product snapshots;
- the commercial terms accepted at sale or servicing;
- the monetary history of the Order;
- the lineage of servicing changes;
- the references required to connect to reservation, payment, delivery, documents and accounting.

It does **not** own canonical payment-provider, inventory, DCS or flight-operations state.

### 2.2 Root fields

```text
Order
-----
OrderId
OrderReference                    # customer-facing/business reference
RootOrderId                       # split lineage
ParentOrderId?
SplitFromChangeId?

OwnerAirlineId                    # trusted deployment owner; not traveler input
FinancialCustomerId               # Core customer financially accountable for the order
SalesContext
BuyerSnapshot
SaleCurrencyReference             # exact platform/source representation; see section 19
CommercialSummary                 # persisted deterministic summary
CustomerTotal                    # transactionally maintained monetary cache using platform representation

CreatedAt
ClosedAt?
CloseReason?

CommercialVersion                # once per committed commercial mutation, not event count
FinancialSequence                # once per committed PriceChangeSet, starts at 0
RowVersion                       # persistence concurrency token
```

Recommended ID policy: preserve the existing platform's globally unique Snowflake/IdGen `long` convention unless the wider platform changes it. Stable global uniqueness is more important than choosing GUID vs long.

### 2.3 SalesContext value object

```text
SalesContext
------------
Channel
SellerCustomerId?
SellerLegalEntityId?
TravelAgencyId?
SellingOfficeId?
ActorUserId?
PointOfSaleCountry
PointOfSaleCurrency
DistributionChainRef?
SoldAt
```

`SalesContext` is a historical snapshot. Do not resolve historical ownership by reading the current Core record only.

### 2.4 BuyerSnapshot value object

```text
BuyerSnapshot
-------------
CustomerId?
BuyerType
DisplayName?
ContactRef?
ExternalCustomerRef?
```

The buyer may differ from Traveler and Payer.

---

## 3. Traveler and contact model

### Traveler

```text
Traveler
--------
TravelerId
CustomerId?
PassengerTypeCode
NameSnapshot
DateOfBirth?
Gender?
Nationality?
LoyaltySnapshots[]
IdentityDocumentSnapshots[]        # protected payload references, not plaintext ledger data
InfantParentTravelerId?
```

Rules:

- Traveler data is owned here; Core owns the financial Customer, not a shared Traveler master. Optional CustomerId is a link, never an instruction to overwrite passenger data from Core.
- Corrections are explicit Order changes with protected before/after personal-data references. Monetary and identity lineage remain; retention/erasure may remove PII payloads without changing monetary facts (section 22).
- Infant-parent relation is explicit.

### Contact

```text
Contact
-------
ContactId
Role                   # Primary, Emergency, Agency, Traveler, Other
Name?
Email?
Phone?
Address?
```

---

## 4. Journey model

### Journey

A customer-facing travel grouping. It is not the fare-pricing unit.

```text
Journey
-------
JourneyId
Sequence
Origin
Destination
JourneyType?
```

### JourneySegment

```text
JourneySegment
--------------
SegmentId
JourneyId
Sequence

SoldScheduleSnapshot
  MarketingCarrier
  MarketingFlightNumber
  OperatingCarrier
  OperatingFlightNumber?
  Origin
  Destination
  DepartureLocal
  OriginTimeZoneId
  ArrivalLocal
  DestinationTimeZoneId
  DepartureUtc?
  ArrivalUtc?
  AircraftType?

SegmentKind                      # ScheduledAir | OpenAir | Surface
ExternalFlightId?
ExternalFlightVersion?
ThroughFlightGroupRef?            # links separately sold segments that share a through-flight identity when supplied
SoldOperationalLegRefs[]?         # optional historical planned legs inside THIS sold passenger segment
```

A `JourneySegment` is the passenger's sold transport segment from board point to off point. It may be fulfilled by one or more physical/operational legs. Physical legs do **not** automatically create extra OrderServices or coupons. Current operational leg composition belongs in `SegmentOperationalState`; the optional sold leg references above are only immutable sale-time context.

Adjacent itinerary semantics are explicit and never inferred from elapsed time:

```text
JourneyConnection
-----------------
FromSegmentId
ToSegmentId
ConnectionKind        # Connection | Stopover | SurfaceBreak | Unknown
ProtectionType        # Protected | Unprotected | Unknown
SourceSystem
SourceReference?
```

A same flight number over multiple physical legs may still be **one** JourneySegment/AirTransportService when the passenger boards once and deplanes at the final off point. If the commercial itinerary contains separately sold passenger segments, keep separate JourneySegments/Services even when they share a flight number; `ThroughFlightGroupRef` records that relationship. This follows the passenger-segment/operational-leg distinction and prevents operational topology from changing commercial identity. `ConnectionKind` does not create or remove a fare break, and `ProtectionType` is not guessed from elapsed time.

The sold schedule snapshot is historical commercial context. Cabin/RBD is passenger-service/fare data, not one shared authoritative cabin on the segment. ScheduledAir requires operating flight identity/date and unambiguous UTC instants; OpenAir may omit a dated flight and cannot be sent as delivery-ready before assignment. Surface represents a gap/ground sector; it never consumes an air seat or requires an air coupon. SegmentSequence defines itinerary display, not fare breaks or fare coupling.

Current operational changes belong in `SegmentOperationalState`, not by destructively rewriting the sold snapshot.

---

## 5. OrderItem

### 5.1 Definition

An `OrderItem` is an individually priced commercial item within the Order and contains one or more `OrderService` instances.

The boundary comes from the accepted Offer/pricing boundary, not from a forced passenger/segment/product-type rule.

```text
OrderItem
---------
OrderItemId
OrderId
SourceOfferId?
SourceOfferItemId?
SourceOwnerCode?
AcceptedPriceSnapshot : [platform monetary representation]?    # immutable sale version, not current balance
ProductSnapshot
CommercialTermsSnapshot
CommercialSource
ItemSalesContext?
CommercialStatus
CreatedByChangeId
ReplacedByChangeId?
CancelledByChangeId?
CreatedAt
CancelledAt?
ReplacedAt?
```

### 5.2 Commercial status

```text
Pending
Active
PartiallyChanged
Cancelled
Replaced
Expired
Partitioned
```

`OrderItem` status is persisted and recomputed in the accepting command transaction from CURRENT membership links; historical predecessor items explicitly remain Replaced/Partitioned. Partial external fulfillment is not a commercial state. Exact derivation and guards are in `07-ELIGIBILITY-AND-LIFECYCLES.md`.

Do not include `Paid`, `Ticketed`, `Flown` or `Refunded` as OrderItem commercial states.

### 5.3 ProductSnapshot

```text
ProductSnapshot
---------------
ProductId?
ProductCode
ProductType
Name
Brand/FareFamily?
SupplierProductRef?
Version?
AttributesJson?            # immutable accepted product attributes
```

Product definition and price are distinct.

### 5.4 CommercialTermsSnapshot

```text
CommercialTermsSnapshot
-----------------------
RefundPolicySummary
ChangePolicySummary
NoShowPolicySummary
PolicySource
PolicyVersion
TermsCapturedAt
PricingContextRef?
OriginalSalePricingDate
UpgradePolicySummary?
BaggageTermsSummary?
Validity?
SourceRuleReferences[]
RawTermsRef?
```

Do not embed a full ATPCO rule engine in Order. Summaries are for display, not executable fare rules. Retain immutable accepted references/version, pricing dates and protected source payload; the authoritative pricing adapter must price servicing from this context or explicitly decline with PricingContextUnavailable. Never silently apply the current catalog rules to an old sale.

---

## 6. OrderService

### 6.1 Definition

`OrderService` is the smallest commercial service unit for which AeroTech needs stable identity and independent servicing/delivery correlation.

For air transport, the normal ordered granularity is one traveler on one passenger segment.

### 6.2 Core fields

```text
OrderService
------------
OrderServiceId
OrderId
OrderItemId                      # current commercial owner only
ServiceType
ServiceCode
Name
CommercialStatus
ServiceVersion                   # increases for current service ownership/definition changes
TravelerId?                      # exactly one for Air/Seat; primary contact only for shared services
BeneficiaryTravelerIds[]          # explicit full set for shared/non-air service
SupplierPartyRef
DeliveryProviderRef?
FulfillmentProfileSnapshot
CreatedByChangeId
ReplacedByServiceId?
PredecessorServiceId?             # convenience for single predecessor, not authoritative for many-to-many
CreatedAt
CancelledAt?
ReplacedAt?
```

Commercial status:

```text
Pending
Active
Cancelled
Replaced
Expired
```

Do not store these canonical states directly on `OrderService`:

- payment status;
- fulfilment provider status;
- delivery/DCS status;
- ticket/EMD status;
- accounting/revenue status.

Those are exposed through projections/links. A flight change creates a successor service. A price-only change does not create a duplicate flight service. An operational seat reassignment does not silently change the product sold.

`FulfillmentProfileSnapshot` contains `RequiresReservation`, `RequiresDocument`, `DocumentKind`, `RequiresPaymentCoverage`, `SupplierRef` and `DeliveryProviderRef`. It is an accepted configuration snapshot, not a live lookup that can change an existing sale unexpectedly.

The same service may be an included benefit or separately sold; `PriceTreatment = SeparatelyPriced | Included | Complimentary | SupplierOpaque` states this without inventing zero-valued money lines. Create a separate service only when it needs independent servicing, supplier execution or delivery tracking; an untracked catalog characteristic can remain a product attribute.

### 6.3 ServiceCoverage

A service may need applicability beyond a single segment.

```text
ServiceCoverage
---------------
OrderServiceId
JourneyId?
SegmentId?
LocationCode?
StartAt?
EndAt?
```

Rules:

- AirTransport requires exactly one Traveler and one Segment.
- Seat normally requires one Traveler and one Segment.
- Paid baggage can cover one or more connected segments/directions depending on product definition.
- Hotel may use no flight segment and instead use date/location/property scope.
- Lounge may be airport/time scoped and optionally associated with a segment.

### 6.4 Typed service details

Prefer a base service table plus one-to-one detail tables for high-value product types.

#### AirTransportServiceDetails

```text
OrderServiceId
SegmentId
CabinSnapshot?
RbdSnapshot?
ServiceClassRef?
MarketingCarrier?
OperatingCarrier?
```

Do not store FareBasis as the authoritative location here; FareBasis belongs to fare-construction context when available.

#### SeatServiceDetails

```text
OrderServiceId
SegmentId
SeatProductCode
RequestedSeat?
SeatCharacteristicsSnapshot?
```

Current seat assignment may also be surfaced from a DCS/seat projection if operational assignment can differ from sold seat product.

#### BaggageServiceDetails

```text
OrderServiceId
BaggageKind          # allowance, prepaid bag, excess weight, sports equipment, etc.
AllowanceBasis       # PerTravelerPortion | PerPiece | SharedPool
AppliesToAirServiceIds[]
PoolingPolicyRef?
PieceCount?
Weight?
WeightUnit?
Dimensions?
SpecialItemCode?
```

#### MealServiceDetails

```text
OrderServiceId
SegmentId?
MealCode
Quantity
SpecialMealCode?
```

#### LoungeServiceDetails

```text
OrderServiceId
AirportCode
LoungeCode?
AccessWindowStart?
AccessWindowEnd?
GuestCount?
```

#### HotelServiceDetails

```text
OrderServiceId
SupplierCode
PropertyCode
CheckIn
CheckOut
RoomType
RoomCount
GuestCount
GuestTravelerIds[]
RatePlanRef?
SupplierBookingRef?
```

#### GroundTransportServiceDetails

```text
OrderServiceId
SupplierCode
PickupLocation
DropoffLocation
PickupAt
VehicleType?
PassengerCount?
BeneficiaryTravelerIds[]
```

#### GenericServiceDetails

```text
OrderServiceId
SchemaName?
SchemaVersion?
AttributesJson
```

This is an extension escape hatch, not the default for core air/ancillary types. SchemaName and SchemaVersion are REQUIRED when JSON is used. A registered validator and fulfillment profile are required before sale; unknown schemas are not silently accepted. Priority, WiFi, Insurance, CIP and SIM use versioned detail schemas until queries/invariants justify a typed table. This does not postpone their core product semantics or remove them from scenario coverage. Special-assistance products such as wheelchair assistance, UMNR, PETC/AVIH and meet-and-assist use a registered versioned schema/profile unless behavior proves a dedicated typed table necessary. Sensitive assistance/guardian/medical-adjacent details are stored through a protected payload reference and minimized in normal Order read models; the generic JSON escape hatch is not permission to dump unbounded PII.

### 6.4.1 Required type-specific validation

| Type | Required local invariants |
|---|---|
| AirTransport | Exactly one beneficiary and one ScheduledAir/OpenAir segment; accepted cabin/RBD per source; capacity/issuer requirements come from the fulfillment profile. No fabricated flight/time for an open segment. |
| Seat | Exactly one traveler and associated air segment/service; requested seat or accepted seat-product characteristics supplied. Current DCS assignment is not a rewrite of sold seat characteristics. |
| Baggage | Valid allowance/product kind; positive granted pieces/weight when a counted entitlement is sold; weight/dimensions carry units. A piece allowance may ALSO have a per-piece weight cap; the two are not mutually exclusive. Coverage and pooling follow the sold product. |
| Meal | Positive integral count when quantity is counted; meal code and relevant air-service/coverage association; free/included treatment is independent of quantity. |
| Lounge | Valid airport/location and access window when time-restricted; named beneficiary and nonnegative guest allowance; optional rather than mandatory flight reference. |
| Hotel | CheckOut later than CheckIn; positive integral rooms and valid guest set/count under source occupancy rules; property/room/rate plan identified. One service per independently cancellable supplier room-stay; aggregate identical rooms only if supplied/serviced as a unit. Nightly pricing lines do not force one service per night. |
| GroundTransport | Supplier-confirmed pickup/dropoff scope and time/window; positive vehicle/passenger count as defined by product; all shared beneficiaries explicit. Vehicle price is not multiplied by guest count. |
| Generic registered detail | Schema name/version, validated required fields, beneficiary/coverage constraints, product-specific validity and declared fulfillment/document policy. Unsupported schema blocks acceptance. |

Changing a sold quantity or reducing a multi-piece/stay package requires an explicit accepted change with its pricing and provider treatment. A partially delivered quantity is tracked as operational quantity/portion evidence, not by silently shrinking the sold service or its historical money.

OpenAir services can be issued only under an issuer profile explicitly supporting open coupons and the corresponding reservation exemption. They are NOT ReadyForDelivery to DCS until a concrete flight binding and its required capacity/control evidence exist. A Surface segment is itinerary context, not an air transport obligation or fabricated ticket coupon. Revalidation/binding retains prior document and pricing context.

### 6.5 Dependencies and membership history

`ServiceDependency(OrderServiceId, RelatedAirServiceId, Kind, OnChangePolicyRef)` represents only required association/coverage, e.g. a seat or meal for a flight. Kinds in v1 are `RequiresAirService`, `CoverageMember`, `BundledWith`; no arbitrary dependency graph language. Cycles in RequiresAirService are invalid. Cancel/rebook evaluates all affected dependents and records Keep/Replace/Cancel/ManualReview; never leaves an active paid seat attached to a replaced flight accidentally.

`OrderItemServiceLink(LinkId, OrderIdAtAssociation, OrderItemId, OrderServiceId, SegmentIdsAtAssociation, LinkedByChangeId)` is immutable commercial membership evidence. Current ownership is `OrderService.OrderId/OrderItemId`; links are not separate aggregates. Link snapshots contain non-PII sold scope/product identifiers. Old item history can therefore reference a service now owned by a successor item or child Order. Do not recompute an old item's sold contents from the service's current owner.

Shared hotel/transfer/pooled-baggage services may have multiple beneficiaries. A split may move the entire shared service only if all relevant beneficiaries move. Otherwise require a supported supplier/pricing partition, or reject `SharedServiceCannotBePartitioned`; never duplicate one room, car or pooled allowance.

---

## 7. Air fare-construction model

### 7.1 Why it exists

`PricingLine + Allocation` cannot tell whether two segments belong to one true RoundTrip pricing unit or two independent one-way units. That distinction affects exchange, refund, no-show and repricing.

Therefore Order owns immutable `AirFareConstruction` snapshots linked to one or more relevant OrderItems. The construction is a pricing context, not a new aggregate. This permits cross-item fare coupling without duplicating one PU as several independent PUs.

### 7.2 Structure

```text
AirFareConstruction
-------------------
FareConstructionId
OrderIdAtCreation
CreatedByChangeId
SupersedesConstructionId?
OrderItemRefs[]
ConstructionType
Source
SourcePricingRef?

PricingGroup[]
```

ConstructionType:

```text
OneWay
RoundTrip
RoundTripFromOneWays
OpenJaw
CircleTrip
Mixed
DynamicProviderDefined
```

### 7.3 PricingGroup

Represents travelers sharing exactly the same source pricing construction. Default normalization is one traveler per PricingGroup; group several only when the source explicitly groups them and extended/per-traveler amounts are unambiguous. Equal PTC alone is insufficient.

```text
PricingGroup
------------
PricingGroupId
FareConstructionId
PassengerTypeCode
TravelerIds[]
Quantity
```

Example: two adults may share one construction while one child has another.

### 7.4 PricingUnit

```text
PricingUnit
-----------
PricingUnitId
PricingGroupId
Type
CombinationMethod
RepricingContextRef?
Sequence
```

Type:

```text
OneWay
RoundTrip
OpenJaw
CircleTrip
Other
```

CombinationMethod:

```text
FiledFare
LocalCombination
Dynamic
ProviderDefined
```

### 7.5 FareComponent

```text
FareComponent
-------------
FareComponentId
PricingUnitId
Sequence
Origin
Destination
SegmentRefs[]
FareBasis?
FareFamily?
Brand?
FareType?
Cabin?
Rbd?
FareOwnerCarrier?
TariffRef?
RuleRef?
RoutingRef?
CommercialTermsRef?
```

Invariants:

- Service count is not FareComponent count.
- One FareComponent may cover multiple connected segments.
- One PricingUnit may contain multiple FareComponents.
- A RoundTrip can be one PU or constructed from one-way components according to Pricing output.
- Ordering never invents fare construction from itinerary shape.
- Fare construction is optional when Pricing cannot/should not provide it; retain an opaque immutable pricing context for servicing.
- FareComponent covers explicit ServiceIds or `(TravelerId, SegmentId)` associations, not every passenger on a segment.
- A quote explicitly identifies its `AffectedPricingUnitIds`, `AffectedOrderItemIds` and pricing-group/traveler scope. PU is a useful input to repricing, not an absolute guarantee that a provider will never reprice a wider context.
- Source logical prices are normalized once. A line amount is always the extended monetary amount, never a per-traveler amount multiplied again by group quantity.
- Previous constructions remain immutable. Active construction bindings change only in an accepted OrderChange; current lookup is indexed, not a scan of every historical construction.

---

## 8. OrderChange and lineage

### 8.1 OrderChange

Every accepted commercial mutation receives a stable `ChangeId`.

```text
OrderChange
-----------
ChangeId
OrderId
ChangeType
Reason
Source
ExternalReference?
ActorContextSnapshot
OccurredAt
```

Types:

```text
Create
AddProduct
Cancel
VoluntaryChange
Exchange
Reaccommodation
InvoluntaryChange
NameCorrection
Split
ManualAdjustment
Close
```

### 8.2 Item/service lineage

When the commercial product/scope changes:

```text
Old OrderItem -> successor OrderItem
Old Service   -> successor Service
```

Store `OrderServiceLineage(ChangeId, PredecessorServiceId, SuccessorServiceId, RelationType)` and equivalent item links. They support one-to-many and many-to-one replacements; the optional single-ID shortcuts are never the source of truth. Old and new flights may have different segment counts. A price-only replacement can reuse an unchanged ServiceId through a new item membership link.

Do not use identity replacement for simple operational observations such as schedule-time update, check-in or boarding.

### 8.3 Split lineage and a deterministic v1 operation

Split is an explicit organization/servicing action, not mandatory merely because passengers now have different itineraries. The model supports non-homogeneous traveler journeys within one Order. [IATA reference in `06`, B-004.]

Default v1 divide policy: select whole travelers and move ALL of their exclusively owned services (including historical consumed services) to a child Order while preserving TravelerId and OrderServiceId. Do not split an infant from its associated adult. A service with beneficiaries on both sides requires an explicit partition result or blocks the divide.

1. Claim source Order; reserve stable child OrderId and SplitTransferId in an Operation record. Freeze relevant commercial and coverage versions.
2. Validate customer/currency/owner, external provider divide support, document control and a priced monetary partition. Grouping travelers is not permission to reuse payment twice.
3. Obtain durable provider divide outcome and a Payment transfer reservation/acknowledgment; an unknown outcome remains a pending operation and does not start another divide.
4. In ONE local transaction, create child Order; move current traveler/service ownership; partition items; create current child journey/segment snapshots and remap moved services' CURRENT coverage. Save the old/new mapping in OrderChange. Historical sold membership, pricing and observation records retain their original identities and OrderIdAtOccurrence.
5. Clone only order-local journey snapshots, not the service identity or the sale. A shared physical flight still has the same ExternalFlightId. Successor fare snapshots reference the appropriate new segment/item IDs; old fare snapshots retain old source references.
6. Append paired source/child SplitTransfer money lines, equal and opposite per component/currency/valuation. Notify Ledger as RECLASSIFICATION, not refund plus new sale. External Payment applies balanced application transfers once; a pending transfer blocks new paid actions but not receipt of facts.
7. Update current document servicing owner only where the whole traveler's document relationship moves. Immutable original issued OrderId, coupons, consumed statuses and issuance amounts stay unchanged. Published source/child mappings let DCS resolve late messages.
8. Recompute both summaries; bump each Order's CommercialVersion once; publish paired transfer events plus current delivery mapping in the same transaction.

Historical source views use immutable membership/change/price references, not the current owning Order of a moved service. Current same-Order invariants apply to CURRENT rows; they do not invalidate legitimate historical references across the split.

Payment can have applications to multiple Orders. What is forbidden is COPYING an application amount to a second Order. A new explicit balanced transfer with unique ApplicationIds is valid. A divide after partial travel does not reopen consumed coupons or recreate earned revenue.

Merge is NOT a physical inverse implemented by copying children. v1 offers `LinkRelatedOrders` for shared servicing visibility; true commercial merge is explicitly unsupported until a priced/provider-supported merge workflow is supplied. Existing orders remain separately accountable.

---

## 9. Time limits

```text
TimeLimit
---------
TimeLimitId
Type
DueAt
Status                         # Active | Met | Cancelled | Expired
PolicyRef
PolicyVersion
ScopeType
OrderItemId?
OrderServiceId?
TravelerId?
Reason?
```

Types can include:

```text
Reservation
Payment
Ticketing
Name
Servicing
Supplier
Other
```

Time limits are constraints, not Order workflow states. Expire only when `now >= DueAt`, scope is still unsatisfied, and no pending irreversible operation needs reconciliation. Unknown payment is neither paid nor failed. Expiry first reconciles/resolves the protecting operation or releases a hold according to an explicit decision; it never automatically overwrites a later valid issuance result. Issued services have document/travel validity; they do not become indefinitely valid because the booking hold deadline was cleared.

---

## 10. External references

```text
ExternalReference
-----------------
ExternalReferenceId
ReferenceType
ProviderType
ProviderCode?
Value
OrderItemId?
OrderServiceId?
CreatedAt
```

Examples:

- PNR/record locator;
- supplier reservation reference;
- partner Order reference;
- NDC Order reference;
- legacy system reference;
- group booking reference.

Use indexed canonical correlation bindings for command/event matching; optional read-side search indexes are for display/search, not authority. See `03` section 5.2.

---

## 11. FulfillmentReservation aggregate

### 11.1 Purpose

Represents Ordering's durable binding to a supplier/inventory reservation outcome. It is not the source of truth for seat capacity.

```text
FulfillmentReservation
----------------------
FulfillmentReservationId
OrderId
ProviderType
ProviderCode?
ExternalReservationRef?
Status                       # deterministic roll-up of service result rows
OperationId
ExpiresAt?
LastSourceEventId?
LastUpdatedAt

ServiceLinks[]
```

Statuses:

```text
Pending
Waitlisted
Confirmed
Rejected
CancellationPending
Released
Unknown
Expired
Mixed
```

CancellationPending is local operation intent, not evidence the supplier has released inventory. HasUnknown and per-member counts remain explicit when the root is Mixed.

Service link:

```text
FulfillmentReservationService
-----------------------------
FulfillmentReservationId
OrderServiceId
ExternalServiceRef?
ObservedStatus                # Pending | Waitlisted | Confirmed | Rejected | Released | Expired | Unknown
ObservedProviderVersion?
ObservedBookingClass?
ObservedCabin?
ConfirmedQuantity?
ValidUntil?
ExternalStatus?
```

Reservation/capacity coupling is explicit only when the source says members are not independently usable:

```text
ReservationCouplingGroup
------------------------
CouplingGroupId
FulfillmentReservationId
Kind                     # Independent | MarriedSegments | ProviderAtomicSet
ExternalGroupRef?
OrderServiceIds[]
```

For a married/atomic group, issuance/readiness cannot treat one confirmed member as independently usable while another required member is Rejected/Unknown/Waitlisted. Reconciliation or a provider-approved replan resolves the group. Waitlist is a reservation outcome; airport standby remains a DCS/delivery fact. A single batch can contain Confirmed and Unknown members; no root Confirmed value hides partial results. Keep the confirmed members and reconcile only unresolved members under the same operation identities. This is the domain replacement for overloading `OrderService.FulfillmentStatus` while keeping current FulfillmentTask as the technical execution mechanism.

---

## 12. ElectronicTicket aggregate

ElectronicTicket is the authoritative locally issued document, or a synchronized external document when `Authority=External`. Provider-authoritative status is never invented from a timeout.

```text
ElectronicTicket
  TicketId, OriginalOrderId, CurrentServicingOrderId, TravelerId
  DocumentNumber, IssuerCarrier, IssuingOffice, Authority
  IssuedAt, ValidFrom, ValidUntil, VoidDeadline
  StatusSummary, OriginalTicketId?, ExchangeChainRef?
  IssuanceAmounts : DocumentAmounts       # immutable accepted monetary breakdown using the platform representation
  PassengerSnapshotRef                  # protected PII, section 22
  Coupons[], DocumentVersion, RowVersion

TicketCoupon
  CouponId, TicketId, CouponNumber, OrderServiceId
  IssuedSegmentSnapshot, CurrentServiceBinding
  FinancialStatus : Open | Used | Void | Exchanged | Refunded | Suspended
  ControlStatus : Local | External | ReleasePending | Unknown
  ControlHolder?, ControlToken?, ControlVersion?
  ProviderCouponStatusCode?, ProviderVersion?
  FareBasisSnapshot?, IssuanceValue : [platform monetary representation]?
```

Check-in, boarding, offload and no-show are delivery facts/aspects. Adapter coupon codes may express them, but a universal fictional NoShow financial status is not required. No-show does not itself consume/refund a coupon. Keep raw external code plus normalized control/financial status. Transition guards are in `07`.

A document can contain used and unused coupons. Refund/exchange operates on eligible UNUSED coupons, not on all coupons indiscriminately. Void is a whole-document issuance cancellation within the issuer window unless the provider explicitly certifies a narrower legal operation; a check-in must be reversed and control regained before an otherwise eligible void/refund.

Revalidation changes an authorized current coupon-service binding through an audited DocumentChange; it preserves the issued snapshot. Exchange creates a new document and explicit old/new coupon links, including one-to-many mapping. A same-service duplicate active coupon is rejected unless a declared conjunction/exchange protocol temporarily requires and reconciles it.

`DocumentPriceLink(DocumentId, CouponId?, PricingLineId, AllocationId?, AttributedValue: [platform monetary representation])` records the ISSUE-TIME value link. It does not rerun current allocations when a past ticket is retrieved. Totals never count these links as new sales.

## 13. ElectronicMiscDocument aggregate

```text
ElectronicMiscDocument
  EmdId, OriginalOrderId?, CurrentServicingOrderId?, GroupBookingId?
  TravelerId?, DocumentNumber, EmdType : Associated | Standalone
  IssuerCarrier, IssuedAt, ValidUntil, VoidDeadline
  ReasonForIssuanceCode, ProviderDocumentCode?, StatusSummary
  IssuanceAmounts : DocumentAmounts, DocumentVersion, RowVersion
  EmdCoupons[]

EmdCoupon
  CouponId, EmdId, CouponNumber, ReasonForIssuanceSubCode
  Purpose : Service | Fee | Deposit | ResidualValue
  OrderServiceId?                     # required for Purpose=Service
  PricingLineId?                      # fee/penalty monetary link
  ExternalValueRef?                   # payment/deposit/stored-value owner
  AssociatedTicketCouponId?           # required for associated linkage
  ProviderCouponRef?, FinancialStatus, ControlStatus
  IssuanceValue : [platform monetary representation]
```

Exactly one primary purpose is required. `Purpose=Service` requires ServiceId; Fee requires a monetary line; Deposit/ResidualValue requires a canonical external value/application reference. EMD-A associates at COUPON level, not only to a ticket header. An EMD-S for a change fee, group deposit or residual value must NOT require a fake seat/flight service. Conversely an EMD is not a new wallet liability or a second payment capture. This is supported by actual issuer EMD-S usage; see `06`, B-007.

ETKT and EMD have separate invariants even when base value types and persistence routines are shared. Keep currency, RFIC/RFISC, issuer, association and provider capability validation explicit. Maximum coupons, format and allowed association changes come from the certified issuer adapter profile; arbitrary defaults are not production configuration.

---

## 14. DocumentStock aggregate

```text
DocumentStock
-------------
DocumentStockId
OwnerCarrier
OfficeId?
DocumentType
Prefix
SerialWidth
CheckDigitProfile
RangeFrom
RangeTo
NextNumber
Status
Version
```

Statuses:

```text
Active
Suspended
Exhausted
Closed
```

Allocation rules:

- sequential within configured stock;
- atomic with document-number reservation/issuance boundary;
- never random-generated as the authoritative production strategy;
- auditable allocation history;
- uniqueness enforced across the issuer's actual numbering namespace, not an assumed globally unique unqualified string;
- stock ranges must not overlap for the same namespace; check atomically under namespace serialization, not a simple unique range-start index;
- `StockAllocation(OperationId, DocumentRole, Number, State)` is durable BEFORE external issue; retried issue reuses it;
- number states Reserved/Issued/Retired: abandoned or uncertain numbers are retired/reconciled, never silently recycled;
- a provider that owns its stock returns its number; Ordering must not also allocate a competing local number;
- prefix, width and check-digit formatting are certified issuer/stock configuration, not an invented universal IATA allocation algorithm.

---

## 15. GroupBooking aggregate

Group Booking remains separate because a block of unnamed capacity is not an ordinary passenger Order.

```text
GroupBooking
------------
GroupBookingId
GroupReference
SalesContext
Status
ClosedAt?

SeatBlocks[]
NameSlots[]
TimeLimits[]
SpawnedOrderRefs[]
DepositRefs[]
```

SeatBlock:

```text
FlightRef
Cabin
Rbd?
SeatsHeld
SeatsAllocated
SeatsReleased
InventoryBlockRef?
BlockVersion
```

NameSlot:

```text
NameSlotId
Status
BlockAssignments[]              # per-flight/portion association, not sum of all held seats
MaterializationRequestId?
ClientPassengerRef?
TravelerNameSnapshot?
PassengerTypeCode?
SpawnedOrderId?
```

Payment lifecycle itself remains in Payment; GroupBooking stores only payment/deposit references/projections required for commercial eligibility. Group name data is a protected staging input, not a second Traveler master after materialization.

For EACH block: `Allocated + Released <= ConfirmedCapacity`. A name assigned to outbound and inbound consumes one unit on EACH block. Never validate allocated passengers against the sum of seats across flights.

Bulk 50-name import returns row-level outcomes with stable ClientPassengerRef/MaterializationRequestId. Repeating an accepted row returns its existing Order/Traveler/document outcome. Validate the batch before committing eligible rows; one invalid passport does not replay 49 successful charges/issuances. Booked seat numbers are not inventory capacity counts. A charter allotment allocation transfers/consumes the EXISTING inventory block, not an additional general-sale seat.

Store CharterContractRef and PriceQuoteRef where relevant. Record agency financial-customer liability independently of end-traveler retail price; do not charge the charter contract again on name materialization. A group deposit is transferred/applied by the Payment owner with balance preservation, not copied to each spawned Order.

---

## 16. Operational projections

### ServiceDeliveryState

```text
OrderServiceId
CurrentStatus
LastMilestone?
LastOccurredAt?
LastSourceSystem?
Version
```

Normalized current statuses:

```text
NotReady
Ready
InProgress
Delivered
NotClaimed
FailedToDeliver
UnableToDeliver
Expired
Suspended
Removed
```

### DeliveryObservation

```text
ObservationId
SourceSystem
SourceEventId
OrderId
OrderServiceId
TravelerId?
SegmentId?
ObservationType
OccurredAt
ReceivedAt
SourceSequence?
SourceEpoch?
SourceRevision?
ObservationKey                    # one source message may contain several passenger facts
SupersedesObservationId?
Aspect                            # CheckIn | Boarding | Travel | Seat | Baggage | Consumption
PayloadHash?
```

Observation types include:

```text
CheckedIn
CheckInCancelled
Boarded
Offloaded
NoShow
Flown
ServiceConsumed
AncillaryConsumed
SeatAssigned
SeatChanged
BaggageAccepted
BaggageLoaded
BaggageDelivered
```

### SegmentOperationalState

```text
SegmentId
FlightKey
CurrentDeparture
CurrentArrival
CurrentOrigin
CurrentDestination
CurrentAircraft?
CurrentStatus
OperationalOutcome?        # Scheduled | Departed | Arrived | Cancelled | Diverted | ReturnedToOrigin
DiversionAirport?
ActualArrivalAirport?
LastFlightVersion?
UpdatedAt
```

### DisruptionImpact

```text
ExternalDisruptionId
OrderId
ImpactType
Reason
Severity?
Status
DetectedAt
ResolvedAt?
AffectedOrderServiceIds[]
AffectedTravelerIds[]
AffectedSegmentIds[]
RecoveryReference?
```

This is a local projection; the external disruption capability remains canonical. When the recovery owner requires customer acceptance, the projection also retains the source-provided action-required/decision-deadline/option/disposition evidence needed by GetOrder and eligibility. Exact code/state representation follows the current repository. Ordering does not invent what happens at the deadline.

### PaymentApplicationProjection

```text
PaymentApplicationId
PaymentId
OrderId
MovementId
MovementKind
ConfirmedAmount : [platform monetary representation]
OriginalMovementId?
SourceVersion
CoverageTokenRef?
ApplicationStatus
AppliedAt
ReversedAt?
AllocationRefs[]
```

Used for Order read models and fulfilment eligibility. Payment lifecycle remains external.

---

## 16A. Finalized servicing records

Ordering must support redisplay/audit of a finalized refund, exchange/reissue, revalidation, void or other servicing operation without recalculating current fare/rules. This is a **semantic read/audit capability**, not another aggregate by default.

A finalized servicing record correlates the facts that actually occurred:

- servicing operation/change identity and reason (voluntary/involuntary/correction);
- affected OrderItem/OrderService scope;
- original and successor reservation/document/coupon references;
- accepted Pricing/change/refund decision and immutable price-change references;
- penalty/waiver/authority evidence when supplied;
- confirmed Payment/value-movement references and unresolved outcomes;
- provider confirmations, actor/seller/office context and timestamps required for support/audit.

The record never recomputes `fare used`, refundable tax, penalty, FX or residual value. If such facts are displayed, they come from the immutable accepted/confirmed source results already retained by the relevant boundary. A corrective operation appends another record/lineage link; it does not edit the prior financial history.

Refund/exchange notices and receipts are derived presentation artifacts. Ordering exposes/publishes the finalized facts; rendering/storage/delivery reuses the existing platform capability when identified. If that owner is not established, implementation stops at the boundary under `13` rather than introducing a document engine in Ordering. See `12` for benchmarked flows.

---

## 17. Commercial lifecycle and materialized summaries

Persist `Order.CommercialSummary` and `OrderItem.CommercialStatus` in SQL, with private/domain-controlled mutation and indexes. Their VALUE is derived; their STORAGE is real. Querying Active orders must not hydrate every aggregate.

`Order.CommercialSummary = Draft | Active | Cancelled | Inactive | Closed` is recomputed by the rules in `07`, in the same local transaction as the mutation. ClosedAt is an explicit fact. Expired or mixed terminal contents map to Inactive, not falsely Cancelled. Completed is a delivery/read facet: used services remain historically accepted, not commercially cancelled.

Independent read facets are ReservationSummary, PaymentSummary, DocumentSummary, DeliverySummary, DisruptionSummary and BalanceSummary. No caller sets Paid/Ticketed/Flown as a commercial source of truth.

Authorization uses `EligibilityDecision` over canonical local command-side facts and required current external evidence, never only a summary label. The same eligibility policy powers UI explanations, but the command MUST re-evaluate before execution and before finalization. Persisting a summary does not make stale UI data safe for payment or document decisions.

---

## 18. Core invariants

### Order / identity

- INV-O01: all child IDs are globally unique within the platform ID policy.
- INV-O02: every OrderItem belongs to exactly one Order at a time.
- INV-O03: every OrderService belongs to exactly one OrderItem and Order at a time.
- INV-O04: a CURRENT service beneficiary must be a current Traveler in its Order; historical membership/sale facts preserve pre-split scope and are not subject to this current ownership constraint.
- INV-O05: an air Service must reference exactly one Traveler and one JourneySegment.
- INV-O06: child Order lineage must preserve RootOrderId.

### Commercial

- INV-C01: an OrderItem may contain one or many Services.
- INV-C02: Service type does not define OrderItem boundary.
- INV-C03: cancellation/replacement is scoped to explicit Service/Item identities.
- INV-C04: a replaced Service cannot become active again; use a successor.
- INV-C05: item status must be consistent with contained service statuses.

### Fare construction

- INV-F01: PricingUnit/FareComponent structure is accepted from Pricing/Offer; Ordering does not infer it from itinerary shape.
- INV-F02: a newly accepted current FareComponent references covered services/segments in its current Order; immutable prior constructions keep their original historical references after split.
- INV-F03: a FareComponent may cover multiple segments.
- INV-F04: multiple travelers with different pricing construction require separate PricingGroups.
- INV-F05: FareConstruction is optional.

### Documents

- INV-D01: an ETKT coupon and a service-purpose EMD coupon reference an existing stable OrderService; fee/deposit/residual EMD-S uses the explicit monetary/value link instead.
- INV-D02: a document number is unique for issuer/document type according to stock policy.
- INV-D03: flown/consumed coupon rules restrict void/refund as defined by document policy.

### Operational input

- INV-X01: source-message dedup is `(SourceSystem, SourceEventId, Consumer)`; individual observations use `(SourceSystem, SourceEventId, ObservationKey)` to support batched messages.
- INV-X02: an older/out-of-order observation cannot blindly regress current delivery state.
- INV-X03: flight operational updates never destructively erase the sold schedule snapshot.
- INV-X04: disruption impact alone never mutates price or commercial status; an explicit OrderChange does.


---

## 19. Shared semantic requirements and platform representations

This section defines **meaning**, not a new shared primitive library. `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` controls representation and ownership.

### Monetary facts and currency

Ordering must be able to persist and compare the monetary facts required by the accepted sale/change/refund result. At minimum, a monetary fact is never meaningful without its currency identity and provenance required by the source contract.

Binding rules:

1. Reuse the current AeroTech monetary/currency representation already used by the platform and the relevant source contract. This document does not authorize creation of a second `Money`, `CurrencyCode`, `ExchangeRate`, currency master or rounding library.
2. AirInfo/BasicInfo is the current currency-reference source and AirPrice is the current rate-of-exchange source according to the reviewed AeroTech architecture. Ordering consumes those boundaries through the established ReferenceData/adapter pattern.
3. Ordering stores the exact accepted/source-provided values needed to preserve commercial history. When the source provides original amount, sale/converted amount, applied-rate reference/snapshot, rounding evidence or tax calculation provenance and those facts are needed for audit/reversal/servicing, preserve them without recomputing them later.
4. Historical accepted amounts are never rewritten using today's fare, tax, currency or FX data.
5. Cross-currency arithmetic is not performed implicitly. A conversion is accepted only as part of an authoritative Pricing/AirPrice/provider result or another explicitly approved owner contract.
6. Ordering may validate that a supplied breakdown reconciles where the source contract gives sufficient data. It does not invent a missing conversion, tax, penalty, fare split or rounding residual.
7. Quantity/units/percentage/application dimensions are preserved when they matter to understand a source-calculated price. Their concrete .NET/SQL representation follows current platform patterns.
8. Document issue-time amounts are frozen as the issuer/source-confirmed commercial view. Later repricing does not mutate them.

Any ambiguity in currency identity, numeric representation, conversion convention, rounding responsibility or source ownership is a `BLOCKED_DECISION` under `13`; the coding agent must not resolve it by introducing a local default.

### Time and identity

Reuse the current platform clock, timestamp, ID and persistence-concurrency conventions. Ordering must distinguish:

- an absolute event/audit instant;
- local flight date/time plus the authoritative location/time-zone context required to correlate flights correctly;
- date-only concepts such as hotel stay boundaries when the source contract treats them as dates.

The design does not mandate a new time library. Never infer local flight identity from the application server timezone.

All domain/persistence IDs follow the current AeroTech generator/conventions unless an approved platform change says otherwise. Display references, document numbers, provider references and database identities remain semantically distinct.

### Representation rule

Names shown in diagrams such as `Money`, `Direction`, `Effect`, `Currency`, `Status` or similar are semantic shorthand from earlier design iterations unless the current repository/contract already defines the corresponding type. They must not be treated as instructions to create a new enum/value object/library. The implementation agent maps the semantic requirement to the established AeroTech representation and uses `BLOCKED_DECISION` only when that representation changes business/cross-service meaning.


## 20. Aggregate loading and mutation boundary

Order owns CURRENT commercial children, membership and the acceptance of new monetary facts. Immutable history collections are not unbounded eager-loaded navigation properties. `LoadForServicing(orderId, targetIds)` must load the target services, dependent services, relevant current price context, scope totals, evidence and Order concurrency row. Domain policies MUST NOT derive whole-order status from an incomplete collection.

After mutation, deterministic SQL/domain projection code in the SAME local transaction recomputes cached totals/summary from complete indexed current rows. Historical pricing is append-only queried by indexed line/ChangeId; a normal cancellation does not load every old event. One invariant implementation is reused for commit and rebuild. The simpler full-current-aggregate loader is acceptable initially for ordinary orders; no separate aggregate per FareComponent, PricingLine or observation is introduced.

Pure policies hold business eligibility logic; application services obtain evidence and coordinate external steps. EF mapping/serialization, broker messages and HTTP statuses do not become domain concepts. Existing ExceptionFactory mechanics may remain at the framework boundary.

## 21. Version and ownership rules

Create establishes `CommercialVersion=1`. Each accepted, committed commercial command affecting an existing Order increments it EXACTLY ONCE, even if it emits several events. A multi-call operation increments only when the commercial change commits. Duplicate/no-op/rejected commands do not increment. A correction of traveler details or material change to accepted money/terms increments; DCS, flight, payment, document and projection-only updates do not.

Both Orders changed by split increment once (new child starts at 1). `RowVersion` is the independent optimistic-concurrency token. `ServiceVersion`, DocumentVersion, DeliveryVersion, FinancialSequence and source projection versions have separate scopes, defined in `03` and `04`.

This intentionally replaces the old CLAUDE.md event-count version rule. Every integration event includes its owning stream identity and sequence. EventId identifies the FACT, not the business version; republish keeps the same EventId.

## 22. Airline scope and personal-data lifecycle

### Scope

v1 is one owning airline per deployment/database, as requested. `OwnerAirlineId` is explicit on Order and aggregate roots and derived from trusted configuration. External seller, supplier, marketing carrier, operating carrier, validating/issuing carrier and financial Customer are different roles even if initially equal. Never infer ownership from a customer-supplied TenantId or carrier code.

Existing platform TenantId is transported from trusted context; it is not hardcoded to 1 in new messages. It does not imply pooled multi-tenancy. Supporting several owner airlines later requires a deliberate isolation migration; current owner-keyed indexes and contracts reduce that cost, but do not promise zero-cost tenancy.

### Privacy versus monetary immutability

Monetary/lineage immutability applies to commercial facts, not perpetual retention of passport numbers, names, contact details or raw provider payloads. Store sensitive snapshots in separable `PersonalDataPayload` records; immutable facts retain pseudonymous IDs/payload references. Encrypt payloads using the existing platform key-management mechanism. Search copies, caches, raw payloads, message retention and backups are included in deletion planning.

`EraseOrRestrictTravelerData` requires an authorized retention decision and legal-hold evaluation. If deletion is approved: remove/redact eligible payloads and search copies, invalidate caches, record a non-PII erasure marker, notify downstream controllers/processors and prevent rebuild/replay from resurrecting erased PII. Existing accounting amounts remain intact. Where granular envelope encryption exists, destruction of the relevant wrapped key may be used; it is not a universal substitute for purging plaintext copies or a guarantee of legal compliance.

A minimal RetentionPolicy is deployment configuration with data category, purpose, review/delete trigger, protected hold and approver. No retention durations are fabricated here. GDPR Article 17 has legal-obligation/claims exceptions; local applicability and exact retention durations are a deployment legal decision [B-012 in `06`], not a reason to delete a required financial record indiscriminately.

## 23. Structural completeness checks

Every new sale or accepted change must reconcile: current service ownership, beneficiaries, typed detail discriminator, coverage validity, supplier fulfillment profile, price context binding, money totals, dependent ancillary treatment and operation scope. Fail with a named reason before any new external side effect if a required input is absent. Historical monetary references are never repaired by changing IDs or substituting current prices.
