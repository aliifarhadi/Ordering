# P2-D — OrderService Composition & Practical Ancillary Domain

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline entering P2-D: **380 passed / 0 failed**. P0 – P2-C.1 frozen. **P2-E and P2-F not started.**

Evidence: [`audit/p2/P2-D-TEST-RUN.txt`](audit/p2/P2-D-TEST-RUN.txt) ·
Binding audit: [`audit/p2/P2-D-SERVICE-SEMANTIC-AUDIT.md`](audit/p2/P2-D-SERVICE-SEMANTIC-AUDIT.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **322 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **194 passed**, 0 failed (real SQL Server) |
| Total | **516 passed, 0 failed** (baseline 380 → +136, zero regressions) |
| New P2-D tests | 120 domain + 16 persistence = **136** |

## 2. What changed

`OrderService` was an **abstract** base with a single EF-TPT subclass `OrderAirTransportService`. Every new
ancillary would have meant a new subclass and a new table, and everything air-specific — traveller, segment,
fare basis, seat, baggage — sat on the only subclass that existed, so ancillaries either inherited fields that
made no sense or were forced through the air shape.

P2-D replaces inheritance with **composition**:

```
OrderService (concrete, one table)
  ├── beneficiaries      OrderServiceBeneficiary[]        who receives it
  ├── coverage           OrderServiceCoveredService[]     which air services it applies to
  │                      OrderServiceCoveredSegment[]     which segments it applies to
  └── exactly one typed detail
        OrderAirTransportServiceDetail | OrderSeatServiceDetail | OrderBaggageServiceDetail
      | OrderMealServiceDetail | OrderLoungeServiceDetail | OrderHotelServiceDetail
      | OrderGroundTransportServiceDetail | OrderGenericServiceDetail
```

There is no domain inheritance and no EF inheritance mapping — `UseTptMappingStrategy()` is gone and no type
derives from `OrderService` anywhere in the Domain assembly (asserted by test). `AttachedDetailCount` enforces
exactly one detail; a second attachment throws 2823.

Air transportation is now just "the common service plus an air detail". `service.SoldSegmentId` and
`service.SoleBeneficiaryId` are the only ways to reach segment and traveller, and `SoleBeneficiaryId` **fails
closed** (2822) on a service that legitimately has several beneficiaries rather than silently returning the
first.

## 3. Beneficiaries, coverage, and membership

- **Beneficiaries** replace the single `TravellerId`. Air and seat services must have exactly one (2822);
  a hotel room or a car may have several. A repeated traveller reference is recorded once.
- **Coverage** is stated, never inferred. Nothing derives "this bag covers the whole itinerary". A covered-air
  reference that resolves to a non-air service is rejected (2784), as is any unresolvable reference.
- **`OrderItemServiceLink`** records the item a service belonged to *when it was created*, plus the
  `OrderChange` that created the link. `OrderService.OrderItemId` remains the **current** item; the link table
  is the immutable original membership, so a later item move cannot erase commercial history.

## 4. Price treatment is not a financial status

`ServicePriceTreatment` (`SeparatelyPriced` / `Included` / `Complimentary` / `SupplierOpaque`) records the
**commercial** treatment of a service. An included checked bag is a real service with a real detail, real
beneficiaries and real coverage — it simply has no pricing line of its own. `FinancialStatus` still means what
it meant (priced / refunded) and is untouched by price treatment. Nothing in P2-D invents a zero-amount pricing
line to make a free service look sold.

## 5. Typed ancillary details

Seat, baggage, meal, lounge, hotel and ground transport each get a small detail entity that models only what
Ordering owns and uses — see the binding audit for the field-by-field justification and for what was rejected.
Each validates its own invariants at construction: baggage quantity/unit (2829, 2830), meal quantity (2831),
lounge airport / guests / access window (2832–2834), hotel window / rooms / guests (2835–2837), ground
transport passengers (2838).

`GroundTransport` is a first-class type with a typed detail; the retired `TransferRide` is blocked from sale.

## 6. Registered generic services

Products that do not deserve a table — priority boarding, Wi-Fi, CIP, SIM card, extra seat, special assistance —
use `OrderGenericServiceDetail` (`SchemaName`, `SchemaVersion`, `AttributesJson`) governed by
`GenericServiceSchemaRegistry`. This is **not** an open key/value bag:

- unregistered schema → **2826**; unsupported version → **2827**
- missing / null / non-object / malformed required attributes → **2828**
- schema bound to a different `OrderServiceType` than the service declares → **2824**
- the **registered schema supplies the fulfilment profile**, and a source claiming otherwise is ignored — a
  Wi-Fi voucher cannot declare that it needs an electronic ticket

`ExtraSeat/1.0` (CBBG and comfort seats) requires `capacityQuantity` and `reason`, requires a reservation and
requires an **EMD** — and it never becomes a traveller.

## 7. Financial pseudo-services blocked

`Penalty`, `ServiceFee`, `Credit`, `Voucher`, `TaxAdjustment`, `ManualAdjustment`, `Notification` and
`TransferRide` cannot be sold as services (**2820**). Money movements belong to `OrderPricingLine`; a voucher is
a tender; a notification is a side effect. The enum members stay — enum placement is closed and the wire
contract is shared — only *selling* them is blocked.

## 8. Legacy air transition (§20)

The old air subclass carried `Baggage` / `CabinBaggage` value objects populated from AirPrice. These were
**not** turned into baggage services (that would fabricate commercial facts the source never sold) and **not**
dropped (that would lose data). They are preserved on the air detail as
`TransitionalCheckedBaggage` / `TransitionalCabinBaggage`, named for what they are, alongside
`TransitionalFareBasis` which P2-C already established as the fallback for issue-time fare basis. A test proves
transitional baggage evidence never materialises a `BaggageAllowance` service.

## 9. Persistence

Migration **`P2DServiceComposition`** (new; no previously applied migration was edited) adds the common-service
columns, eleven child tables (eight typed details, two coverage tables, beneficiaries) and the item-service
link table, then **backfills**:

1. one `OrderAirTransportServiceDetail` row per legacy `OrderAirTransportServices` row, carrying segment, fare
   basis, seat and both baggage value objects;
2. one `OrderServiceBeneficiary` row per legacy service from its old single `TravellerId`;
3. one `OrderItemServiceLink` row per service, attributed to the order's first `OrderChange`.

Only after the backfill does `Up` drop `Order.OrderAirTransportServices` — the scaffolded migration dropped it
first and was hand-corrected. `PriceTreatment`'s scaffolded default of `0` was corrected to `1`
(`SeparatelyPriced`), since `0` is not a valid member. There are no polymorphic or nullable "fake" FKs: every
detail table has a real FK to `OrderServices`, and coverage is two explicit tables rather than one
type-discriminated column.

`OrderRepository` includes all beneficiaries, both coverage collections, all eight details and the link table.
The `OrderDetails` projection exposes service type/code/name, all statuses, `PriceTreatment`, current and
original item membership, beneficiaries, a fulfilment block, a coverage block and a discriminated detail block.

## 10. Reserve and issue

`ReserveEligibilityPolicy`, `IssueEligibilityPolicy`, `FulfillmentPlanner`, `OrderIssuanceService`,
`AirlineReserveInventoryAdapter`, `UpdateLastTicketingDateCommandHandler` and `OrderSplitService` were rewired
from `OfType<OrderAirTransportService>()` to `IsAirTransport` + `SoldSegmentId` + `SoleBeneficiaryId`.
`OrderService.CopyTo` carries beneficiaries and the air detail (including transitional baggage) through a
split. A persistence test proves an order carrying meal and baggage services still reserves and issues, with
the air services documented and the ancillaries left undocumented because they require no document.

## 11. Not done

Adding a service to an existing order (**P2-E `AddProduct`**), EMD issuance for ancillaries that require a
document (**P2-F**), consolidation of `ElectronicTicketId`/`TicketCouponId` into the traffic-document fields
(P2-F), supplier-side ancillary reservation, and bundles as a first-class construct. No sibling repository was
modified. No `Money`, `CurrencyCode`, FX/ROE/rounding framework, CQRS/outbox/inbox/ID/clock/lock abstraction or
parallel `ServiceV2` model was created. Enum placement was not reopened.
