# P2-B.1 — Domain Semantic Audit

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08

Every field introduced by P2-B in the accepted-source and snapshot model, judged against the four-part rule:
**Ordering ownership + concrete Ordering use case + source-independent meaning + authoritative/defensible input.**
A field failing any part is removed or deferred. "Might be useful later" is never a justification.

Legend: **Keep** · **Refine** (survives with corrected meaning/type) · **Remove** · **Defer**.

---

## 1. `AcceptedOrderSource`

| Field | Ordering meaning | Actual consumer / use case | Source | Decision |
|---|---|---|---|---|
| `SourceSystem` | Which upstream produced this accepted sale | Written to `ProductSnapshot.SourceSystem`; audit + future multi-source disambiguation | ACL constant | **Keep** — opaque provenance |
| `SourceOfferId` | The accepted offer reference | `PriceChangeSet.SourceOfferId`, source-line identity prefix, `ProductSnapshot.SourceOfferId` | `OfferDetail.OfferId` | **Keep** |
| `SaleCurrencyId` | Order sale currency | `Order.CurrencyId`; P2-A validates every customer-balance line against it | `OfferDetail.CurrencyId` | **Keep** |
| `TicketingDeadline` | When the sale must be ticketed | `Order.TimeToLive`; P1 expiry/issue flow | `LastTicketingDate`, renamed at ACL | **Keep** |
| `Travellers[]` | Source↔caller traveller correlation | Resolves `TravellerRef` → Ordering traveller id | offer travellers | **Keep** |
| `Journeys[]` | Sold journey/segment structure | `OrderItinerary` + `OrderSegment`; P1 reserve/issue | offer bounds/flights | **Keep** |
| `Products[]` | Individually priced commercial items | `OrderItem` + snapshots + services | derived at ACL | **Keep** |
| `PricingLines[]` | Accepted monetary facts | P2-A `CommitPriceChange` | offer price lines/charges | **Keep** |

## 2. `AcceptedProduct`

| Field | Ordering meaning | Actual consumer / use case | Source | Decision |
|---|---|---|---|---|
| `ProductRef` | Source-local product correlation key | Maps pricing lines to the created `OrderItem`; never an Ordering identity | ACL-derived | **Keep** |
| `TravellerRef` | Which traveller the product was sold to | Product↔traveller correlation | offer traveller ref | **Keep** |
| `ProductType` | What kind of product was sold | `OrderItem.ProductType`; drives item semantics | ACL classification | **Keep** |
| `Quantity` | Sold quantity | `OrderItem.Quantity` | ACL (1 per passenger fare) | **Keep** |
| `UnitOfMeasure` | How the quantity is counted | `OrderItem.UnitOfMeasure` | ACL | **Keep** |
| `Snapshot` | Accepted product evidence | `OrderItemProductSnapshot` | ACL | **Keep** |
| `CommercialTerms` | Accepted customer-facing terms | `OrderItemCommercialTermsSnapshot` | ACL | **Keep** |
| `Services[]` | Sold service obligations | `OrderAirTransportService` | ACL | **Keep** |
| `ProductCode` | Ordering-meaningful product code | `OrderItem.ProductCode` — **redisplay only** | **AirPrice supplies none** | **Refine** → now optional and **left null**. Previously fed `AirFareId`, an external database identifier, which is not a product code. |
| `ProductName` | Ordering-meaningful product name | `OrderItem.ProductName` — **redisplay only** | **AirPrice supplies none** | **Refine** → now optional and **left null**. Previously fed `FareBasis`, which is fare-construction context, not a product name. |

## 3. `AcceptedProductSnapshot` / `OrderItemProductSnapshot` (persisted)

| Field | Ordering meaning | Actual consumer / use case | Source | Decision |
|---|---|---|---|---|
| `ProductType` | What was sold | Redisplay of accepted product; historical audit | ACL | **Keep** |
| `SourceProductReference` | Opaque upstream product identifier | Historical audit; correlating an old sale back to its source | `AirFareId` | **Keep** — provenance, explicitly *not* a ProductCode |
| `SourceSystem` | Which upstream owned that reference | Disambiguates the reference; audit | ACL constant | **Keep** |
| `SourceOfferId` | Which offer was accepted | Audit; re-display of the accepted sale | `OfferDetail.OfferId` | **Keep** |
| `ProductCode` | Customer/airline-visible product code | Redisplay | none today | **Refine** → optional, left null |
| `ProductName` | Customer-visible product name | Redisplay | none today | **Refine** → optional, left null |
| `BrandCode` | Machine-readable fare-brand identity | Redisplay; future brand-aware servicing pre-screen | none today | **Keep as nullable, left null** — no code is invented from `AirFareId` |
| `BrandName` | Customer-visible fare brand/family label | Redisplay of what the customer bought | `FareFamily`, only because the ACL explicitly treats it as a brand label | **Refine** (was `Brand`) |
| `MarketingAirlineId` | Marketing carrier role at sale | Redisplay; carrier-role distinction from `OwnerAirlineId` | segment carriers, only when unambiguous | **Keep** |
| `OperatingAirlineId` | Operating carrier role at sale | Same | segment carriers, only when unambiguous | **Keep** |
| `SupplierCode` | Non-carrier supplier of the product | Needed for non-air products | none today (air) | **Keep as nullable, left null** |
| `SourcePricingReference` | Upstream pricing reference | Ties the accepted product to its pricing decision; audit | `AirFareId` | **Keep** — opaque reference |
| `AcceptedAt` | When the sale was accepted | Historical audit; snapshot immutability evidence | clock | **Keep** |

## 4. `AcceptedCommercialTerms` / `OrderItemCommercialTermsSnapshot` (persisted)

| Field | Ordering meaning | Actual consumer / use case | Source | Decision |
|---|---|---|---|---|
| ~~`IsRefundable`~~ | — | — | — | **Removed** — a provider-shaped boolean is not an Ordering rule model |
| ~~`IsChangeable`~~ | — | — | — | **Removed** |
| ~~`IsUpgradable`~~ | — | — | — | **Removed** |
| ~~`CheckedBaggage`~~ | — | — | — | **Removed** — baggage is not change/refund policy; duplicated `OrderAirTransportService`. **Defer to P2-D** |
| ~~`CabinBaggage`~~ | — | — | — | **Removed** — same |
| `RefundabilitySummary` | Sale-time refundability summary | Redisplay; servicing **pre-screen only** | ACL translation of coarse source evidence | **New / Keep** |
| `ChangeabilitySummary` | Sale-time changeability summary | Same | Same | **New / Keep** |
| `UpgradeEligibilitySummary` | Sale-time upgrade-eligibility summary | Same | Same | **New / Keep** |
| `SourceSystem` | Who supplied the terms evidence | Audit; interpreting the summary's provenance | ACL constant | **Refine** (was `PolicySource`) |
| `SourcePolicyReference` | Opaque upstream rule/policy reference | Audit; P2-C/P3 can resolve richer rules from it | `AirFareId` | **Refine** (was `SourceRuleReference`) |
| `SourcePolicyVersion` | Version of the accepted policy | Audit; only stored when actually supplied | none today → null | **Keep as nullable** |
| `TermsCapturedAt` | When the terms were accepted | Historical audit | clock | **Keep** |

**Binding constraint recorded in code and test:** the summaries are *historical accepted-sale evidence*. They
may inform display and servicing pre-screen. They **must not** authorize an irreversible refund/exchange/change.
P3 must still obtain the authoritative servicing quote/evidence. A test asserts that no
`OrderAggregate.Policies` eligibility policy takes `OrderItemCommercialTermsSnapshot` as input.

**No speculative rule model was added.** Penalty amounts, before/after-departure rules, no-show rules, waiver
authority, residual value and refund calculation are all absent — no authoritative source supplies them and no
Ordering use case requires them today.

## 5. `AcceptedAirServiceDetail`

| Field | Ordering meaning | Actual consumer / use case | Source | Decision |
|---|---|---|---|---|
| `FareReference` | Opaque fare identifier for the sold air service | `OrderAirTransportService.AirFareId`; P1 reserve/issue correlation | `AirFareId` | **Keep** — opaque reference |
| `FareBasis` | Fare-construction context | `OrderAirTransportService.FareBasis`, read by **P1 ETKT issuance** | `FareBasis` | **Keep, transitional** — authoritative ownership moves to `FareComponent` in **P2-C** |
| `FareFamily` | Brand label carried onto the legacy service | `OrderAirTransportService.FareFamilyTitle` | `FareFamily` | **Keep, transitional** — P2-D decides the final home |
| ~~`FareNumber`~~ | — | none; the ACL never supplied a value | none | **Removed** — fails the practicality gate outright |
| ~~`IsChangeable`~~ | — | — | — | **Removed** — air service must not own fare-family commercial policy |
| ~~`IsRefundable`~~ | — | — | — | **Removed** |
| ~~`IsUpgradable`~~ | — | — | — | **Removed** |
| `CheckedBaggage` | Included checked allowance sold with the air service | `OrderAirTransportService.Baggage` (P1 field) | fare component | **Keep, transitional** — the real baggage model is **P2-D** |
| `CabinBaggage` | Included cabin allowance | `OrderAirTransportService.CabinBaggage` (P1 field) | fare component | **Keep, transitional** |

The legacy `OrderAirTransportService.IsRefundable/IsChangeable/IsUpgradable` columns still exist (P1 schema).
They are no longer fed by a per-service accepted input; the Domain **derives** them from the item's commercial
term summary (`Permitted ⇒ true`). A `Conditional` or `Unknown` summary therefore does not grant the legacy
permission flag — pinned by test.

## 6. Transitional / non-normative representations

These remain only because the P1 vertical slice depends on them, and are explicitly **not** the target model:

| Representation | Why it remains | Removed/replaced by |
|---|---|---|
| `OrderAirTransportService.FareBasis` | P1 ETKT issuance reads `service.FareBasis` at issue time | **P2-C** — issuance must take issue-time fare context from the fare-construction/service association |
| `OrderAirTransportService.FareFamilyTitle` | P1 column, redisplay | **P2-D** |
| `OrderAirTransportService.Baggage` / `CabinBaggage` | P1 columns; minimum P1-compatible baggage representation | **P2-D** — included allowance vs separately priced baggage |
| `OrderAirTransportService.IsRefundable/IsChangeable/IsUpgradable` | P1 columns | **P3** — servicing reads authoritative quotes, not sale-time booleans |
