# P2-B.1 — Domain Semantic Decoupling

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
P0–P2-A.1 frozen and unchanged. **P2-C not started.**

Field audit: [`audit/p2/P2-B.1-DOMAIN-SEMANTIC-AUDIT.md`](audit/p2/P2-B.1-DOMAIN-SEMANTIC-AUDIT.md)
Corrected mapping: [`audit/p2/P2-B-SOURCE-NORMALIZATION-MAPPING.md`](audit/p2/P2-B-SOURCE-NORMALIZATION-MAPPING.md)
Evidence: [`audit/p2/P2-B.1-TEST-RUN.txt`](audit/p2/P2-B.1-TEST-RUN.txt)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **157 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **171 passed**, 0 failed (real SQL Server) |
| Total | **328 passed, 0 failed** (baseline 303 → +25, zero regressions) |

P2-B removed physical AirPrice DTO coupling; P2-B.1 removes the remaining **semantic** coupling to the current
AirPrice Fare/FareFamily shape. The Ordering model no longer changes when AirPrice's fare/brand model changes —
only the ACL does.

## 2. AirPrice vocabulary now terminates at the ACL

The Order Domain has **zero** references to `AeroTech.Messages.AirPrice.*` — assembly types, member signatures
and source files, each asserted by a separate test. The P2-B allow-list exception for `PassengerTypeCode` and
`WeightUnit` is gone.

Three Ordering-owned enums were added under the closed convention in `Contracts/AeroTech.Messages/Ordering/Enums`:
`PassengerTypeCode` (same IATA PTC vocabulary, Ordering-owned), `BaggageWeightUnit`, and `JourneyType` (used by
the pre-existing pricing provider port). A fourth, `CommercialTermState`, carries the new commercial summaries.
The AirPrice service and its contracts were **not** modified, and no sibling-service change is required — the
ACL translates: `TranslatePassengerType` fails closed (2787) on an unmapped code, and the weight-unit
translation is part of the existing fail-closed baggage path (2786).

`JourneyType` also disappeared from the Domain by switching the legacy `Domain/Providers/Pricing` port contract
to the Ordering enum. Member names and numeric values are identical, so the provider wire payloads are byte-identical.

## 3. Product semantics corrected

The rejected mapping is gone:

| Was (P2-B) | Now (P2-B.1) |
|---|---|
| `AirFareId → ProductCode` | `AirFareId → SourceProductReference` / `SourcePricingReference` only — opaque provenance. `ProductCode` is nullable and **left null**. |
| `FareBasis → ProductName` | `FareBasis` is fare-construction context. It is written to no product-name field. `ProductName` is nullable and **left null**. |
| `FareFamily → Brand` | `FareFamily → BrandName`, and only because the ACL explicitly treats that source value as the customer-visible brand/family label. `BrandCode` exists but is **never invented** from `AirFareId`. |

`OrderItem.ProductCode`/`ProductName` became nullable so an absent product identity stays absent instead of
being synthesized. This is a deliberate touch to a P1-era column shape: the P1 *behaviour* freeze is preserved,
but retaining a non-null column would have forced exactly the false mapping the review flagged.

No fare-family feature mirror was created. Fare-family benefits (baggage, seat, priority, lounge, meal) become
real sold/included `OrderService`s in **P2-D** when operationally relevant.

## 4. Commercial terms are now an Ordering-owned summary

`IsRefundable` / `IsChangeable` / `IsUpgradable` are no longer the persisted model. The snapshot holds
`RefundabilitySummary`, `ChangeabilitySummary` and `UpgradeEligibilitySummary`, each a
`CommercialTermState` of `Unknown | Prohibited | Permitted | Conditional`, plus `SourceSystem`,
`SourcePolicyReference`, an optional `SourcePolicyVersion` (stored only when actually supplied) and
`TermsCapturedAt`.

For the current coarse AirPrice contract the ACL translates `true → Permitted`, `false → Prohibited` and
**absent evidence → `Unknown`** (not a permission). A future Pricing/FareFamily contract can map richer rules
into `Conditional` **without any Ordering schema change** — pinned by a theory over all four states plus a
persistence round-trip of `Conditional` with a policy version.

**The summary cannot authorize servicing.** It is historical accepted-sale evidence that may inform display and
pre-screen only; P3 must still obtain the authoritative servicing quote. A test asserts no
`OrderAggregate.Policies` eligibility policy accepts the snapshot as input, and another asserts a `Conditional`
summary does not grant the legacy permission flag.

No speculative rule model was introduced: no penalty amount, before/after-departure rule, no-show rule, waiver
authority, residual value or refund calculation exists anywhere in the model.

## 5. Baggage ownership

Baggage was removed from `AcceptedCommercialTerms` and `OrderItemCommercialTermsSnapshot` — it is not a
change/refund commercial-policy fact and was being persisted twice. Only the minimum P1-compatible
representation on `OrderAirTransportService` remains, recorded as transitional/non-normative. The real baggage
model — included allowance versus separately priced — is **P2-D**, and none of it was built here.

## 6. Air service detail carries no commercial policy

`AcceptedAirServiceDetail` lost `IsChangeable`, `IsRefundable`, `IsUpgradable` and `FareNumber` (the last had
no source input and no consumer — it failed the practicality gate outright). The legacy
`OrderAirTransportService` permission columns still exist in the P1 schema but are now **derived** from the
item's commercial term summary rather than fed per-service, so the air service is not the owner of fare-family
policy. `FareBasis` and `FareFamily` survive there only as temporary compatibility fields because P1 ETKT
issuance reads `service.FareBasis`; **P2-C must move authoritative fare-basis context to `FareComponent`**, after
which issuance takes issue-time fare context from the fare-construction association instead of the legacy field.

## 7. Persistence

`P2BAcceptedSourceSnapshots` had already been applied, so it was **not** rewritten. A new corrective migration
`P2B1DomainSemanticDecoupling` drops the three booleans and six baggage columns from
`OrderItemCommercialTermsSnapshots`, adds the three summary columns plus `SourcePolicyVersion`, renames
`PolicySource → SourceSystem` and `SourceRuleReference → SourcePolicyReference`, renames
`Brand → BrandName` and adds `BrandCode` on `OrderItemProductSnapshots`, and makes `OrderItems.ProductCode`
/`ProductName` nullable.

The three scaffolded renames were checked to be **meaning-preserving** (same content, new name) — unlike the
P2-A case where EF matched columns by CLR type — and Up/Down are symmetric, so they were kept. One scaffolding
defect was corrected by hand: EF defaulted the new enum columns to `0`, which is not a valid
`CommercialTermState`; they now default to `1` (`Unknown`) so no row can carry an invalid value. Applied to
`DotAirOrderNew`.

## 8. Tests added (+25)

`tests/AeroTech.Ordering.Domain.Tests/P2/CommercialSemanticsTests.cs` (12) — fare identifier stays opaque
provenance and is not a product code; fare basis is never a product name; absent code/name stay absent; brand
label only when supplied; all four `CommercialTermState` values accepted without schema change; `Conditional`
does not grant the legacy flag; summaries immutable after sale; the summary cannot authorize servicing; baggage
absent from both commercial-terms types; accepted air service carries no policy; legacy flags derived from item
terms.

`DomainProviderBoundaryTests` gained three stricter assertions (zero AirPrice types in fields/properties, zero
in member signatures, zero in Domain source files) replacing the P2-B allow-list test.

`AirPriceOfferNormalizerTests` gained 6 — boolean→summary translation both ways, absent evidence → `Unknown`,
passenger-type and weight-unit translation at the ACL, fare id as provenance not product code, brand-name
translation only when supplied.

`tests/AeroTech.Ordering.Persistence.Tests/P2/CommercialSnapshotPersistenceTests.cs` (2) — both snapshots
survive reload unchanged with null product code/name; a `Conditional` summary with a policy version round-trips.

The full P1 vertical slice (Create → Get → Reserve → coverage → issuance eligibility → controlled ticket number
→ Issue → redisplay), P1.1/P1.2 recovery and partial issuance, and all P2-A/P2-A.1 pricing tests remain green,
as does P2-B fail-closed normalization.

## 9. Exit gate

| Gate | Status |
|---|---|
| AirPrice DTO structure terminates at the ACL | **Yes** (P2-B, unchanged) |
| AirPrice semantic vocabulary terminates at the ACL | **Yes** — zero `AeroTech.Messages.AirPrice.*` in the Domain |
| ProductSnapshot holds Ordering product semantics, not repurposed fare fields | **Yes** |
| CommercialTermsSnapshot is a stable Ordering-owned summary | **Yes** — `CommercialTermState` |
| Source change/refund/upgrade booleans are ACL inputs, not Order rule structure | **Yes** |
| Baggage not duplicated into CommercialTermsSnapshot | **Yes** — removed |
| FareBasis is not ProductName | **Yes** |
| AirFareId is not ProductCode | **Yes** |
| No AirPrice-owned enums in the Order Domain | **Yes** |
| Every persisted P2-B field has a concrete Ordering use case | **Yes** — see the field audit |
| No speculative fare-rule / fare-family model introduced | **Yes** |
| All existing tests remain green | **Yes** — 328 passed, 0 failed |
| P2-C has not started | **Yes** — no `AirFareConstruction`/`PricingUnit`/`FareComponent` type exists |

**P2-B is ready to freeze.** No `BLOCKED_DECISION` was required.
