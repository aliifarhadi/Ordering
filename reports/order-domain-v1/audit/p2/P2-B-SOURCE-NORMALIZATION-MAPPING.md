# P2-B — Source Normalization Mapping Audit

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
**Written before implementation**, as required by the P2-B brief §7.

Purpose: classify every meaningful field of the current AirPrice-shaped creation source so nothing is
silently dropped when provider vocabulary stops at the ACL boundary.

Classification legend: `NORMALIZED` (carried into the Ordering-owned accepted source),
`PRODUCT_SNAPSHOT`, `COMMERCIAL_TERMS_SNAPSHOT`, `PRICING_LINE`, `SERVICE_DETAIL_EXISTING`
(already carried by an existing P1 service/segment structure), `DEFER_P2_C`, `DEFER_P2_D`, `NOT_APPLICABLE`.

---

## 1. `OfferDetail` root

| Source field | Classification | Target / reason |
|---|---|---|
| `OfferId` | NORMALIZED | `AcceptedOrderSource.SourceOfferId`; also the `PriceChangeSet.SourceOfferId` and the source-line identity prefix. |
| `CurrencyId` | NORMALIZED | `AcceptedOrderSource.SaleCurrencyId` → `Order.CurrencyId`. Sale currency of the accepted offer. |
| `LastTicketingDate` | NORMALIZED | `AcceptedOrderSource.TicketingDeadline` → `Order.TimeToLive`. Provider field name does not reach the Domain (§19). |
| `Travellers[]` | NORMALIZED | `AcceptedSourceTraveller` correlation records. |
| `Bounds[]` | NORMALIZED | `AcceptedJourney` + `AcceptedSegment`. |
| `FareComponents[]` | NORMALIZED (partly) / DEFER_P2_C | The commercially meaningful fields become product/terms/service facts (below). The *fare construction structure itself* — pricing units, component grouping, true-RT vs OW+OW — is **DEFER_P2_C**; P2-B must not invent it. |
| `PriceLines[]` | PRICING_LINE | Traveller-scoped accepted pricing lines. |
| `OrderCharges[]` | PRICING_LINE | Order-scoped accepted pricing lines (`BasisType = Order`, `ApplicationLevel = PerOrder`). |
| `Charges[]` | PRICING_LINE | Charge classification, code, name, refundability — resolved **at the ACL** into `ComponentType` + `Code`/`Description`/`Refundability`. |
| `Rates[]` | PRICING_LINE | Source-applied conversion provenance → the existing `ExchangeRate` value object on the pricing line. |

## 2. `OfferTraveller`

| Source field | Classification | Target / reason |
|---|---|---|
| `TravellerRef` | NORMALIZED | Source-local correlation key only (`TravelerRef`); never an Ordering identity (§6). |
| `TravellerIndex` | NORMALIZED | Correlates the accepted source to the caller-supplied traveller list. |
| `PassengerTypeCode` | NOT_APPLICABLE (for creation) | The authoritative passenger type for the Order comes from `CreateOrderArgs.Travellers[].PassengerType` (caller context), which is what `OrderTraveller` already stores. The offer copy is redundant; carried as correlation evidence only, not used to overwrite the caller value. **P2-B.1:** that Domain field is now the Ordering-owned `PassengerTypeCode`; any AirPrice-sourced value is translated at the ACL and fails closed on an unmapped code. |

## 3. `OfferBound` → `AcceptedJourney`

| Source field | Classification | Target / reason |
|---|---|---|
| `BoundId` | NORMALIZED | `JourneyRef` source-local key; also retained as `OrderItinerary.BoundId` (existing P1 field). |
| `Sequence` | NORMALIZED | `AcceptedJourney.Sequence` → `OrderItinerary.Sequence`. |
| `OriginAirportId` / `DestinationAirportId` | NORMALIZED | `OrderItinerary` origin/destination. |
| `Flights[]` | NORMALIZED | `AcceptedSegment[]`. |
| *(direction)* | NORMALIZED | Outbound/Inbound is derived **at the ACL** from bound sequence (existing P1 rule: single bound or index 0 ⇒ Outbound). The Domain no longer performs this provider-shape inference. |

## 4. `OfferFlight` → `AcceptedSegment`

All of these are sold-schedule snapshot evidence (§20), carried by the existing `OrderSegment`:

| Source field | Classification | Target / reason |
|---|---|---|
| `FlightId` | SERVICE_DETAIL_EXISTING | `AcceptedSegment.FlightSourceId` → `OrderSegment.FlightId`. Provider reference, not Ordering identity. |
| `FlightVersion` | SERVICE_DETAIL_EXISTING | `OrderSegment.FlightVersion`. |
| `Number` | SERVICE_DETAIL_EXISTING | `OrderSegment.Number`. |
| `OriginAirportId`, `OriginAirportTerminalId` | SERVICE_DETAIL_EXISTING | `OrderSegment` origin. |
| `DestinationAirportId`, `DestinationAirportTerminalId` | SERVICE_DETAIL_EXISTING | `OrderSegment` destination. |
| `OperatingAirlineId` | SERVICE_DETAIL_EXISTING | `OrderSegment.OperatingAirlineId`. Kept distinct from marketing and from `OwnerAirlineId` (§21). |
| `MarketingAirlineId` | SERVICE_DETAIL_EXISTING | `OrderSegment.MarketingAirlineId`. |
| `DepartureDateTime` / `ArrivalDateTime` | SERVICE_DETAIL_EXISTING | Sold schedule snapshot. |
| `Duration` | SERVICE_DETAIL_EXISTING | `OrderSegment.Duration`. |
| `AircraftId` | SERVICE_DETAIL_EXISTING | `OrderSegment.AircraftId`. **Behaviour preserved as-is:** the existing P1 code coerces a missing aircraft to `0`. This is pre-existing P1 behaviour on a non-commercial descriptive field; it is recorded here rather than changed, because altering an existing entity field's nullability is outside P2-B scope. |
| `CabinClassId`, `RbdId`, `BookingClass` | SERVICE_DETAIL_EXISTING | `OrderSegment` cabin/RBD/booking-class snapshot. |
| `FlightCapacityId` | SERVICE_DETAIL_EXISTING | `OrderSegment.FlightCapacityId` — the reservation/capacity reference P1 reserve depends on. |
| `Legs[]` | SERVICE_DETAIL_EXISTING | `OrderSegmentLeg` (a sold segment may bind several physical legs). |

### `OfferFlightLeg` / `OfferFlightStop`

| Source field | Classification | Target / reason |
|---|---|---|
| `LegId`, `Sequence`, origin/destination, terminals, departure/arrival | SERVICE_DETAIL_EXISTING | `OrderSegmentLeg`. |
| `Stop.DurationMinutes`, `Stop.StopType`, `Stop.PassengersCanBoardOrLeave` | NOT_APPLICABLE (unchanged) | The existing P1 creation path already passes `null` for leg stop type and stop duration; `OfferFlightStop` was mapped from the wire but never consumed. P2-B does not begin consuming it — that would be new behaviour, not normalization. The AirPrice `StopType` vocabulary therefore leaves the Domain entirely with the provider model. Recorded as a known unconsumed source fact. |

## 5. `OfferFareComponent`

| Source field | Classification | Target / reason |
|---|---|---|
| `AirFareId` | PRODUCT_SNAPSHOT (provenance only) + SERVICE_DETAIL_EXISTING | **Corrected in P2-B.1.** It is an opaque external identifier, carried as `ProductSnapshot.SourceProductReference` / `SourcePricingReference` provenance. It is **not** an Ordering `ProductCode`. It also remains on `OrderAirTransportService.AirFareId` and `OrderSegment.AirFareId` as today. |
| `FareBasis` | SERVICE_DETAIL_EXISTING (transitional) + DEFER_P2_C | **Corrected in P2-B.1.** FareBasis is fare-construction context, **not** an Ordering `ProductName`, and is no longer written to any product name field. It survives only as a transitional compatibility field on the legacy `OrderAirTransportService` because P1 ETKT issuance still reads `service.FareBasis`. Authoritative ownership moves to `FareComponent` in **P2-C**, after which issuance must obtain issue-time fare context from the fare-construction association rather than the legacy service field. |
| `FareFamily` | PRODUCT_SNAPSHOT (brand label) + SERVICE_DETAIL_EXISTING | **Corrected in P2-B.1.** Mapped to `ProductSnapshot.BrandName` **only because the ACL explicitly treats this source value as the customer-visible fare brand/family label**, and only when the source actually supplies one. No `BrandCode` is invented from it, and the source FareFamily *structure* is never mirrored as an Ordering structure. Fare-family benefits become real sold/included `OrderService`s in **P2-D**, not a generic feature mirror. |
| `BookingClass` | SERVICE_DETAIL_EXISTING | `OrderSegment.BookingClassCode`. |
| `IsRefundable` | COMMERCIAL_TERMS_SNAPSHOT (ACL input only) | **Corrected in P2-B.1.** This provider-side coarse boolean is an **ACL input**, not the persisted schema. The ACL translates it into the Ordering-owned `RefundabilitySummary` (`Permitted` / `Prohibited`, and `Unknown` when the source supplies nothing). A richer future Pricing/FareFamily contract maps into `Conditional` without changing the Ordering schema. It also still sets the pricing line's `Refundability`. |
| `IsChangeable` | COMMERCIAL_TERMS_SNAPSHOT (ACL input only) | Same treatment → `ChangeabilitySummary`. |
| `IsUpgradable` | COMMERCIAL_TERMS_SNAPSHOT (ACL input only) | Same treatment → `UpgradeEligibilitySummary`. |
| `BaggagePieces`, `BaggageWeight`, `BaggageUnit` | SERVICE_DETAIL_EXISTING (transitional) + DEFER_P2_D | **Corrected in P2-B.1.** Baggage is **not** a change/refund commercial-policy field and was removed from `CommercialTermsSnapshot`; it is no longer persisted in two places. Only the minimum P1-compatible representation on `OrderAirTransportService` remains, explicitly transitional/non-normative. The final baggage service/product semantics — included allowance versus separately priced — are **P2-D**. Unit parsing happens at the ACL and fails closed; see §7. |
| `CabinBaggagePieces`, `CabinBaggageWeight`, `CabinBaggageUnit` | SERVICE_DETAIL_EXISTING (transitional) + DEFER_P2_D | Same treatment. |
| `BoundId` | NORMALIZED | Correlation only. |

## 6. `OfferPriceLine` → accepted pricing line

| Source field | Classification | Target / reason |
|---|---|---|
| `TravellerRef` | PRICING_LINE | Source-line identity component + product/service correlation. |
| `IsBase` | PRICING_LINE | Selects `ComponentType = Fare` vs a charge classification. Decided **at the ACL**. |
| `AirFareId` | PRICING_LINE | Source-line identity component; product correlation. |
| `AirChargeId` | PRICING_LINE | Source-line identity component; resolves the `OfferCharge`. |
| `Code` | PRICING_LINE | `PricingLine.Code` fallback and identity component. |
| `BoundId` | PRICING_LINE | Identity component. |
| `FlightId` | PRICING_LINE | Chooses `BasisType = OrderService` (per-segment) vs `OrderItem`; identity component. |
| `Amount` | PRICING_LINE | `OriginalAmount` (source currency magnitude). |
| `CurrencyId` | PRICING_LINE | `OriginalCurrencyId`, resolved via the source rate when the line omits it. |
| `EquivalentAmount` | PRICING_LINE | `SaleAmount` (order sale currency). |
| `EquivalentCurrencyId` | PRICING_LINE | `SaleCurrencyId`, resolved via the source rate when omitted. |
| `RateOfExchangePeriodId` | PRICING_LINE | Resolves the `OfferRate` → `ExchangeRate` provenance, preserved verbatim. |

## 7. `OfferCharge`

| Source field | Classification | Target / reason |
|---|---|---|
| `AirChargeId` | PRICING_LINE | Source-line identity component. |
| `Kind` | PRICING_LINE | **The one classification decision that moves to the ACL.** `Tax → Tax`, `Surcharge → CarrierSurcharge`, `Fee → Fee` — the closed mapping for the current coarse contract. Any other/unknown value **fails closed** at the boundary; there is no `_ => Fee`. |
| `Code` | PRICING_LINE | `PricingLine.Code`. |
| `Name` | PRICING_LINE | `PricingLine.Description`. |
| `IsRefundable` | PRICING_LINE | `PricingLine.Refundability`. |

## 8. `OfferRate`

| Source field | Classification | Target / reason |
|---|---|---|
| `RateOfExchangePeriodId` | PRICING_LINE | `ExchangeRate.RateOfExchangeId`. |
| `FromCurrencyId` / `ToCurrencyId` | PRICING_LINE | Resolve the line's original/sale currency when the line omits them. |
| `Rate` | PRICING_LINE | `ExchangeRate.RateOfExchange` at `decimal(28,12)`. |
| `DecimalPlaces` | PRICING_LINE | `ExchangeRate.NumberOfDecimalPlaces`. |
| *(rounding factor)* | NOT_APPLICABLE | The source supplies none; the existing code passes `0`. Unchanged, and explicitly **not** invented — this remains the P0-recorded `RoundingFactor` debt. |

---

> **P2-B.1 correction notice.** This audit originally implied that some source fields dictate the persisted
> Ordering schema. They do not. Provider fields are ACL *inputs*; the Ordering schema holds Ordering-owned
> semantics. The rows above have been corrected for `AirFareId`, `FareBasis`, `FareFamily`, the three
> refund/change/upgrade booleans and baggage. See
> [`P2-B.1-DOMAIN-SEMANTIC-AUDIT.md`](P2-B.1-DOMAIN-SEMANTIC-AUDIT.md) for the field-level keep/remove decisions.

## 9. Silent defaults found and their disposition (§11)

| Current default | Verdict | P2-B disposition |
|---|---|---|
| `ParseWeightUnit(unit) → WeightUnit.Kg` on unparseable/missing unit | **Not acceptable** | Moves to the ACL and **fails closed**: when a baggage allowance is actually supplied (pieces or weight > 0) an unrecognised or missing unit is rejected. No allowance supplied ⇒ no baggage object, which is absence, not a defaulted unit. |
| `ComponentTypeOf(kind) → _ => Fee` | **Not acceptable** | Moves to the ACL and **fails closed** on an unknown charge kind. |
| `fareComponent?.IsRefundable ?? false` etc. | **Acceptable, narrowed** | These are absence-of-fare-component fallbacks. The ACL now requires a fare component for an accepted air product and rejects when it cannot be resolved, so the coalesce is no longer load-bearing; the values come from the resolved component. |
| `fareComponent?.AirFareId ?? 0` on the segment | **Acceptable (pre-existing)** | Retained: `OrderSegment.AirFareId` is non-nullable in the P1 schema. The ACL resolves a real fare id for the accepted product path, so `0` is not reachable there. Changing the column's nullability is out of P2-B scope. |
| `(int)(flight.AircraftId ?? 0)` | **Acceptable (pre-existing)** | Descriptive, non-commercial. Recorded above; unchanged. |
| `line.CurrencyId ?? rate.FromCurrencyId ?? offer.CurrencyId` | **Acceptable** | This is source-supplied resolution, not invention: the offer's own currency is an authoritative source fact and the precedence is explicit. Moves to the ACL unchanged. |

## 10. Deferred, with reason

| Item | Deferred to | Reason |
|---|---|---|
| Pricing unit / fare component structure, true-RT vs OW+OW | **DEFER_P2_C** | `AirFareConstruction` is explicitly out of P2-B scope; inventing structure from bound shape is forbidden (INV-P13/INV-F01). |
| Per-product ancillary types, service coverage, beneficiaries, composition over inheritance | **DEFER_P2_D** | The service/item model rework is P2-D. P2-B keeps the existing `OrderAirTransportService`. |
| `OfferFlightStop` consumption | **DEFER_P2_D** | Never consumed by P1; beginning to consume it is new behaviour, not normalization. |
| `PassengerTypeCode` from the offer | NOT_APPLICABLE | Caller context is authoritative for traveller identity today; the offer value is correlation evidence only. |

## 11. Nothing dropped

Every field in the current `OfferDetail` graph appears in exactly one row above. The two source facts not
carried into the Order (`OfferFlightStop.*`, offer-side `PassengerTypeCode`) are recorded with their reason
and remain available on the provider side; they are not deleted from the provider model.
