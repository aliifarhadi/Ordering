# P2-D — Service Semantic Audit (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08

Every persisted field on the service model is listed below. A field is **Keep** only when all four hold:
Ordering owns the fact, there is a concrete Ordering use case, the meaning is source-independent, and there is
an authoritative input. "ATPCO / NDC / AirPrice has it" and "might be useful later" are not reasons and were
not accepted.

---

## 1. `OrderService` — the common, concrete service entity

| Field | Ordering meaning | Concrete use case | Authoritative input | Verdict |
|---|---|---|---|---|
| `Id` | Service identity | Every downstream reference (pricing basis, coverage, fare component, coupon) | IdGen snowflake | Keep |
| `OrderId` | Owning aggregate root | Aggregate boundary, repository load | Aggregate | Keep |
| `OrderItemId` | **Current** commercial item the service belongs to | Item roll-up, cancel/void cascade, projection | Accepted source product → item | Keep |
| `ServiceType` | What kind of thing was sold | Selects the typed detail, fulfilment routing, blocked-type gate | Accepted source | Keep |
| `ServiceCode` | Airline-facing code for the service | Agent display, reporting, EMD RFISC-style reporting later | Accepted source | Keep |
| `Name` | Human label as sold | Backoffice/OTA display without a catalogue lookup | Accepted source | Keep |
| `Status` | Lifecycle of the service itself | Cancel/void cascade, eligibility policies | Domain transitions | Keep |
| `CommercialStatus` | Sold / cancelled commercially | Commercial summary roll-up | Domain transitions | Keep |
| `FulfillmentStatus` | Reservation outcome | Reserve saga, release, re-reserve | Reservation adapter | Keep |
| `DeliveryStatus` | Whether the service was consumed | Used/unused on void and refund eligibility | Domain transitions | Keep |
| `FinancialStatus` | Priced / refunded state of the service | Refund cascade | Domain transitions | Keep |
| `DocumentStatus` | Documented / voided / cancelled | Issuance, void, re-issue guards | Issuance path | Keep |
| `DeliveryModel` | Per-passenger-segment, per-journey, per-order | How many instances are expected; fulfilment fan-out | Accepted source | Keep |
| `PriceTreatment` | **Commercial** treatment: separately priced / included / complimentary / supplier-opaque | Distinguishes "free" from "paid" without lying about money; drives whether a pricing line is expected | Accepted source | Keep (new) |
| `RequiresReservation` | Needs an inventory or supplier hold before it is deliverable | Reserve planning | Accepted source, or registered schema for generic services | Keep (renamed from `RequiresFulfillment`) |
| `RequiresSupplierConfirmation` | Needs an external confirmation that is not an inventory hold | Async ancillary confirmation | Accepted source | Keep |
| `RequiresDocument` | Needs a traffic document to be deliverable | Issuance planning | Accepted source, or registered schema | Keep |
| `DocumentKind` | Which document: ETKT / EMD / provider document | Chooses the document family at issuance; nullable and set only when a document is required | Accepted source, or registered schema | Keep (new) |
| `RequiresPaymentCoverage` | Must be covered by funds before delivery | Funding verification | Accepted source | Keep |
| `ProviderType` | Which provider family fulfils it | Adapter selection | Accepted source | Keep |
| `SupplierCode` | Third-party supplier identity | Non-airline ancillaries (hotel, ground transport) | Accepted source | Keep |
| `DeliveryProviderReference` | The provider's own reference for this service | Support, reconciliation, cancellation with the supplier | Provider | Keep |
| `HoldBatchId` | FlightFlow hold batch this service belongs to | Release / confirm / split remap | Inventory provider | Keep |
| `SeatHoldReference` | Seat-level hold reference | Release, split | Inventory provider | Keep |
| `TrafficDocumentId` / `DocumentCouponId` | Document evidence by identity | Void, refund, coupon status | Issuance path | Keep |
| `ElectronicTicketId` / `TicketCouponId` | Legacy ETKT evidence by identity | Current P1 issuance slice | Issuance path | Keep (P2-F consolidates document evidence) |
| `CreatedAt` | When the service was added to the Order | Ordering, audit | Clock | Keep |

### Removed from the service in P2-D

| Removed field | Why |
|---|---|
| `OrderAirTransportService` (whole entity) | Inheritance replaced by composition; every field moved to `OrderAirTransportServiceDetail` |
| `TravellerId` (single) | Wrong cardinality — a hotel room or a car serves several travellers. Replaced by `OrderServiceBeneficiary` |
| `OrderSegmentId` on the common service | Only air transportation is sold against a segment. Moved to the air detail; reached through `SoldSegmentId` |
| `FareBasis`, `Seat`, `Baggage`, `CabinBaggage` on the common service | Air-specific; moved to the air detail and explicitly marked transitional |
| `RequiresFulfillment` | Ambiguous — conflated inventory reservation with delivery. Renamed to `RequiresReservation` |

---

## 2. Membership and coverage

| Entity / field | Ordering meaning | Concrete use case | Authoritative input | Verdict |
|---|---|---|---|---|
| `OrderServiceBeneficiary.OrderTravellerId` | Which traveller receives the service | Per-passenger delivery, coupon mapping, split | Accepted source traveller ref | Keep |
| `OrderServiceCoveredService.CoveredOrderServiceId` | Which **air service** this service applies to | Baggage/meal/WiFi scoping, void cascade | Accepted source, stated only | Keep |
| `OrderServiceCoveredSegment.OrderSegmentId` | Which segment this service applies to when the source scopes by segment | Segment-scoped ancillaries | Accepted source, stated only | Keep |
| `OrderItemServiceLink.OrderItemId` | The item a service belonged to **when it was created** | Original commercial membership survives a later item move | Accepted source | Keep |
| `OrderItemServiceLink.LinkedByChangeId` | The `OrderChange` that created the membership | Audit — which commercial mutation put this service in this item | Domain change set | Keep |
| `OrderItemServiceLink.LinkedAt` | When | Audit ordering | Clock | Keep |

Coverage is **never inferred**. If the accepted source states no coverage, both coverage collections stay
empty. A covered-air-service reference that resolves to a non-air service is rejected (2784). There is no
generic coverage engine, no rule evaluation and no implicit "applies to the whole itinerary" default.

---

## 3. Typed details — one per service, exactly one

| Detail | Fields kept | Rejected / not modelled |
|---|---|---|
| `OrderAirTransportServiceDetail` | `OrderSegmentId`, `TransitionalFareBasis`, `RequestedSeat`, `TransitionalCheckedBaggage`, `TransitionalCabinBaggage` | fare family, refundability/changeability/upgradability (removed in P2-B.1 — item-level commercial terms own them) |
| `OrderSeatServiceDetail` | `AssociatedAirOrderServiceId`, `SoldSeatNumber` | seat characteristics, seat map, row/column decomposition — no Ordering use case |
| `OrderBaggageServiceDetail` | `Kind`, `Pieces`, `Weight`, `WeightUnit`, `PerPieceWeightLimit` | pooling rules, ATPCO baggage provisions, embargo text — not Ordering-owned |
| `OrderMealServiceDetail` | `MealCode`, `Quantity`, `SpecialMealCode` | dietary free text, catering supplier menus |
| `OrderLoungeServiceDetail` | `AirportId`, `LoungeCode`, `AccessStart`, `AccessEnd`, `GuestCount`, `RelatedAirOrderServiceId` | lounge amenities, terminal maps |
| `OrderHotelServiceDetail` | `PropertyReference`, `CheckIn`, `CheckOut`, `RoomCount`, `GuestCount`, `SupplierReference`, `RoomTypeCode`, `RatePlanReference` | address, star rating, cancellation-policy text — supplier-owned, not Ordering-owned |
| `OrderGroundTransportServiceDetail` | `PickupLocationReference`, `DropoffLocationReference`, `PickupAt`, `PassengerCount`, `VehicleTypeCode` | driver, plate, live tracking |
| `OrderGenericServiceDetail` | `SchemaName`, `SchemaVersion`, `AttributesJson` | a product catalogue, arbitrary key/value bags with no registered schema |

Every typed detail validates its own invariants in its constructor (reason codes 2829–2838). `AttachedDetailCount`
is the enforced "exactly one detail" invariant; attaching a second detail throws 2823.

### Why `AttributesJson` is acceptable here and not for fare construction

Generic detail attributes are **leaf display/fulfilment attributes of one registered schema** — they are never
joined, filtered or aggregated on, and the schema that governs them is registered in the domain
(`GenericServiceSchemaRegistry`) with required attributes and a fulfilment profile. Fare construction, by
contrast, is a **relational structure** that is traversed and resolved, so it stays relational (P2-C).

---

## 4. Fulfilment profile precedence

For a typed detail, the accepted source states `RequiresReservation` / `RequiresDocument` / `DocumentKind`.
For a **generic** detail the **registered schema decides** and the source's claim is ignored — a source cannot
declare that a Wi-Fi voucher needs an electronic ticket. Unregistered schema → 2826; unsupported version →
2827; missing or malformed required attributes → 2828; a schema bound to a different `OrderServiceType` than
the service declares → 2824.

Registered schemas: `Priority/1.0`, `WiFi/1.0`, `Cip/1.0`, `SimCard/1.0`, `ExtraSeat/1.0`,
`SpecialAssistance/1.0`.

---

## 5. Blocked service types (financial pseudo-services)

`Penalty`, `ServiceFee`, `Credit`, `Voucher`, `TaxAdjustment`, `ManualAdjustment`, `Notification`,
`TransferRide` cannot be created as services (2820). They are not things a passenger receives:

| Blocked type | Where it actually lives |
|---|---|
| Penalty, ServiceFee, TaxAdjustment, ManualAdjustment, Credit | `OrderPricingLine` with the matching `PricingComponentType` |
| Voucher | A tender / payment instrument, not a sold service |
| Notification | An operational side effect, not a sold service |
| TransferRide | Superseded by the first-class `GroundTransport` type with a typed detail |

The enum members remain (enum placement is closed and wire-compatibility matters); only *selling* them is
blocked.

---

## 6. Deferred (explicitly not done in P2-D)

| Item | Phase |
|---|---|
| Adding a service to an existing Order after creation | P2-E `AddProduct` |
| EMD issuance for `RequiresDocument` ancillaries | P2-F |
| Consolidating `ElectronicTicketId`/`TicketCouponId` into `TrafficDocumentId`/`DocumentCouponId` | P2-F |
| Ancillary reservation against a supplier (only air reservation is wired) | P2-E/P3 |
| Bundles as a first-class construct — a bundle is currently N services sharing one item and one `Included` price treatment | later, and only with a real use case |
