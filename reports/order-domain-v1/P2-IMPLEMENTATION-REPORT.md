# P2 — Pricing, Fare Construction, Ancillary Catalogue & EMD

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Started:** 2026-09-08
Entry gate: [`audit/p2/P2-ENTRY-AND-MAPPING.md`](audit/p2/P2-ENTRY-AND-MAPPING.md) · Evidence: [`audit/p2/P2-TEST-RUN.txt`](audit/p2/P2-TEST-RUN.txt), [`audit/p2/P2-A.1-TEST-RUN.txt`](audit/p2/P2-A.1-TEST-RUN.txt), [`audit/p2/P2-B-TEST-RUN.txt`](audit/p2/P2-B-TEST-RUN.txt), [`audit/p2/P2-B.1-TEST-RUN.txt`](audit/p2/P2-B.1-TEST-RUN.txt), [`audit/p2/P2-C-TEST-RUN.txt`](audit/p2/P2-C-TEST-RUN.txt)

| Sub-phase | Status |
|---|---|
| **P2-A** pricing foundation | **Complete / Frozen** |
| **P2-A.1** pricing foundation correctness closure | **Complete — P2-A frozen** |
| **P2-B** accepted source normalization | **Complete** |
| **P2-B.1** domain semantic decoupling | **Complete — P2-B frozen** |
| **P2-C** AirFareConstruction | **Complete** |
| P2-D service / item model | Not started |
| P2-E initial sale + AddProduct | Not started |
| P2-F ElectronicMiscDocument | Not started |
| P2-G projections / APIs / events | Not started |
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

## Scope

P2-D was not started. P3 was not started. No sibling service was inspected or changed. Enum placement was not reopened. No
`Money`, `CurrencyCode`, ExchangeRate framework, currency service, ROE engine or rounding library was
created — the existing `ExchangeRate` value object at `decimal(28,12)` is reused unchanged. No parallel
`PricingV2` / `OrderV2` model exists. Refund, exchange, void, split, DCS, disruption, group booking, tax
engines and fare-rule engines remain unimplemented.
