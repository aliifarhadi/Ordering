# P2-C — Standard Air Fare Construction

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline commit `c29dc60` (P2-B.1, 328 tests). P0–P2-A.1 frozen. **P2-D not started.**

Practicality audit: [`audit/p2/P2-C-FARE-CONSTRUCTION-SEMANTIC-AUDIT.md`](audit/p2/P2-C-FARE-CONSTRUCTION-SEMANTIC-AUDIT.md)
Evidence: [`audit/p2/P2-C-TEST-RUN.txt`](audit/p2/P2-C-TEST-RUN.txt)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **192 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **177 passed**, 0 failed (real SQL Server) |
| Total | **369 passed, 0 failed** (baseline 328 → +41, zero regressions) |

## 2. Final P2-B closure (§1)

`AcceptedProductBuilder` was assigning `AirFareId` to **both** `SourcePolicyReference` and
`SourcePricingReference`. The current AirPrice source exposes neither a policy/rule reference nor a distinct
pricing-decision reference, so one identifier was being given three different semantic meanings purely because
target fields existed. Both are now `null`; only `SourceProductReference` retains `AirFareId` as opaque
fare/product provenance for `ProductType.AirFare`.

The other nullable provenance fields were reviewed under the same rule and remain `null` for the current
adapter: `BrandCode`, `SupplierCode`, `SourcePolicyVersion`, `ProductCode`, `ProductName`. Two tests pin this,
and the domain test factories were aligned so they model the real source rather than an optimistic one. No
separate P2-B.2 phase was created; this is recorded here as final P2-B closure.

## 3. The model

```text
Order
└── OrderAirFareConstruction[]          (immutable, Order-owned, no separate aggregate/repository)
    ├── OrderAirFareConstructionItem[]  (immutable item membership evidence)
    └── OrderFarePricingGroup[]
        ├── OrderFarePricingGroupTraveller[]
        └── OrderFarePricingUnit[]
            └── OrderFareComponent[]
                ├── OrderFareComponentService[]
                └── OrderFareComponentSegment[]
```

Ordering-owned vocabulary added under the closed enum convention: `AirFareConstructionType`,
`FarePricingUnitType`, `FareCombinationMethod`. No `FareConstructionV2`/`PricingV2`. The construction takes part
in the same Order unit of work and is created inside the same accepted commercial mutation as the sale.

**Nothing is inferred.** `ConstructionType`, `PricingUnitType` and `CombinationMethod` are all nullable and are
stored only when the authoritative source states them; a returning itinerary never becomes a `RoundTrip` by
geometry. Pinned by tests.

**True RT vs OW+OW is structural**, not a flag: a true round trip is one `RoundTrip` pricing unit holding two
fare components, while the same itinerary priced as two one-ways is two `OneWay` units holding one component
each. A test asserts the two shapes stay distinguishable while the component count matches.

**Through fare is first-class.** One `OrderFareComponent` may cover several `OrderService`s and several
segments, via explicit stable `OrderServiceId` links — coverage is never reconstructed by comparing airport
pairs. Fare break is the same structure with separate components. `FareComponent` and `OrderService` remain
distinct identities.

**Pricing groups default to one traveller.** Travellers are only grouped when the accepted input explicitly
groups them (carrying the source's own group reference); shared `PassengerTypeCode`, fare basis or price never
merge groups, and `PassengerTypeCode` is pricing context, never traveller identity. No fake traveller records
exist.

## 4. Independence from the monetary ledger

`FareConstruction` carries no amount, tax, commission or total. Accepting one does not move `CustomerTotal`,
`FinancialSequence`, `ObligationVersion` or `CommercialVersion` — a test compares an order with and without a
construction and asserts all four are identical, with one `PriceChangeSet` and one `OrderChange` either way. A
fare component requires no matching pricing line, and ticket value attribution still flows only through
`DocumentPriceLink → PricingLine`, asserted against real issuance.

## 5. Current AirPrice treatment

The current source cannot defensibly supply pricing-unit boundaries, unit types or combination methods, so per
§18 the adapter emits **no** construction: `FareConstructions` stays empty and that is a valid Order. Nothing is
fabricated — no unit per bound, no RT because the itinerary returns, no `ProviderDefined` filler. Tests assert
the normalizer never references the accepted fare-construction types at all.

## 6. ETKT integration

`Order.ResolveIssueFareBasis(orderServiceId)` is the single issue-time resolver: it returns
`FareComponent.FareBasis` when an active construction covers the service, and otherwise falls back to the
transitional `OrderAirTransportService.FareBasis`. `IssueOrderService` now calls it instead of reading the
service field directly.

Both paths are tested: with a through-fare construction the coupon's fare-basis snapshot is `YTHRU` (the
component value, deliberately different from the service field), and a current-source order with no
construction still issues through the explicit fallback. A multi-segment fare component produces exactly one
coupon per service with no duplication, and P1.1/P1.2 partial-issuance and recovery behaviour is unchanged.

The fallback exists only because the current source cannot supply construction; no construction is manufactured
to remove it.

## 7. Persistence

Eight new tables — `OrderAirFareConstructions`, `OrderAirFareConstructionItems`, `OrderFarePricingGroups`,
`OrderFarePricingGroupTravellers`, `OrderFarePricingUnits`, `OrderFareComponents`, `OrderFareComponentServices`,
`OrderFareComponentSegments` — all relational (no JSON for core relationships, since P3 will query them), with
unique indexes on each membership pair. `OrderRepository` loads the full hierarchy.

Migration `P2CAirFareConstruction` is purely additive (8 `CreateTable`). A second migration,
`P2CIgnoreComputedFareComponents`, removes a spurious shadow foreign key: EF had mistaken the computed
`OrderAirFareConstruction.FareComponents` convenience property for a real navigation and generated an
`OrderAirFareConstructionId` column on `OrderFareComponents`. The property is now explicitly ignored. Previously
applied P2 migrations were not edited; both new migrations are applied to `DotAirOrderNew`.

**One implementation defect found and fixed during testing:** the four membership entities never assigned their
`Id`, so with `ValueGeneratedNever()` every row would have collided on key `0`. They now take a generated
snowflake id like every other entity.

## 8. Invariants enforced

A construction belongs to one Order; group → construction, unit → group, component → unit; a construction cannot
supersede itself (2800); it requires at least one pricing group (2801); a group requires at least one traveller
(2802) and one pricing unit (2803); a unit requires at least one fare component (2804); a component must cover
at least one sold service (2805); and every referenced item, traveller, service and segment must belong to the
same Order (2806, with unresolved accepted refs failing as 2784). Deliberately **not** constrained: one service
per component or one segment per component — through fares exist.

Immutability is structural: no public setters and no mutating methods on any of the four types (asserted by
reflection). "Current" is derived from `SupersedesConstructionId` lineage rather than a mutable `IsCurrent`
flag, and successors carry `SupersedesConstructionId` + `CreatedByChangeId`. P3 servicing workflow is not
implemented.

## 9. Tests added (+41)

`tests/AeroTech.Ordering.Domain.Tests/P2/AirFareConstructionTests.cs` (30) covers requirements 1–30: optionality,
no fabrication from the current source, explicit OW/RT/OpenJaw, RT vs OW+OW distinctness, nothing inferred,
one-traveller-default groups, no PTC merging, explicit source grouping, through fare, fare break, component ≠
service, opaque fare basis with no parsed rules, brand independent of fare basis, no FareFamily model, no
required pricing line, no monetary movement, opaque references retained, missing references null, immutability,
lineage, self-supersede refusal, cross-Order reference refusal, and both issue-time fare-context paths.

`tests/AeroTech.Ordering.Domain.Tests/P2/FareConstructionBoundaryTests.cs` (5) covers 34–39: no
AirPrice/Providers vocabulary in fare-construction types or source files, the normalizer infers no pricing
units, and no false policy/pricing reference is populated.

`tests/AeroTech.Ordering.Persistence.Tests/P2/AirFareConstructionPersistenceTests.cs` (6) covers 31–33 and the
relational round trip: full RT hierarchy reload, through-fare component reload, no construction persisted when
absent, no duplicate coupons from a multi-segment component, coupons taking fare context from the component,
and ticket value attribution still coming from pricing lines.

## 10. Exit gate

| Gate | Status |
|---|---|
| `AirFareConstruction` is Ordering-owned and immutable | **Yes** |
| Group/unit/component represent accepted semantics, not provider schema | **Yes** |
| True RT and OW+OW representable distinctly | **Yes** — structurally |
| Through fare is first-class | **Yes** |
| Fare construction is optional | **Yes** |
| Current AirPrice fabricates no PU/construction semantics | **Yes** — emits none |
| FareBasis ownership moves to `FareComponent` when a construction exists | **Yes** |
| P1 ETKT still works for no-construction orders via explicit fallback | **Yes** |
| FareConstruction does not duplicate monetary truth | **Yes** |
| No false source policy/pricing references | **Yes** — §1 closure |
| Every persisted field passes the practicality audit | **Yes** |
| All previous tests remain green | **Yes** — 369 passed, 0 failed |
| P2-D not started | **Yes** |

No `BLOCKED_DECISION` was required.

## 11. Carried forward

- `OrderAirTransportService.FareBasis` / `FareFamilyTitle` / baggage remain transitional P1 compatibility
  fields. New logic must not use them; the fare-basis fallback is now explicit and tested, and removal waits on
  upstream source coverage (P2-D/P3).
- The legacy service permission flags stay derived from the item's commercial term summary (P2-B.1) — no
  commercial policy was added to `FareComponent`.
