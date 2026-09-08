# P2-B — Accepted Source Normalization & Commercial Snapshots

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
P0–P1.2 frozen. P2-A and P2-A.1 frozen and unchanged in semantics. **P2-C not started.**

Mapping audit: [`audit/p2/P2-B-SOURCE-NORMALIZATION-MAPPING.md`](audit/p2/P2-B-SOURCE-NORMALIZATION-MAPPING.md)
Evidence: [`audit/p2/P2-B-TEST-RUN.txt`](audit/p2/P2-B-TEST-RUN.txt)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **141 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **162 passed**, 0 failed (real SQL Server) |
| Total | **303 passed, 0 failed** (baseline 257 → +46, zero regressions) |

## 2. The boundary that now exists

```text
AirPrice wire response
  → OfferResponseMapper.ToProviderModel   (Providers/Offer/Wire → Providers/Offer/Model)
  → AirPriceOfferNormalizer               (Providers/Offer/Services — the ACL)
  → AcceptedOrderSource                   (Ordering-owned semantics, Domain)
  → Order.Create(...)                     (validates and commits)
```

`OfferDetail`, `OfferReader` and the whole offer graph moved out of the Domain into
`AeroTech.Ordering.Providers.Offer.Model` (one record per file; `OfferReader` is now `internal` to the ACL).
The Domain's `OrderAggregate/Offers/` folder is gone.

`IOfferProvider` was reshaped: `GetByOfferIdAsync → GetAcceptedSourceAsync`, returning `AcceptedOrderSource`.
`OfferProvider` fetches and delegates translation to the injected `IAirPriceOfferNormalizer`; the Domain never
sees AirPrice vocabulary and the provider never asks the Domain what anything means.

There is exactly **one** active creation path. No `OrderV2`, no parallel model, no legacy `OfferDetail`
overload left behind.

## 3. The Ordering-owned accepted source

`Domain/OrderAggregate/AcceptedSource/` — `AcceptedOrderSource`, `AcceptedSourceTraveller`,
`AcceptedJourney`, `AcceptedSegment`, `AcceptedSegmentLeg`, `AcceptedProduct`, `AcceptedProductSnapshot`,
`AcceptedCommercialTerms`, `AcceptedBaggageAllowance`, `AcceptedService`, `AcceptedAirServiceDetail`,
`AcceptedSourcePricingLine` (plus an internal `AcceptedSourceRefMap` used only during creation).

Correlation uses source-local refs — `JourneyRef`, `SegmentRef`, `ProductRef`, `ServiceRef`, `TravellerRef` —
which are **never** Ordering identities. The Domain allocates every real id through `IIdGenerator` and
resolves the refs; an unresolvable ref is rejected (2784) rather than silently dropped.

## 4. Fail-closed source interpretation

Both silent defaults the brief called out are gone, and both now live at the ACL:

| Was | Now |
|---|---|
| `ComponentTypeOf(kind) => _ => PricingComponentType.Fee` in `Order.Create` | `AirPriceOfferNormalizer.ComponentTypeOf` maps Tax/Surcharge/Fee explicitly and **throws 2785** on an unknown kind *or* an unresolvable charge reference. A future enum value cannot become a Fee. |
| `ParseWeightUnit(unit) => WeightUnit.Kg` | `AirPriceOfferNormalizer.BaggageOf` returns `null` when no allowance is supplied, and **throws 2786** when an allowance *is* supplied with a missing or unrecognised unit. |

`WeightUnit.Kg` no longer appears as a literal anywhere in `src` — the unused `Baggage.None()` factory that
carried the last one was removed.

The accepted `Surcharge → CarrierSurcharge` mapping for the current coarse AirPrice contract is unchanged and
now exists **only** in the normalizer. Every other `CarrierSurcharge` reference in the codebase is
Ordering-side component handling (totals, the component policy, legacy wire translation), not source
classification.

## 5. P2-A pricing passes through unreinterpreted

The normalizer emits `AcceptedSourcePricingLine` already carrying `ComponentType`, `Effect`, `Direction`,
`LineRole`, `OriginalAmount`/`OriginalCurrencyId`, `SaleAmount`/`SaleCurrencyId`, `ExchangeRate` provenance,
`BasisType`, `ApplicationLevel`, `Refundability`, `Code`, `Description`, `SourceLineRef` and `OccurrenceKey`.
`Order.Create` maps refs to ids and hands them to the frozen `CommitPriceChange` unchanged.

Original sale normalizes to `Effect = CustomerBalance`, `Direction = Debit`, `LineRole = Original`. The
P2-A.1 split between `SourceLineRef` (stable identity: offer:traveller:bound:flight:code) and
`OccurrenceKey` (separate ordinal) is preserved, so genuine repeats of the same tax code stay distinguishable
while the occurrence is not embedded in the semantic reference.

## 6. Product and commercial terms snapshots

`OrderItem` now owns three distinct snapshots, and §15's separation is explicit:

| Snapshot | Answers |
|---|---|
| `OrderItemProductSnapshot` | *what product was commercially accepted* — product type, source product reference, code/name/brand, marketing & operating carrier when unambiguous, supplier, source system/offer/pricing reference, accepted-at |
| `OrderItemCommercialTermsSnapshot` | *what customer-facing terms were accepted* — refundable/changeable/upgradeable evidence, checked & cabin baggage allowance, policy source, source rule reference, captured-at |
| `OrderItemPolicySnapshot` (pre-existing, untouched) | *how Ordering operationally and accountably treats the item* — delivery model, accounting granularity, assignment mode, requires-passenger/segment/document/fulfilment |

No responsibility is duplicated and `OrderItemPolicySnapshot` was not deleted or renamed.

Both new snapshots are immutable accepted-sale evidence: private setters, no public mutators, constructed
only during creation, and copied (never shared) on split. A test asserts by reflection that neither type
exposes a public setter or a public mutating method, and another asserts that mutating a refreshed copy of
the source record cannot change the persisted snapshot. Only source-supplied facts are stored — the source
knows `IsRefundable = true`, so that is what is snapshotted; no penalty, deadline, waiver or refund amount is
fabricated.

## 7. Persistence

New tables `OrderItemProductSnapshots` and `OrderItemCommercialTermsSnapshots`, both one-to-one with
`OrderItems` and cascade-deleted, with baggage stored as owned columns. Migration
`P2BAcceptedSourceSnapshots` is **purely additive** — two `CreateTable` in `Up`, two `DropTable` in `Down`,
no renames, no column drops, no reinterpretation. `P2PricingFoundation` and `P2A1PricingProvenance` were not
touched. Applied to `DotAirOrderNew`. `OrderRepository.AggregateQuery()` includes both snapshots.

## 8. Ticketing deadline, schedule and carrier roles

The provider's `LastTicketingDate` becomes `AcceptedOrderSource.TicketingDeadline` at the ACL; the Domain
never sees the provider field name and simply establishes `TimeToLive`. No passive expiry behaviour was
added, and the P1 rule that time limits are constraints rather than Order states is untouched.

Sold-schedule facts (flight source id/version/number, origin/destination/terminals, departure/arrival,
duration, aircraft, cabin/RBD/booking class, capacity reference, legs) are carried as accepted-sale snapshot
evidence into the existing `OrderSegment`/`OrderSegmentLeg`. Marketing carrier, operating carrier and
supplier stay distinct from each other and from `OwnerAirlineId`, which remains the trusted home operator
identity supplied by `IHomeOperatorProvider` — a test pins that a segment's marketing carrier is not the
owner airline. No P5 operational state was introduced.

## 9. Tests added (+46)

- **Normalizer (ACL), 22 cases** — `tests/AeroTech.Ordering.Persistence.Tests/P2/AirPriceOfferNormalizerTests.cs`:
  the three supported charge kinds; unknown kind fails closed; unresolvable charge fails closed;
  customer-balance/debit/original polarity; original and sale amounts and both currencies preserved; ROE
  provenance preserved; repeated tax code keeps one identity with distinct occurrences; different source
  lines stay distinct; occurrence not embedded in the reference; refundability/changeability/upgradeability
  preserved; baggage preserved with its unit; unknown/empty/missing baggage unit rejected (3 cases);
  ticketing deadline normalized; marketing and operating carriers distinct; traveller/journey/segment/service
  correlations stable; product snapshot evidence; order-level charge becomes an Order-basis PerOrder line.
- **Domain, 13 cases** — `tests/AeroTech.Ordering.Domain.Tests/P2/AcceptedSourceCreationTests.cs`:
  `Order.Create` takes the normalized source and no offer type; customer total equals the accepted sale
  values; product and terms snapshots on every item; snapshot immutability; a later source refresh cannot
  mutate accepted snapshots; source identity + occurrence survive creation; one committed `OriginalSale`
  change set with `FinancialSequence` 1; one `Create` `OrderChange` with unchanged version semantics;
  ticketing deadline → `TimeToLive`; carrier roles ≠ owner airline; empty product set refused; unresolvable
  service ref refused.
- **Architecture boundary, 11 cases** — `tests/AeroTech.Ordering.Domain.Tests/P2/DomainProviderBoundaryTests.cs`:
  the Domain assembly defines no offer types and has no `Offers` namespace; no Domain member signature uses
  `AirChargeKind` or AirPrice `StopType`; the Order aggregate's only AirPrice enum references are exactly
  `PassengerTypeCode` and `WeightUnit`; `Order.Create.cs` contains no AirPrice namespace, `AirChargeKind`,
  `OfferReader` or `OfferDetail`, and no `WeightUnit.Kg` / `PricingComponentType.Fee` fallback.

## 10. Closure checklist (§28)

| Item | Status |
|---|---|
| Domain has no AirPrice-specific creation decisions | **Yes** — boundary tests + source-file assertions |
| no `_ => Fee` fallback | **Yes** — none in `src` |
| no unknown-weight-unit ⇒ Kg fallback | **Yes** — no `WeightUnit.Kg` literal in `src` |
| Surcharge mapping lives only at ACL boundary | **Yes** — only in `AirPriceOfferNormalizer` |
| `SourceLineRef` and `OccurrenceKey` remain separate | **Yes** |
| `ProductSnapshot` immutable accepted-sale evidence | **Yes** |
| `CommercialTermsSnapshot` immutable accepted-sale evidence | **Yes** |
| `OrderItemPolicySnapshot` not semantically duplicated | **Yes** — three-way separation documented in §6 |
| home `OwnerAirlineId` separate from carrier/supplier roles | **Yes** |
| no P2-C FareConstruction introduced | **Yes** — no such type exists |
| no generic raw JSON framework introduced | **Yes** |
| no sibling service changed | **Yes** — only this repository |

## 11. Recorded, not hidden

- **Two AirPrice enums remain referenced by the Order aggregate**: `PassengerTypeCode` (on `OrderTraveller`,
  from caller context) and `WeightUnit` (on `Baggage`). These are shared platform *value vocabulary*, not
  provider decision logic, and the standing project rule that enums stay in `Contracts/AeroTech.Messages`
  and are never moved, wrapped or duplicated for layering purity is closed — so they were left in place and
  pinned by an explicit allow-list test instead of being silently tolerated.
- **`JourneyType`** still appears on the pre-existing `Domain/Providers/Pricing` port contract. That is a
  provider-port boundary type outside the creation path and outside P2-B scope; it is recorded here.
- **Pre-existing coercions preserved deliberately** (documented in the mapping audit §9): `AircraftId ?? 0`
  and `AirFareId ?? 0` on `OrderSegment`, which are P1 schema non-nullability artefacts on descriptive
  fields, not new invented business facts.
- **`OfferFlightStop`** and the offer-side `PassengerTypeCode` remain unconsumed source facts, as in P1;
  they are retained on the provider model with the reason recorded in the mapping audit.
- **`AirTransportPolicy`** is still the single hard-coded item policy in `Order.Create`; that generalizes in
  P2-D when the service/item model becomes composition-based.

No `BLOCKED_DECISION` was required.
