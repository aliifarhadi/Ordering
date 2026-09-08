# P2-C — Fare Construction Practicality Audit

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08

Binding rule: every persisted field must satisfy **Ordering ownership + concrete Order use case +
source-independent meaning + authoritative/defensible source**. Justifications of the form "ATPCO has it",
"NDC has it", "AirPrice sends it" or "might be useful later" are rejected outright.

Legend for *Required/optional*: **Required** = the model cannot express accepted fare construction without it;
**Optional** = stored only when the authoritative source supplies it, otherwise `null`.

---

## 1. `OrderAirFareConstruction`

| Field | Ordering meaning | Concrete consumer / use case | Authoritative source | Req/Opt | Why stored rather than derived |
|---|---|---|---|---|---|
| `Id` | Identity of one accepted pricing context | Lineage target for successors; parent of the hierarchy | Ordering `IIdGenerator` | Required | Ordering identity is never derived from a provider id |
| `OrderId` | Owning Order | Aggregate ownership; every reference is scoped to one Order | Ordering | Required | Enforces the same-Order invariant |
| `CreatedByChangeId` | Which accepted commercial mutation created it | Audit; ties construction to the `OrderChange` that accepted it | Ordering `OrderChange` | Required | Cannot be recomputed after later mutations |
| `SupersedesConstructionId` | Lineage to the construction it replaces | `CurrentFareConstruction()` derives "current" from lineage instead of a mutable flag | Ordering | Optional | Immutable successor lineage replaces an `IsCurrent` flag (§29) |
| `ConstructionType` | Accepted trip construction shape | P3 change/refund scoping; redisplay | Authoritative pricing source only | Optional | **Never inferred from itinerary geometry**; absent when the source does not say |
| `SourceSystem` | Which upstream produced the construction | Audit; disambiguates the opaque references below | ACL | Required | Provenance for every stored reference |
| `SourcePricingReference` | Opaque upstream pricing-decision reference | Audit; lets P3 fetch the original decision | Source, when it genuinely supplies one | Optional | Left `null` for the current AirPrice adapter — see §4 |
| `CreatedAt` | When the construction was accepted | Ordering current-vs-historical resolution; audit | Ordering clock | Required | Historical fact |

**Rejected here:** `IsCurrent` (derivable from lineage), any total/amount (the pricing ledger owns money),
any refund/change/upgrade flag (owned by `OrderItemCommercialTermsSnapshot` and P3 quotes).

### `OrderAirFareConstructionItem`

| Field | Ordering meaning | Consumer | Source | Req/Opt | Why stored |
|---|---|---|---|---|---|
| `FareConstructionId` / `OrderItemId` | Immutable membership evidence between construction and priced item | P3 must know which items a construction priced, after split/servicing has moved current ownership | Accepted source `ProductRefs` resolved to Ordering ids | Required | Current `OrderItem` ownership is not a reliable historical answer |

## 2. `OrderFarePricingGroup`

| Field | Ordering meaning | Consumer | Source | Req/Opt | Why stored |
|---|---|---|---|---|---|
| `Id`, `FareConstructionId` | Group identity and parent | Hierarchy | Ordering | Required | — |
| `PassengerType` | Accepted passenger pricing context | Redisplay; P3 repricing context | Source, when supplied | Optional | Pricing context only — **never traveller identity**, and equality never merges groups |
| `SourceReference` | Opaque upstream group reference | Audit; proves the source really grouped travellers | Source, when supplied | Optional | The only defensible evidence that a multi-traveller group was authoritative |

### `OrderFarePricingGroupTraveller`

| Field | Ordering meaning | Consumer | Source | Req/Opt | Why stored |
|---|---|---|---|---|---|
| `PricingGroupId` / `OrderTravellerId` | Which real Order travellers were priced in this context | P3 repricing scope; redisplay | Accepted `TravellerRefs` resolved to Ordering ids | Required | The authoritative relationship is to real travellers, not to a PTC; no fake traveller records are created |

## 3. `OrderFarePricingUnit`

| Field | Ordering meaning | Consumer | Source | Req/Opt | Why stored |
|---|---|---|---|---|---|
| `Id`, `PricingGroupId` | Unit identity and parent | Hierarchy | Ordering | Required | — |
| `PricingUnitType` | Accepted unit shape (OW/RT/OpenJaw/CircleTrip/Other) | **The true-RT vs OW+OW distinction** that P3 change/refund depends on | Source only | Optional | Cannot be reconstructed from route shape later; absent when unstated |
| `CombinationMethod` | How the fare was combined | P3 repricing/combinability context | Source only | Optional | Never derived |
| `Sequence` | Accepted ordering of units | Stable redisplay and deterministic scoping | Source | Required | Preserves source order without re-sorting |
| `SourceReference` | Opaque upstream unit reference | Audit | Source, when supplied | Optional | Provenance |

**The structural distinction itself is carried by the shape** — one RT unit with two components versus two OW
units with one component each — not by a derived flag.

## 4. `OrderFareComponent`

| Field | Ordering meaning | Concrete consumer / use case | Authoritative source | Req/Opt | Why stored rather than derived |
|---|---|---|---|---|---|
| `Id`, `PricingUnitId` | Component identity and parent | Hierarchy | Ordering | Required | — |
| `Sequence` | Accepted order within the unit | Redisplay; deterministic scoping | Source | Required | — |
| `OriginAirportId` / `DestinationAirportId` | Accepted fare-component endpoints | Redisplay; P3 scope checks on through fares where endpoints ≠ segment endpoints | Source | Optional | For a through fare the endpoints are **not** derivable from any single segment |
| `FareBasis` | Accepted sale-time fare basis | **Issue-time fare context** (`ResolveIssueFareBasis` → ETKT coupon), P3 repricing | Source | Optional | Authoritative home of fare basis once a construction exists (§12) |
| `BrandCode` / `BrandName` | Accepted retail brand identity | Redisplay of what the customer bought | Source, when supplied | Optional | Retail identity is independent of fare basis; never invented from a fare id |
| `FareType` | Accepted fare type label | Redisplay; P3 context | Source, when supplied | Optional | Opaque label, not interpreted |
| `CabinClassId` / `RbdId` / `BookingClass` | Accepted fare-level cabin/RBD context | P3 repricing context where the fare-level value can differ from the segment snapshot | Source, when supplied | Optional | Kept only because fare-level and segment-level values may legitimately differ |
| `FareOwnerCarrierId` | Which carrier owned the fare | P3 repricing/interline attribution; distinct from marketing/operating/owner airline | Source, when supplied | Optional | A distinct commercial role that no other field carries |
| `TariffReference` / `RuleReference` / `RoutingReference` | Opaque upstream rule provenance | P3 must be able to cite the accepted rule set; audit | Source, when supplied | Optional | **Opaque references only** — no rule structure is cloned and nothing is executed from them |
| `SourceFareReference` / `SourceComponentReference` | Opaque upstream identifiers | Audit; correlating back to the pricing decision | Source, when supplied | Optional | Provenance, never reused as an Ordering code |
| `CreatedAt` | When the component was accepted | Audit | Ordering clock | Required | Historical fact |

### `OrderFareComponentService` / `OrderFareComponentSegment`

| Field | Ordering meaning | Consumer | Source | Req/Opt | Why stored |
|---|---|---|---|---|---|
| `FareComponentId` / `OrderServiceId` | Which sold air-service obligations the component priced | **Through fare and fare break**; `ActiveFareComponentFor(serviceId)` at issue time; P3 scoping | Accepted `ServiceRefs` | Required | §17 forbids reconstructing coverage by comparing airport pairs during servicing; stable across split |
| `FareComponentId` / `OrderSegmentId` | Which sold segments the component covered | Redisplay; segment-scoped validation | Accepted `SegmentRefs` | Optional (may be empty) | Service links are authoritative; segment links are supplementary evidence |

---

## 5. Fields deliberately **not** implemented

| Candidate | Why rejected |
|---|---|
| `FareComponent.TravelerId` | Redundant and a contradiction risk: the `PricingGroup` owns traveller association and every linked `OrderService` already resolves to exactly one traveller. Nothing breaks without it. |
| `IsCurrent` on the construction | Derivable from `SupersedesConstructionId` lineage (§29). |
| Any amount / total / tax / commission | The `PricingLine` ledger is the only monetary truth (§15). |
| `IsRefundable` / `IsChangeable` / `IsUpgradable` on the component | §14 — sale-time summary belongs to `OrderItemCommercialTermsSnapshot`; real authority is the P3 quote. |
| Baggage, seat, priority, lounge, meal on the component | §26 / §13 — sold benefits become `OrderService`s in P2-D. |
| Fare-family features/rules/hierarchy | §13 — no FareFamily model exists in Ordering; a test asserts no type is named `FareFamily`. |
| Min/max stay, advance purchase, seasonality, routing maps, ATPCO categories | §34 — not implemented; only opaque references are retained. |
| Parsed meaning from `FareBasis` characters | §12 — `FareBasis` is an opaque industry code for Ordering. |
| A `PricingUnit → OrderItem` link | The construction-level item membership already answers it; a second path would create contradiction. |

## 6. Current AirPrice treatment

The current AirPrice offer supplies `FareBasis`, `FareFamily`, `BoundId` and `AirFareId` but **no** authoritative
pricing-unit boundaries, no true-RT vs OW+OW evidence, no unit type and no combination method. Per §18 the
adapter therefore emits **no** fare construction at all: `AcceptedOrderSource.FareConstructions` stays empty and
`Order.FareConstructions` is empty. Absence is recorded as correct, not as a gap to be filled with invented
structure — asserted by test.

Consequently `SourcePricingReference` and `SourcePolicyReference` are `null` for AirPrice-sourced orders: the
source exposes no distinct pricing-decision or policy reference, and `AirFareId` is not relabelled to populate
them (§1 closure).
