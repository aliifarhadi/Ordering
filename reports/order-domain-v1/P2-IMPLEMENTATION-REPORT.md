# P2 — Pricing, Fare Construction, Ancillary Catalogue & EMD

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Started:** 2026-09-08
Entry gate: [`audit/p2/P2-ENTRY-AND-MAPPING.md`](audit/p2/P2-ENTRY-AND-MAPPING.md) · Evidence: [`audit/p2/P2-TEST-RUN.txt`](audit/p2/P2-TEST-RUN.txt), [`audit/p2/P2-A.1-TEST-RUN.txt`](audit/p2/P2-A.1-TEST-RUN.txt), [`audit/p2/P2-B-TEST-RUN.txt`](audit/p2/P2-B-TEST-RUN.txt), [`audit/p2/P2-B.1-TEST-RUN.txt`](audit/p2/P2-B.1-TEST-RUN.txt), [`audit/p2/P2-C-TEST-RUN.txt`](audit/p2/P2-C-TEST-RUN.txt), [`audit/p2/P2-C.1-TEST-RUN.txt`](audit/p2/P2-C.1-TEST-RUN.txt), [`audit/p2/P2-D-TEST-RUN.txt`](audit/p2/P2-D-TEST-RUN.txt), [`audit/p2/P2-D.1-TEST-RUN.txt`](audit/p2/P2-D.1-TEST-RUN.txt), [`audit/p2/P2-E-TEST-RUN.txt`](audit/p2/P2-E-TEST-RUN.txt), [`audit/p2/P2-E.1-TEST-RUN.txt`](audit/p2/P2-E.1-TEST-RUN.txt), [`audit/p2/P2-F-TEST-RUN.txt`](audit/p2/P2-F-TEST-RUN.txt), [`audit/p2/P2-G-TEST-RUN.txt`](audit/p2/P2-G-TEST-RUN.txt), [`audit/p2/P2-G.1-TEST-RUN.txt`](audit/p2/P2-G.1-TEST-RUN.txt)

| Sub-phase | Status |
|---|---|
| **P2-A** pricing foundation | **Complete / Frozen** |
| **P2-A.1** pricing foundation correctness closure | **Complete — P2-A frozen** |
| **P2-B** accepted source normalization | **Complete** |
| **P2-B.1** domain semantic decoupling | **Complete — P2-B frozen** |
| **P2-C** AirFareConstruction | **Complete** |
| **P2-C.1** fare construction scope resolution | **Complete — P2-C frozen** |
| **P2-D** OrderService composition & ancillary domain | **Complete** |
| **P2-D.1** price-treatment & referential-integrity closure | **Complete — P2-D frozen** |
| **P2-E** idempotent add-service commercial mutation | **Complete — P2-D.1 frozen** |
| **P2-E.1** benchmark-aligned Order Change / Add Service | **Complete — P2-E frozen** |
| **P2-F** ElectronicMiscDocument | **Complete — P2-E.1 frozen** |
| **P2-G** OrderView, projection & pricing-change events | **Complete — P2-F frozen** |
| **P2-G.1** OTA order ownership / resource authorization closure | **Complete — P2-G frozen** |
| P2-H verification | Not started |

---

## P2-A — Pricing foundation

**Complete.** The old *Credit-means-sale* polarity is gone from the active model, and every monetary fact
now enters the Order through one immutable, validated, change-set-scoped ledger.

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **92 passed**, 0 failed (69 baseline + 23 new P2-A) |
| `AeroTech.Ordering.Persistence.Tests` | **128 passed**, 0 failed (real SQL Server) |
| Total | **220 passed, 0 failed** — the full P0–P1.2 baseline is intact |

### 1. The polarity replacement

`Direction` is now the only source of sign: `Debit = +1`, `Credit = -1`, magnitudes always non-negative,
`CustomerTotal = Σ signedSale where Effect = CustomerBalance`. The create path that previously wrote every
sale as `Credit` now writes `Debit`.

The published `OrderIssued` / `OrderCancelled` / `OrderDocumentVoided` / `OrderSplit` contracts still carry
the old enum, so the inversion is **translated at the publish adapter** — `LegacyPricingLineTranslation` maps
the new `ComponentType`/`Direction` back to `OrderPricingLineCategory` and the legacy polarity — exactly as
`02` §3.3 requires ("mapped at its adapter, never reused without translation"). No consumer sees a silently
flipped sign, and the wire contract is re-versioned in P2-G, not here.

### 2. New model (all in the one Ordering domain — no `V2` types)

`OrderChange` (stable `ChangeId` per accepted commercial mutation) → `OrderPriceChangeSet` (immutable after
commit, carries `FinancialSequence`, reason, source, `ExpectedCommercialVersion`) → `OrderPricingLine`
(replaced in place) → `OrderPricingAllocationSet` → `OrderPricingAllocation`.

`OrderPricingLine` gained `ComponentType`, `Effect`, `LineRole`, `OriginalAmount`/`SaleAmount` with their own
currencies, `ApplicationLevel`/`Quantity`/`UnitOfMeasure`/`UnitPrice`, `BasisType`/`BasisReferenceId`,
`SourceLineRef`, `TransferGroupId`, `RelatedOperationId`, `CalculationSnapshot`, `TaxDetails`,
`SettlementPartyRef`/`SettlementCategory`. It lost `LineScope`, `LineSubCategory`, `LineReason` (reason moved
up to the change set) and `IsPercentage`.

Eleven enums were added under the existing convention in `Contracts/AeroTech.Messages/Ordering/Enums`, one
per file with `Display` names. No enum was moved.

### 3. Rules now enforced in the domain

`PricingComponentPolicy` implements the complete `02` §3.4 matrix: `Tax + SettlementOnly` is rejected
outright (2751), commission can never touch the customer balance (2752), a discount is `Credit` and a
fare/tax/fee/surcharge/markup/penalty is `Debit` as an original (2754), `Adjustment`/`Other` require a code
(2755), and a settlement-only line requires an explicit party and category (2756).

Reversals require the original line, the opposing direction, identical component/effect/currencies, and are
capped by the outstanding value in **both** currencies (2757–2760). A lineage link alone is not a reversal.

Allocation sets belong to exactly one line, purpose and version, and reconcile per `02` §4.2: `Complete`
must equal the parent exactly, `Partial` may not exceed it, `Unavailable` must be empty — no invented
shares (2761–2766). Allocations are never added to totals.

`SourceLineRef` is a deterministic composite of offer, traveller, bound, flight and code, so two genuine
occurrences of the same tax code stay distinguishable. *(Superseded by P2-A.1: the occurrence moved into a
separate `OccurrenceKey` and uniqueness is now scoped to the price change set.)*

### 4. Totals, commission and versions

`OrderAmount` is now a **derived cache** recomputed from the ledger in the same transaction as the mutation,
and it gained `AncillaryTotal` (ProductCharge). `Order.CustomerTotal` is the authoritative derived value.

`Commission` stopped calculating. It was `totalAmount * rate / 100` folded into the order total; it is now a
pure carrier whose amount comes from `ComponentType = Commission` ledger lines, which are `SettlementOnly` by
default and therefore do **not** reduce `CustomerTotal` (`02` §7.8).

`FinancialSequence` was added to `Order`, advancing exactly once per committed `PriceChangeSet`, independent
of `CommercialVersion`. `ObligationVersion` advances only when the committed change actually moves
`CustomerTotal` — a settlement-only line does not touch it. `OwnerAirlineId` was added to `Order` and is
supplied by `IHomeOperatorProvider` at the application boundary, **not** through `CreateOrderArgs`, because
it is trusted operator context and never client input.

### 5. Source classification change — confirmed

`AirChargeKind.Surcharge` previously collapsed into `OrderPricingLineCategory.Fee`. It now maps to
`ComponentType.CarrierSurcharge` per `02` §3.2/§7.3 (YQ/YR must not be Fare or Tax). Raised for confirmation
at P2-A and **accepted by the owner for the current coarse AirPrice contract**; the subject is closed. P2-B
moves this interpretation out of Domain code into the accepted-source normalizer and retains the original
source kind/code/reference there, so a richer future AirPrice classification changes the mapper rather than
Ordering's pricing semantics.

### 6. Persistence

New tables `OrderChanges`, `OrderPriceChangeSets`, `OrderPricingAllocationSets`, `OrderPricingAllocations`;
`OrderPricingLineAllocations` dropped. Unique indexes on `(OrderId, FinancialSequence)` and
`(OrderPricingLineId, Purpose, Version)`, plus a filtered unique source-line index *(re-scoped by P2-A.1 to
`(PriceChangeSetId, SourceLineRef, OccurrenceKey)`)*.

Migration `P2PricingFoundation` was **hand-corrected before it was applied**: EF scaffolded ten
`RenameColumn` operations that matched old and new columns *by CLR type rather than by meaning* — it would
have turned `LineCategory` into `Direction`, `EquivalentCurrencyId` into `ComponentType` and `CurrencyId`
into `BasisType`, silently reinterpreting existing rows as garbage. Each was rewritten as an explicit
`DropColumn` + `AddColumn` pair (and the `Down` section symmetrised) so no value is carried across a
semantic boundary. Applied to `DotAirOrderNew`.

### 7. Legacy paths kept compiling, not extended

`Order.Cancel`, `Order.Termination` and `Order.Split` were migrated onto the new ledger rather than left
behind: cancellation and void now build bounded, allocation-aware `Reversal` lines through
`CommitPriceChange`, which removes the old hand-rolled `IsLineReversed`/`IsAllocationReversed` scan.

**Known deviation carried into P3:** the legacy split still physically moves value between orders. It now
does so as `LineRole = Transfer` lines committed under a `SplitTransfer` change set on the child (which at
least preserves referential integrity and change-set lineage), but `02` §10.3 requires a *balanced pair* of
opposite-direction transfer lines on both orders under one `TransferGroupId`. Split is out of P2 scope; this
is recorded as P3 work.

### 8. Tests added

`tests/AeroTech.Ordering.Domain.Tests/P2/PricingFoundationTests.cs` — 23 cases pinning: sale-is-debit and
the signed customer total; the totals cache; discount polarity in both directions; settlement-only
commission; the tax and commission prohibitions; informational exclusion; settlement party requirement;
non-negative magnitudes; reversal lineage, opposition and outstanding-value capping; source-line duplicate
rejection; financial-sequence advance and change-set immutability; empty change set refusal; obligation
version movement; the three allocation completeness rules; sale-currency coherence; and that allocations
never inflate the customer total.

---

## P2-A.1 — Pricing foundation correctness closure

**Complete. P2-A is frozen.** An independent review of the pushed P2-A code found six pricing-foundation
correctness gaps; all six are closed and the exit gate is fully met. Full detail, defect by defect, is in
[`audit/p2/P2-A.1-CORRECTNESS-AUDIT.md`](audit/p2/P2-A.1-CORRECTNESS-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **116 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **141 passed**, 0 failed (real SQL Server) |
| Total | **257 passed, 0 failed** (baseline 220 → +37) |

1. **`CommitPriceChange` is now exception-atomic.** It was mutating the aggregate — appending the
   `OrderChange`, burning a `FinancialSequence` and attaching the change set — before validating the lines.
   It is now split into a pure `StagePriceChange` (constructs and validates everything, touching no aggregate
   state) and `AttachPriceChange` (attaches, assigns the sequence, commits, recomputes caches). A rejected
   monetary mutation now leaves `Changes`, `PriceChangeSets`, `PricingLines`, `FinancialSequence`,
   `ObligationVersion` and both caches exactly unchanged. Multi-line candidates validate against their staged
   siblings, so two reversals of one original in a single candidate are capped jointly. No second unit of
   work or transaction framework was added.

2. **Reversal provenance is enforced.** A reversal may no longer target another reversal (2772), must carry
   the original's accepted `ExchangeRate` unchanged when it had one (2773) — today's rate cannot be
   substituted — must supply a non-zero original-currency amount while original value is outstanding (2774),
   and must close the outstanding original exactly when it closes the outstanding sale value (2775). Ordering
   still never derives a partial original amount itself.

3. **Source occurrence identity is explicit.** `OccurrenceKey` was split out of the synthetic
   `SourceLineRef`, and uniqueness moved from `(OrderId, SourceLineRef)` — which wrongly blocked a later
   authoritative decision from referencing the same source line — to
   `(PriceChangeSetId, SourceLineRef, OccurrenceKey)`, in both the domain check and a SQL unique index.

4. **`OrderChange` history reloads.** `OrderRepository.AggregateQuery()` mapped but never included `Changes`,
   so reloaded Orders had an empty commercial change history.

5. **Commission demotion finished.** `Order.Create` no longer writes the caller-supplied `CommissionRate` as
   authoritative. Rate and amount now come only from accepted `Commission` pricing facts; no rate is
   back-calculated from amounts, and commission still never moves `CustomerTotal`.

6. **Allocation completeness finished.** Partial sets now persist an explicit residual
   (`ResidualSaleAmount`/`ResidualSaleCurrencyId`, plus `IsFullyAttributed`), and supplied original-currency
   allocations must be complete-or-absent (2777), currency-coherent, and reconcile exactly when `Complete`.
   Nothing is fabricated when the source supplies no original breakdown.

Persistence: the already-applied `P2PricingFoundation` migration was not touched. A new additive
`P2A1PricingProvenance` migration adds `OccurrenceKey`, the corrected index and the residual columns, with no
renames and no reinterpretation of existing values.

Two P2-A tests were rewritten because P2-A.1 deliberately changes their expected behaviour (global
source-reference rejection, and a reversal supplying `OriginalAmount = 0`); neither assertion was weakened.

---

## P2-B — Accepted source normalization & commercial snapshots

**Complete.** Full detail in [`P2-B-NORMALIZATION-REPORT.md`](P2-B-NORMALIZATION-REPORT.md); the field-by-field
source classification is in [`audit/p2/P2-B-SOURCE-NORMALIZATION-MAPPING.md`](audit/p2/P2-B-SOURCE-NORMALIZATION-MAPPING.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **141 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **162 passed**, 0 failed (real SQL Server) |
| Total | **303 passed, 0 failed** (baseline 257 → +46, zero regressions) |

AirPrice vocabulary now terminates at the ACL. `OfferDetail`/`OfferReader` and the whole offer graph moved
from the Domain into `AeroTech.Ordering.Providers.Offer.Model` (one record per file), a new
`AirPriceOfferNormalizer` translates them into the Ordering-owned `AcceptedOrderSource`, and `IOfferProvider`
now returns that accepted source rather than a provider DTO. `Order.Create` consumes only Ordering semantics
and resolves source-local refs (`JourneyRef`/`SegmentRef`/`ProductRef`/`ServiceRef`/`TravellerRef`) to
generated ids; there is exactly one active creation path.

Both silent defaults are gone and both moved to the boundary: an unknown or unresolvable source charge kind
fails closed (2785) instead of becoming a Fee, and a supplied baggage allowance with an unknown or missing
unit fails closed (2786) instead of becoming kilograms. `WeightUnit.Kg` no longer appears as a literal in
`src`. The closed `Surcharge → CarrierSurcharge` mapping is unchanged and now lives only in the normalizer.

`OrderItem` gained two immutable accepted-sale snapshots — `OrderItemProductSnapshot` (what product was
accepted) and `OrderItemCommercialTermsSnapshot` (what customer-facing terms were accepted) — kept
semantically distinct from the pre-existing `OrderItemPolicySnapshot` (how Ordering operationally treats the
item), which was not touched. Migration `P2BAcceptedSourceSnapshots` is purely additive; the P2-A and P2-A.1
migrations were not modified. P2-A pricing facts pass through normalization unreinterpreted, including the
`SourceLineRef` / `OccurrenceKey` split.

---

## P2-B.1 — Domain semantic decoupling

**Complete. P2-B is frozen.** An architecture review found that P2-B removed physical AirPrice DTO coupling
while leaving semantic coupling to the current AirPrice Fare/FareFamily shape. Full detail in
[`P2-B.1-SEMANTIC-DECOUPLING-REPORT.md`](P2-B.1-SEMANTIC-DECOUPLING-REPORT.md); the field-by-field
keep/refine/remove decisions are in
[`audit/p2/P2-B.1-DOMAIN-SEMANTIC-AUDIT.md`](audit/p2/P2-B.1-DOMAIN-SEMANTIC-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **157 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **171 passed**, 0 failed (real SQL Server) |
| Total | **328 passed, 0 failed** (baseline 303 → +25, zero regressions) |

The Order Domain now has **zero** `AeroTech.Messages.AirPrice.*` references — the P2-B allow-list exception for
`PassengerTypeCode` and `WeightUnit` is gone, replaced by Ordering-owned vocabulary (`PassengerTypeCode`,
`BaggageWeightUnit`, `JourneyType`, `CommercialTermState`) that the ACL translates into, failing closed on an
unmapped value. No sibling service was changed and provider wire payloads are byte-identical.

The rejected product mapping is corrected: `AirFareId` is opaque provenance and not a `ProductCode`, `FareBasis`
is never a `ProductName`, and `FareFamily` becomes `BrandName` only because the ACL explicitly treats it as the
customer-visible brand label. Absent product code/name now stay null rather than being synthesized.

`CommercialTermsSnapshot` replaced the provider-shaped booleans with Ordering-owned
`Refundability`/`Changeability`/`UpgradeEligibility` summaries over `Unknown | Prohibited | Permitted |
Conditional`, so richer future fare rules map into `Conditional` without a schema change. The summary is
display/pre-screen evidence only and cannot authorize servicing — asserted by test. Baggage was removed from the
terms snapshot (it was persisted twice and is not change/refund policy) and `AcceptedAirServiceDetail` lost its
refund/change/upgrade policy and the unused `FareNumber`; the legacy service permission flags are now derived
from the item's summary. No speculative fare-rule or fare-family model was introduced.

Corrective migration `P2B1DomainSemanticDecoupling` was added rather than rewriting the applied
`P2BAcceptedSourceSnapshots`; its scaffolded renames were verified meaning-preserving, and EF's invalid `0`
default for the new enum columns was corrected by hand to `Unknown`.

---

## P2-C — Standard air fare construction

**Complete.** Full detail in [`P2-C-FARE-CONSTRUCTION-REPORT.md`](P2-C-FARE-CONSTRUCTION-REPORT.md); the
field-by-field justification is in
[`audit/p2/P2-C-FARE-CONSTRUCTION-SEMANTIC-AUDIT.md`](audit/p2/P2-C-FARE-CONSTRUCTION-SEMANTIC-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **192 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **177 passed**, 0 failed (real SQL Server) |
| Total | **369 passed, 0 failed** (baseline 328 → +41, zero regressions) |

**Final P2-B closure:** the ACL was assigning `AirFareId` to both `SourcePolicyReference` and
`SourcePricingReference`. The current source supplies neither, so both are now `null`; only
`SourceProductReference` keeps `AirFareId` as opaque provenance. The remaining nullable provenance fields were
reviewed under the same rule and stay null.

**Model:** `Order → OrderAirFareConstruction[] → OrderFarePricingGroup[] → OrderFarePricingUnit[] →
OrderFareComponent[]`, Order-owned and immutable, with explicit membership tables for construction↔item,
component↔service and component↔segment. Construction type, pricing-unit type and combination method are all
nullable and stored only when the source states them — nothing is inferred from itinerary geometry. True RT vs
OW+OW is expressed structurally (one RT unit with two components versus two OW units), and a through fare is a
single component covering several services and segments.

Fare construction carries no money: accepting one moves neither `CustomerTotal`, `FinancialSequence`,
`ObligationVersion` nor `CommercialVersion`, and ticket value attribution still flows only through
`DocumentPriceLink → PricingLine`.

**Current AirPrice emits no construction at all** — it cannot defensibly supply pricing-unit boundaries — and an
Order with none is valid. **ETKT** now resolves issue-time fare basis through `Order.ResolveIssueFareBasis`,
using `FareComponent.FareBasis` when a construction exists and otherwise the explicit transitional service-field
fallback; both paths are tested.

Migration `P2CAirFareConstruction` adds eight relational tables; a follow-up `P2CIgnoreComputedFareComponents`
removes a spurious shadow FK EF generated from a computed convenience property. No previously applied migration
was edited.

---

## P2-C.1 — Fare construction scope resolution

**Complete. P2-C is frozen.** Full detail in
[`P2-C.1-SCOPE-RESOLUTION-REPORT.md`](P2-C.1-SCOPE-RESOLUTION-REPORT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **202 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **178 passed**, 0 failed (real SQL Server) |
| Total | **380 passed, 0 failed** (baseline 369 → +11, zero regressions) |

P2-C resolved fare context through a global-singular `CurrentFareConstruction()` chosen by `CreatedAt`, then
searched only inside it. An Order may legitimately hold several simultaneously active, independent
constructions covering different commercial scopes, so any service outside the newest construction silently
fell through to the transitional legacy `FareBasis` — a wrong answer disguised as a valid absence.

`CurrentFareConstructions()` now returns **all** non-superseded constructions (supersession was already
branch-specific, so `A1→A2` alongside an untouched `B1` yields `{A2, B1}`), and `ActiveFareComponentFor`
searches every current construction by actual service membership: zero matches → `null` and the transitional
fallback; exactly one → authoritative; more than one → **fails closed** with reason code 2807. Nothing is
picked by newest/first/highest-id/latest-`CreatedAt`, and no construction is auto-superseded to break a tie.
`ResolveIssueFareBasis` inherits this, so the legacy fallback is reserved for absence and is never reached
through conflict.

`AcceptedFareConstruction` gained one optional `SupersedesConstructionRef` so lineage can be expressed at
acceptance and tested; the P3 reissue workflow is still not implemented. No other fare-construction semantics
changed, and there is no schema change or migration.

---

## P2-D — OrderService composition & practical ancillary domain

**Complete. P2-C.1 is frozen.** Full detail in
[`P2-D-SERVICE-MODEL-REPORT.md`](P2-D-SERVICE-MODEL-REPORT.md); binding field-by-field justification in
[`audit/p2/P2-D-SERVICE-SEMANTIC-AUDIT.md`](audit/p2/P2-D-SERVICE-SEMANTIC-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **322 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **194 passed**, 0 failed (real SQL Server) |
| Total | **516 passed, 0 failed** (baseline 380 -> +136, zero regressions) |

`OrderService` was an abstract base with one EF-TPT subclass, `OrderAirTransportService`, so every air-specific
fact - traveller, segment, fare basis, seat, baggage - lived on the only subclass that existed and every new
ancillary would have meant another subclass and another table. P2-D replaces inheritance with **composition**:
a concrete `OrderService` plus **exactly one** typed detail (air, seat, baggage, meal, lounge, hotel, ground
transport, generic), plus beneficiaries and stated coverage. No type derives from `OrderService` anywhere in
the Domain assembly, and `UseTptMappingStrategy()` is gone.

`OrderServiceBeneficiary` replaces the single `TravellerId` - air and seat services must have exactly one
beneficiary (2822), a hotel room or a car may have several, and `SoleBeneficiaryId` fails closed rather than
returning the first. Coverage is **stated, never inferred**: `OrderServiceCoveredService` /
`OrderServiceCoveredSegment` stay empty when the source says nothing, and a covered-air reference resolving to
a non-air service is rejected. `OrderItemServiceLink` preserves the item a service belonged to when it was
created together with the `OrderChange` that created the link, so a later item move cannot erase commercial
history.

`ServicePriceTreatment` (SeparatelyPriced / Included / Complimentary / SupplierOpaque) records commercial
treatment and is **not** a financial status - an included bag is a real service with a real detail, real
beneficiaries and real coverage that simply owns no pricing line, and no zero-amount line is invented to make
it look sold. Generic services (Priority, WiFi, Cip, SimCard, ExtraSeat, SpecialAssistance) are governed by
`GenericServiceSchemaRegistry`, which fails closed on unregistered schema (2826), unsupported version (2827),
missing or malformed attributes (2828) and schema/service-type mismatch (2824), and whose **schema supplies the
fulfilment profile** so a source cannot claim a Wi-Fi voucher needs an electronic ticket. Financial
pseudo-services (Penalty, ServiceFee, Credit, Voucher, TaxAdjustment, ManualAdjustment, Notification,
TransferRide) cannot be sold (2820) - money lives on `OrderPricingLine`, a voucher is a tender, and
`TransferRide` is superseded by first-class `GroundTransport`.

Legacy air baggage evidence was neither fabricated into baggage services nor dropped: it is preserved on the
air detail as `TransitionalCheckedBaggage` / `TransitionalCabinBaggage`, named for what it is, and a test
proves it never materialises a `BaggageAllowance` service.

Migration `P2DServiceComposition` adds the new columns and tables and **backfills** air details, beneficiaries
and item-service links from the legacy subclass table before dropping it - the scaffolded migration dropped it
first and was hand-corrected, and the scaffolded `PriceTreatment` default of `0` (not a valid member) was
corrected to `1`. No previously applied migration was edited. Every detail table has a real FK; there are no
polymorphic or nullable fake FKs.

---

## P2-D.1 — Service price-treatment & referential-integrity closure

**Complete. P2-D is frozen.** Full detail in
[`P2-D.1-CLOSURE-REPORT.md`](P2-D.1-CLOSURE-REPORT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **324 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **225 passed**, 0 failed (real SQL Server) |
| Total | **549 passed, 0 failed** (baseline 516 -> +33, zero regressions) |

Two correctness areas were closed.

**PriceTreatment was hardcoded.** The AirPrice ACL stamped `SeparatelyPriced` on every air service at creation
time, before any pricing evidence existed - existence alone was treated as proof of an independent product
price. `PriceTreatment` now describes the commercial relationship between a service and accepted price
evidence, and is decided after normalization: an **Original**, **CustomerBalance**, **primary** component
(`Fare` or `ProductCharge`) at `BasisType = OrderService` for that service makes it `SeparatelyPriced`; the
same kind of line at `BasisType = OrderItem` on the parent product makes it `Included`; neither makes it
`SupplierOpaque`; `Complimentary` is never inferred. A service-scoped `Tax`, `CarrierSurcharge` or `Fee` is not
primary value evidence, so an item-priced round trip with per-segment taxes leaves both air services
`Included` and counts the item price exactly once. The rule lives in `ServicePriceTreatmentResolver` in the
ACL; `AcceptedProductBuilder` now accumulates pending descriptors and materializes `AcceptedService` once the
traveller's pricing facts are known. The Domain still never sees an AirPrice DTO, and the change creates,
deletes or splits no `PricingLine` and moves no `CustomerTotal`, `FinancialSequence` or `ObligationVersion`.

**Target references had no foreign keys.** Ten references - service to current item, beneficiary to traveller,
covered segment, covered service, all three `OrderItemServiceLink` targets including `LinkedByChangeId`, air
detail segment, seat detail associated service and lounge related-air service - are now real FKs configured
without adding navigation properties, all `DeleteBehavior.NoAction` so the aggregate root keeps the single
cascade path and SQL Server raises no multiple-cascade-path error. The database proves the target exists; the
Domain still proves it belongs to the same Order and has the required semantic type.

Migration `20260908134158_P2D1ServiceTreatmentAndIntegrity` re-derives persisted `PriceTreatment` from the P2-A
ledger using the real columns and enum values (never overwriting an explicit `Complimentary`), then runs ten
named orphan pre-checks that `THROW` on corrupt historical data before adding the FKs - nothing is deleted,
repointed or invented. `P2DServiceComposition` was not edited. The corrective statement lives in
`P2D1ServicePriceTreatmentBackfill.Sql` so the migration and its tests execute identical text.

---

## P2-E — Idempotent AddProduct commercial mutation

**Complete. P2-D.1 is frozen.** Full detail in
[`P2-E-ADD-PRODUCT-REPORT.md`](P2-E-ADD-PRODUCT-REPORT.md); binding audit in
[`audit/p2/P2-E-COMMERCIAL-MUTATION-AUDIT.md`](audit/p2/P2-E-COMMERCIAL-MUTATION-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **399 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **250 passed**, 0 failed (real SQL Server) |
| Total | **649 passed, 0 failed** (baseline 549 -> +100, zero regressions) |

The first real post-creation commercial mutation. One durable idempotent operation produces exactly one
`OrderChange(AddProduct)`, one new `OrderItem` with its product and commercial-terms snapshots, one or more new
`OrderServices` with immutable `OrderItemServiceLinks`, and one committed `PriceChangeSet(AddProduct)` carrying
the accepted pricing evidence - advancing `CommercialVersion` once, `FinancialSequence` once, and
`ObligationVersion` only when `CustomerTotal` actually moves.

**Atomicity.** `Order.AddProduct` follows the P2-A two-phase shape: `StageProductAddition` builds and validates
the whole candidate - change, item, snapshots, every service with beneficiaries, typed detail and coverage,
membership links, mapped pricing lines and allocation sets - while the aggregate is untouched;
`AttachProductAddition` then attaches, commits, activates only the new services and versions once, with no
validation left to fail. A rejected addition leaves every collection, cache, summary and counter identical.

**Idempotency.** The P0 stack is reused unchanged (`ServicingOperationKind.AddProduct = 9` appended).
`OrderChange.OperationId` is the persisted commercial recovery proof, and replay resolves from persisted order
state - a fresh process retrying the same key returns the same change, item and service ids without calling the
provider. Replay recognition runs before the expected-version check, so a retry after an unrelated later
mutation still resolves the original addition. Migration `P2ECommercialOperationUniqueness` adds the filtered
unique index on `OrderChanges(OrderId, OperationId)` behind a fail-closed duplicate pre-check.

**Boundary.** One new semantic port, `IAcceptedProductAdditionPort`, carrying only Ordering-owned values and
returning the Ordering-owned `AcceptedProductAddition`. The public command is just
`OrderId + SourceReference + ExpectedCommercialVersion + Idempotency-Key`. A deterministic test double and a
fail-closed `UnconfiguredProductAdditionProvider` are the only implementations; no fake production ancillary ACL
was written and no sibling repository was touched.

**Scope guards.** `AirTransportation` cannot be added, so no journey, segment, traveller or fare construction is
ever created; financial pseudo-products and pseudo-services stay blocked and order-level fees remain pricing
lines; reversals are rejected; commission and tax stay source-owned; nothing is reserved, documented or
EMD-issued, and the existing electronic ticket is provably untouched. Bundle pricing moves the total once and
invents no per-service allocation. `ProductType.Ancillary` was appended for honest generic categorisation.

**Two defects fixed on the way.** Legacy ticketing treated every `RequiresDocument` service as one completion
scope, so a pending EMD ancillary would have made an already ticketed air order look unticketed; completion is
now scoped to `DocumentKind == ElectronicTicket`. And `OrderItemPolicySnapshot`, which an exhaustive search
showed has no reader anywhere and whose every field is now owned by `OrderService`, became optional rather than
having a false `AirTransportPolicy()` stamped on hotel or baggage items.

---

## P2-E.1 — Benchmark-aligned Order Change / Add Service

**Complete. P2-E is frozen.** Full detail in
[`P2-E.1-BENCHMARKED-ORDER-CHANGE-REPORT.md`](P2-E.1-BENCHMARKED-ORDER-CHANGE-REPORT.md); scope audit in
[`audit/p2/P2-E.1-BENCHMARK-AND-SCOPE-AUDIT.md`](audit/p2/P2-E.1-BENCHMARK-AND-SCOPE-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **399 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **272 passed**, 0 failed (real SQL Server) |
| Total | **671 passed, 0 failed** (baseline 649 -> +22, zero regressions) |

P2-E's internal mutation was correct but it was exposed under an invented business vocabulary: `AddProduct`
named the implementation shape as if it were an airline operation, and the request carried an arbitrary
`SourceReference`. No industry standard or PSS benchmark has a post-sale "AddProduct" operation.

The public operation is now **Order Change**, with **Add Service** as its only implemented use case:
`POST /Backoffice/v1/Orders/{OrderId}/Change` and `POST /Api/v1/Bookings/{OrderId}/Change`, both invoking one
`IOrderChangeService.AddServiceAsync`. The request represents acceptance of an already quoted offer -
`AcceptSelectedQuotedOfferList` with `QuotedOfferId` and `SelectedOfferItemIds` - and the response is the
updated order from Ordering's own projection plus `OperationId` and `CommercialVersion`. Price, tax,
commission, snapshots, service detail internals and attributes JSON cannot be sent by the caller. The
`/AddProduct` endpoints and their request DTOs were deleted, not aliased.

`IAcceptedProductAdditionPort` became `IOrderChangeQuoteProvider`, resolving `AcceptedQuotedOfferSelection` into
`AcceptedAddServiceChange` and nothing else. An expired or rejected quote fails the change (2862) with no
mutation - Ordering never reprices locally. More than one selected offer item is rejected explicitly (2864)
before the provider is called, documented as a current subset of the standard flow rather than a different
business flow. **ServiceList, SeatAvailability and OrderQuote remain upstream capabilities outside Ordering.**

`ServicingOperationKind` 9 was renamed `AddProduct` -> `AddService` (value preserved). `OrderChangeType.AddProduct`
and `PriceChangeReason.AddProduct` keep their persisted values and are recorded as internal historical labels,
not public vocabulary. Internal domain members were deliberately not renamed. Ancillary and SSR are kept
distinct: no SSR code or status was invented, `GenericService` is explicitly not the permanent SSR model, and a
first-class `SpecialServiceRequest` is deferred to its own benchmarked slice. Every P2-E internal correctness
property - atomicity, one-mutation invariants, the three counters, operation recovery, price-treatment
evidence, ETKT document-family scope - is unchanged.

### Standing decision — Airline Flow Benchmark Rule

```
Ordering public business flows and vocabulary must be traceable
to an industry standard or established PSS benchmark.

Internal implementation abstractions do not justify creation
of new airline business operations.

Any intentional deviation requires:
- benchmark compared,
- limitation identified,
- reason for deviation,
- practical benefit,
- compatibility impact.
```

Before adding any public command, endpoint or message, answer "what established airline/PSS flow does this
correspond to?" - if there is no defensible answer, do not expose it. Internal domain terminology may differ
where useful; public business vocabulary must not invent a parallel airline workflow.

---

## P2-F — Benchmark-aligned Electronic Miscellaneous Document

**Complete. P2-E.1 is frozen.** Full detail in
[`P2-F-EMD-IMPLEMENTATION-REPORT.md`](P2-F-EMD-IMPLEMENTATION-REPORT.md); benchmark and scope audit in
[`audit/p2/P2-F-EMD-BENCHMARK-AND-SCOPE-AUDIT.md`](audit/p2/P2-F-EMD-BENCHMARK-AND-SCOPE-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **436 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **309 passed**, 0 failed (real SQL Server) |
| Total | **745 passed, 0 failed** (baseline 671 -> +74, zero regressions) |

The EMD is modelled as an accountable **fulfilment artefact**, never as commercial truth: issuance advances no
`CommercialVersion`, `FinancialSequence` or `ObligationVersion` and creates no `OrderChange`, `PriceChangeSet`,
`PricingLine`, item or service. `ElectronicMiscDocument` is its own aggregate with `EmdCoupon[]` and
`EmdPriceLink[]`; it does not inherit `ElectronicTicket` and no `TrafficDocumentV2` was created.

**EMD-A / EMD-S** are distinct: an associated document requires a ticket-coupon association on every coupon,
a standalone document rejects one, and the type is supplied by the accepted quote - never inferred from
`ServiceType` or `ProductType`. **RFIC** lives once on the document and **RFISC** per coupon, both opaque
validated codes from the quote, never derived from `ProductType` or `ServiceCode`; one document carries exactly
one RFIC and mixed codes fail closed.

The new immutable `OrderServiceEmdIssuanceSnapshot` carries the accepted issuance evidence from Add Service;
the public Order Change contract is unchanged and still cannot supply RFIC, RFISC, EMD type, association or
document number. **EMD-A association** is coupon-level: the sale references an air `OrderService`, and issuance
resolves the single current non-void ticket coupon covering it - zero or several candidates fail closed, with
no sequence guessing or newest/first pick. **Coupon value** comes only from accepted pricing evidence and is
frozen into `EmdPriceLink`; an item-level price with no defensible split is never divided, it fails closed.

Issuance extends the existing `/Issue` operation rather than inventing `/IssueEmd`: `IssueOrderService` now
coordinates document families through `ElectronicTicketIssuer` (P1 logic intact) and
`ElectronicMiscDocumentIssuer`, discovering both scopes, issuing or recovering ET first, then resolving EMD-A
associations and issuing EMDs; EMD-S needs no ET. Controlled `DocumentStock` is reused under a configured EMD
document type, the number is allocated before the irreversible call, and retry reuses number, role and provider
operation key. `Pending`/`Unknown` keep the reservation and stay recoverable; partial irreversibility moves to
`NeedsReconciliation`. ET and EMD families stay independently correct - an issued EMD never tickets an
unticketed order, a pending EMD never un-tickets a ticketed one, and an already-issued ET is unchanged.

### Recorded servicing decision — EMD has no revalidation lifecycle

```
EMD does not use the electronic-ticket revalidation lifecycle.
Later EMD servicing uses exchange or the other applicable EMD operations.
```

EMD refund, exchange, void, reassociation and disassociation are deferred to P3 and were not partially
implemented. No SSR subsystem, SVC segment, TSM, real EMD host, JetPay, Ledger or SIS work was added.

---

## P2-G — Benchmark-aligned OrderView, projection & pricing-change events

**Complete. P2-F is frozen.** Full detail in
[`P2-G-ORDERVIEW-AND-EVENTS-REPORT.md`](P2-G-ORDERVIEW-AND-EVENTS-REPORT.md); benchmark audit in
[`audit/p2/P2-G-ORDERVIEW-AND-EVENT-BENCHMARK-AUDIT.md`](audit/p2/P2-G-ORDERVIEW-AND-EVENT-BENCHMARK-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **451 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **337 passed**, 0 failed (real SQL Server) |
| Total | **788 passed, 0 failed** (baseline 745 -> +43, zero regressions) |

**One typed local Order View.** The query contract returned `object` / `JsonElement`; it is now the strongly
typed `OrderView`. The projector builds that same type through `OrderViewBuilder` and the handler deserialises
it, so one schema replaces a projector-side anonymous shape plus a reader-side dynamic document. JSON remains
the local storage mechanism, with `SchemaVersion` as a payload marker. `GET /Backoffice/v1/Orders/{id}/Details`,
the new REST-native `GET /Api/v1/Bookings/{id}`, and the `POST .../{id}/Change` response all return the same
view; retrieval is entirely local with no upstream provider call (asserted).

**Complete read surface.** The view now redisplays totals from the existing amount cache (nothing recomputed),
per-document-family facets, travellers, journeys, items with both accepted snapshots, services with
beneficiaries, coverage, price treatment, membership, fulfilment profile, typed or generic detail and EMD
issuance evidence, **fare constructions** (previously absent), commercial changes, and committed **pricing
history** with full modern P2-A pricing lines and allocation sets. Nothing is inferred while projecting, generic
services expose schema identity only, and no legacy pricing category appears.

**Version semantics kept honest.** `CommercialVersion` is exposed under its own name and never relabelled as an
IATA Order Version; no speculative `ExternalOrderVersion` was created. `ProjectionRevision` stays technical
freshness evidence - a test proves issuance advances it while `CommercialVersion` stands still.

**`OrderPricingChanged`.** A new additive internal integration contract raised from domain truth at the point
where the price change set is committed and all counters are final - **exactly one event per committed
`PriceChangeSet`**, never one per line, service or tax. Sibling events from one mutation share the final
`CommercialVersion` and take distinct `EventOrdinal`s, so the event never publishes the set's
`ExpectedCommercialVersion` as its version. The payload is the focused middle ground: envelope facts plus every
pricing line with its allocations - magnitudes with explicit direction, commission still `SettlementOnly`,
repeated tax occurrences still distinguishable, no fabricated references, no provider DTO, no serialised Order,
and nothing routed through `LegacyPricingLineTranslation`.

Reserve, ET/EMD issuance, recovery, provider confirmation and projection refresh emit no pricing event; replay,
rejection and a failed quote write no outbox row. The event is written through the existing `IOutboxWriter`
inside the same UnitOfWork as the mutation and the projection - no second outbox, no dedup table, no broker code
in Domain/Application, and **no Ledger call**. `OrderCreated` and the legacy `OrderIssued` polarity adapter are
untouched, and no historical events were back-filled.

---

## P2-G.1 — OTA order ownership / resource authorization closure

**Complete. P2-G is frozen.** Full detail in
[`P2-G.1-OTA-OWNERSHIP-CLOSURE-REPORT.md`](P2-G.1-OTA-OWNERSHIP-CLOSURE-REPORT.md); endpoint audit in
[`audit/p2/P2-G.1-OTA-OWNERSHIP-AUDIT.md`](audit/p2/P2-G.1-OTA-OWNERSHIP-AUDIT.md).

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **451 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **354 passed**, 0 failed (real SQL Server) |
| Total | **805 passed, 0 failed** (baseline 788 -> +17, zero regressions) |

**The defect.** `GET /Api/v1/Bookings/{orderId}` and `POST /Api/v1/Bookings/{orderId}/Change` retrieved by
`OrderId` alone, so any authenticated OTA caller could read any Order and commit a commercial mutation
against it. Both are closed; P2-G could not be frozen until they were.

**One small seam, not a framework.** `IOrderCustomerAccessGuard` in `Application/OrderAggregate/Access/`:
`RequireCustomerId()` resolves trusted identity from `ICallerContext` or fails closed (2890 / 403), and
`EnsureOwnedAsync(orderId, ct)` compares it against **Ordering-local truth** via a new
`IOrderRepository.FindCustomerIdAsync` — one `AsNoTracking` projection of `Order.CustomerId`, no aggregate
materialisation, no call to Core, Identity, Aegis, Offer, Pricing, JetPay or Ledger. The suggested
`(orderId, customerId)` signature was narrowed so that no call site *can* pass a customer id. No new
authorization framework, policy DSL, permission engine or resource ACL subsystem was built, and
`IIdentityService` / `ICallerContext` were not redesigned.

**Fail closed, disclose nothing.** Unauthenticated or missing `customer_id` -> 2890 / 403 with no order read.
A foreign order and an unknown order take the identical branch and return the identical
`Order '{id}' was not found.` (2500 / 404) — no foreign customer id, traveller name, total or offer id in
the message (asserted). The `CurrentCustomerId ?? 0` defaulting on the two customer-facing **creation**
endpoints was corrected to the same fail-closed rule, since a fabricated owner would have made the check
meaningless.

**Ownership precedes every effect.** The guard is the first statement of both endpoints, ahead of the
idempotency key, `BeginAsync`, CommandReceipt / ServicingOperation / OperationOrderClaim, quote acceptance,
the provider operation key, `Order.AddProduct`, PriceChangeSet, projection and outbox. The exit-gate test
asserts a full before/after record of the target order is byte-identical — counters, receipt/operation/claim
counts, change/item/service/price-set/line counts, pricing outbox count, `ProjectionRevision` and the
projection snapshot — with quote provider `CallCount == 0` and no domain event dispatched. Replay is not
authorization: a non-owner replaying an owner's `Idempotency-Key` gets the same 404 and mutates nothing.

**Backoffice untouched.** Neither Backoffice controller takes the guard (asserted), a Backoffice change still
succeeds for an airline caller with no `CustomerId`, and both channels still call the single
`OrderChangeService` — no OTA-specific commercial mutation exists. The typed `OrderView` and
`OrderPricingChanged` are unchanged.

---

## Scope

P2-H was not started. P3 was not started. No sibling service was inspected or changed. Enum placement was not reopened. No
`Money`, `CurrencyCode`, ExchangeRate framework, currency service, ROE engine or rounding library was
created — the existing `ExchangeRate` value object at `decimal(28,12)` is reused unchanged. No parallel
`PricingV2` / `OrderV2` model exists. Refund, exchange, void, split, DCS, disruption, group booking, tax
engines and fare-rule engines remain unimplemented.
