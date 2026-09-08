# P2-H — Architecture & Semantic Audit (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09
**Entry commit:** `6fc7d22` (P2-G.1), clean tree · **Production code changed by this audit:** none.

Source-level audit of the active P2 production code. Every search below was run against `src/` (and
`Contracts/` where stated), excluding `obj/`. A textual hit is classified by actual semantics, not by
pattern match.

---

## 1. Bounded-context boundary (§5)

`AeroTech.Ordering.Domain.csproj` references exactly two projects:

```
Framework/AeroTech.Framework.Core
Contracts/AeroTech.Messages
```

Complete list of non-`System`, non-Ordering `using` directives in the whole Domain project:

| Namespace | Verdict |
|---|---|
| `AeroTech.Framework.Core.Domain.*`, `.ServiceContracts` | Platform framework — allowed |
| `AeroTech.Messages.Ordering.Enums` | Ordering-owned — allowed (§28 convention) |
| `AeroTech.Messages.Shared.Enums` | Platform-shared — allowed |
| `AeroTech.Messages.Aegis`, `.Aegis.Enums` | Trusted-context vocabulary (P0) — allowed |
| `AeroTech.Messages.FlightFlow.Enums` | **Finding F-1**, see §9 |

**`AeroTech.Messages.AirPrice.*` in Domain: zero occurrences.** Every AirPrice reference in the solution is
confined to the ACL:

```
src/AeroTech.Ordering.Providers/Offer/Model/OfferCharge.cs
src/AeroTech.Ordering.Providers/Offer/Model/OfferFlightStop.cs
src/AeroTech.Ordering.Providers/Offer/Services/AirPriceOfferNormalizer.cs
src/AeroTech.Ordering.Providers/Offer/Services/OfferResponseMapper.cs
src/AeroTech.Ordering.Providers/Offer/Wire/FlightOfferDetailResponse.cs
```

No `Offer`, `FareFamily`, `AirFare`, `Bound` or `Flight` aggregate exists in the Ordering Domain — the
Domain's air-side model is `OrderSegment` / `OrderSegmentLeg` / `OrderService` / `OrderAirFareConstruction`,
all Ordering-owned. `No_fare_family_model_is_introduced` and `No_fare_construction_type_references_provider_or_airprice_vocabulary`
assert this.

## 2. Semantic coercion (§6)

Every write site of the three fields is the accepted-source contract or the entity that stores it. The only
place the AirPrice ACL constructs them is `Providers/Offer/Services/AcceptedProductBuilder.cs`:

```csharp
new AcceptedProductSnapshot(
    ProductType.AirFare,
    _airFareId.ToString(),                 // <- product identity, the one matching target
    AirPriceOfferNormalizer.SourceSystem,
    _offer.OfferId,
    ProductCode: null, ProductName: null, BrandCode: null,
    BrandName: BrandNameOf(_fareComponent?.FareFamily),
    MarketingAirlineId: Unambiguous(...), OperatingAirlineId: Unambiguous(...),
    SupplierCode: null,
    SourcePricingReference: null),         // <- explicitly not AirFareId
new AcceptedCommercialTerms(
    TermStateOf(_fareComponent?.IsRefundable),
    TermStateOf(_fareComponent?.IsChangeable),
    TermStateOf(_fareComponent?.IsUpgradable),
    AirPriceOfferNormalizer.SourceSystem,
    SourcePolicyReference: null),          // <- explicitly not AirFareId
```

| Target field | Populated from | Verdict |
|---|---|---|
| `SourcePricingReference` (product snapshot) | **null** | No coercion |
| `SourcePricingReference` (fare construction) | never set by the ACL — no construction is produced | No coercion |
| `SourcePolicyReference` | **null** | No coercion |
| `SourcePolicyVersion` | never passed — defaults null | No coercion |
| `ProductIdentifier` | `AirFareId.ToString()` | Correct: the source's identity for that product |
| `BrandName` | fare-family label, trimmed, **null when absent** | Real evidence only |
| `BrandCode` / `ProductCode` / `ProductName` / `SupplierCode` | null | Not fabricated |
| `MarketingAirlineId` / `OperatingAirlineId` | only when unambiguous, else null | Not fabricated |
| refundable / changeable / upgradable | `true → Permitted`, `false → Prohibited`, `null → Unknown` | Absence stays absence |

One source identifier is used once, for the one field whose meaning matches. Asserted by
`The_airprice_adapter_populates_no_false_policy_or_pricing_reference`,
`The_fare_identifier_is_carried_as_provenance_and_never_as_a_product_code`,
`A_fare_family_label_becomes_a_brand_name_only_when_the_source_supplies_one` and
`Absent_source_evidence_becomes_unknown_rather_than_a_permission`.

## 3. Fare construction inference (§7)

`AirPriceOfferNormalizer` returns `AcceptedOrderSource(...)` **without** the optional
`FareConstructions` argument, so the current AirPrice source yields `FareConstructions = null` and the Order
persists none. No pricing group, pricing unit, unit type, combination method or component grouping is derived
from itinerary geometry anywhere — `grep -riE "openjaw|isroundtrip|infer"` over `src/` returns **zero hits**.

Required scenarios, all covered:

| Scenario | Test |
|---|---|
| current AirPrice ⇒ constructions empty | `The_current_airprice_source_fabricates_no_fare_construction`, `An_order_without_fare_construction_is_valid`, `An_order_without_a_construction_persists_none` |
| explicit true RT | `An_explicit_true_round_trip_construction_persists_as_accepted` |
| OW + OW distinguishable from RT | `The_same_itinerary_priced_as_two_one_ways_stays_structurally_distinct_from_a_true_round_trip` |
| explicit OpenJaw | `An_open_jaw_construction_is_stored_when_the_source_declares_it` |
| through fare, multi service/segment | `A_through_fare_component_covers_several_sold_services_and_segments`, `A_through_fare_component_covering_two_services_survives_a_reload` |
| fare break ⇒ multiple components | `A_fare_break_produces_separate_components_with_separate_service_scopes` |
| never inferred | `A_construction_type_is_never_inferred_from_the_itinerary`, `A_pricing_unit_type_and_combination_method_are_never_inferred`, `The_airprice_normalizer_infers_no_pricing_units` |

`FareBasis` is never parsed for meaning: `grep FareBasis` filtered for `Substring|StartsWith|EndsWith|Split|Regex|ToCharArray`
returns **zero hits**; asserted by `A_fare_basis_is_stored_as_opaque_context_and_is_never_parsed`.

## 4. Money (§ frozen rules, §34)

`grep -rn "class Money|record Money|struct Money|class CurrencyCode|record CurrencyCode|class Rounding|RoundingPolicy|MidpointRounding|Math.Round"` over `src/` returns **zero hits**.
No FX/ROE engine was introduced. `RateOfExchange` / `RateOfExchangeId` are the pre-existing platform
`ExchangeRate` value object at `decimal(28,12)` carrying AirPrice-owned conversion provenance —
`A_reversal_may_not_replace_the_historical_rate_with_todays_rate` and
`Source_applied_conversion_provenance_is_preserved` guard it.

## 5. Pricing foundation (§8)

All frozen invariants remain enforced and asserted (`PricingFoundationTests`, `PricingCorrectnessTests`):
Debit/Credit polarity, `CustomerBalance` / `SettlementOnly` / `Informational` effects, non-negative
magnitudes, commission never charged to the customer, tax never settlement-only, reversal provenance and
joint caps, source reference vs occurrence identity, allocation ownership, and Complete / Partial /
Unavailable reconciliation. `Allocations_are_never_added_to_the_customer_total` and
`The_persisted_total_is_a_cache_of_the_ledger` confirm `Order.CustomerTotal` stays derived from
customer-balance pricing lines and that allocations contribute nothing independently.

## 6. Versions, events and atomicity (§9, §13, §14, §15)

`CommercialVersion` +1 once per accepted commercial mutation and never for reservation, funding, document
issuance or projection (`CommercialVersionTests`, `CommercialVersionReplayTests`); `FinancialSequence` +1
per committed `PriceChangeSet`; `ObligationVersion` only on customer-total movement; ordinals restart at 1
per mutation with siblings sharing the final version (`EventOrdinalTests`,
`Sibling_events_share_the_commercial_version_and_use_distinct_ordinals`). Exactly one
`OrderPricingChanged` per committed set, never for reserve, funding, ET issue, EMD issue, recovery,
projection refresh, replay, rejection or failed quote (`OrderPricingChangedOutboxTests`,
`Reservation_and_issuance_write_no_pricing_message`, `Document_issuance_adds_no_order_item_service_or_fare_construction`).

The **only** explicit transaction in `src/` is inside `OrderingUnitOfWork.SaveChangesAsync`; it is opened and
committed within that method, so no provider call can occur inside it. Command state, read model and outbox
commit together (`LocalTransactionAtomicityTests`, `SharedTransactionTests`).

## 7. OrderView and local read (§16 – §20)

`GetOrderDetailsQuery : IRequest<OrderView?>`. `grep "JsonElement|dynamic| object "` over
`Query/OrderAggregate/View/` and `Query/OrderAggregate/Queries/` returns **zero hits**. `AttributesJson`
appears only in Domain (accepted source, entity, schema validation) and Persistence (EF configuration,
migrations) — never in Query, Synchronizer or RestApi; asserted by
`A_generic_service_shows_schema_identity_without_raw_attributes`. `Retrieval_calls_no_upstream_provider`,
`An_ota_read_touches_no_upstream_provider` and `Get_order_redisplays_from_the_local_projection_without_upstream_calls`
cover the local-read guarantee for both GET routes. `Issuance_advances_the_projection_but_not_the_commercial_version`
keeps `ProjectionRevision` and `CommercialVersion` distinct, and no `ExternalOrderVersion` /
`NdcOrderVersion` field exists.

## 8. OTA ownership, payments and ledger (§21, §26, §27)

`grep -rn "CurrentCustomerId"` over `src/` returns **zero hits** — the `?? 0` defaulting is gone from the
whole solution, and the ownership seam is `IOrderCustomerAccessGuard` over
`IOrderRepository.FindCustomerIdAsync` (`AsNoTracking` projection of `Order.CustomerId`, no external call).
All 17 P2-G.1 tests re-run green, plus the 4 new creation fail-closed tests.

`grep -rin "ledger"` over `src/` returns **zero hits** — `LedgerFlow` exists only as unreferenced message
contracts under `Contracts/AeroTech.Messages`. Likewise JetPay. **No Ledger call exists in any Ordering
source file.**

The active P2 path (`Services/Creation`, `Reservation`, `Issuance`, `OrderChange`, `Access`) contains **zero**
references to `Payment`, `PaymentAggregate` or `IPaymentProvider`.

## 9. Findings and classification (§39)

| # | Finding | Class | Action |
|---|---|---|---|
| **F-1** | `OrderSegmentLeg.StopType` persists `FlightStopType` from `AeroTech.Messages.FlightFlow.Enums` — a sibling-owned enum on an Ordering entity. Also used by `Domain/Providers/FlightFlow/FlightHeldSeatsResult` and `Domain/Providers/Pricing/AirFareBoundReservationValidationRequest`. | **Legacy pre-existing debt** — `git log --diff-filter=A` dates all four files to `e133140` (first commit), before P0. It is the documented `Domain → Contracts` enum inversion (CLAUDE.md, `DomainAuditRemediationPlan.md` P3). Not introduced or widened by P2; not on the P2 pricing/commercial path. | **Deferred.** Fixing it means an Ordering-owned stop-type enum plus a migration — a new domain concept, which §1 forbids in P2-H. |
| **F-2** | `Domain/Providers/{FlightFlow,Pricing,Payment}` hold legacy ports outside the P2 `Domain/Ports/<Area>` convention. | **Legacy pre-existing debt** (first commit). Consumed only by P1-era paths: FulfillmentTask adapters, `OrderSplitService`, `UpdateLastTicketingDateCommandHandler`, `PaymentService`. The five P2 ports all live under `Domain/Ports/`. | **Deferred.** Relocation is a refactor of working architecture. |
| **F-3** | The `Payment` aggregate, `PaymentService`, `PayOrderCommandHandler` and `POST Internal/v1/Orders/{id}/Payments` still exist. | **Legacy pre-existing debt** (first commit). Reachable only through the Internal maintenance channel; **not** on the active P2 target path and no P2 functionality depends on it. | **Deferred**, per §26 ("record as legacy debt, do not refactor it in P2-H"). |
| **F-4** | `PingController` route is `api/v{version}/[controller]` — lowercase prefix and token-based, unlike the PascalCase Order routes. | **Cosmetic pre-existing debt** (shared liveness endpoint, not an Order resource). | **Deferred**; §33 forbids route redesign. |
| **F-5** | Settlement-only pricing had domain coverage but no persistence/outbox-level gate. | **Test gap** — the only real gap found. | **Fixed** in `SettlementOnlyPricingGateTests` (2 tests). No production code changed. |

No P2 defect was found. No blocker.

## 10. Static search register (§34)

| Search (over `src/`, excluding `obj/`) | Hits | Classification |
|---|---|---|
| `AeroTech.Messages.AirPrice` in Domain | 0 | clean |
| `AeroTech.Messages.AirPrice` anywhere | 5 files | all in `Providers/Offer` ACL — correct |
| `JsonElement` / `dynamic` / ` object ` in OrderView + queries | 0 | clean |
| `GetOrderDetailsQuery` response type | `IRequest<OrderView?>` | typed |
| `CurrentCustomerId` | 0 | the `?? 0` pattern no longer exists |
| `AttributesJson` outside Domain/Persistence | 0 | no leak |
| `Ledger` in `src/` | 0 | no dependency |
| Payment aggregate on the active P2 path | 0 | none |
| `Money` / `CurrencyCode` / `Rounding` / `MidpointRounding` / `Math.Round` | 0 | no subsystem introduced |
| `FareBasis` + `Substring|StartsWith|EndsWith|Split|Regex|ToCharArray` | 0 | never parsed |
| `openjaw` / `isroundtrip` / `infer` (case-insensitive) | 0 | no geometric inference |
| `BeginTransaction` / `TransactionScope` / `ExecutionStrategy` | 1 | `OrderingUnitOfWork` only — no provider IO inside |

## 11. API surface inventory (§33)

| Channel | Route prefix | Actions |
|---|---|---|
| Backoffice | `Backoffice/v1/Orders` | `POST FlightOffers`, `GET Paginated`, `POST {id}/Cancel`, `/Split`, `/Documents/{documentId}/Void`, `/Remarks`, `GET {orderId}/Details`, `POST {orderId}/Reserve`, `/Issue`, `/Change`, `/Withdraw` |
| OTA Api | `Api/v1/Bookings` | `POST FlightOffers`, `GET {orderId}`, `POST {orderId}/Change` — all three guarded |
| OTA Panel | `OtaPanel/v1/Bookings` | `POST FlightOffers` — guarded |
| Internal | `Internal/v1/Orders` | `POST`, `POST FlightOffers`, `GET {id}`, `POST {id}/Reservations`, `/Payments`, `/Issuance`, `/Cancel`, `/Split`, `/Documents/{documentId}/Void`, `/Remarks` |
| Internal | `Internal/v1/FulfillmentTasks` | `POST {id}/Run`, `POST {id}/Retry` |
| Service | `Service/v1/Bookings` | declared shell, **no actions** |
| Shared | `api/v1/Ping` | liveness (F-4) |

No duplicate alias, no `RQ`/`RS` message-shaped route, no NDC transport clone, no provider-specific public
route, and no unowned customer-facing OrderId endpoint. Order action segments are PascalCase.
