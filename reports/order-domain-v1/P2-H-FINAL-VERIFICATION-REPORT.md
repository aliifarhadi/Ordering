# P2-H — Final Verification, Architecture Audit & P2 Release Closure

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09

| | |
|---|---|
| **Entry commit** | `6fc7d227ce532ccc8465027b709199352b541021` (P2-G.1) — verified as HEAD, clean tree |
| **Parent** | `bae30a3c18afa52422a43648c254a9b5f5515177` (P2-G) |
| **Final commit** | this working tree on `k8s-stg`, entry commit + P2-H verification tests and reports only |
| **Build** | `dotnet build AeroTech.Ordering.sln` — **0 errors**, 2 warnings (both the same NU1510) |
| **Domain tests** | **451 passed**, 0 failed |
| **Persistence tests** | **360 passed**, 0 failed (real SQL Server, database rebuilt from zero) |
| **Total** | **811 passed, 0 failed** (baseline 805 → +6; requirement was ≥ 807) |
| **Migration result** | Database dropped and rebuilt from the full 22-migration chain; suite green against the resulting schema. **No migration created by P2-H.** |
| **Production-code changes by P2-H** | **none** |
| **New verification tests** | **6** |

Evidence: [`audit/p2/P2-H-TEST-RUN.txt`](audit/p2/P2-H-TEST-RUN.txt) ·
Architecture & semantics: [`audit/p2/P2-H-ARCHITECTURE-AND-SEMANTIC-AUDIT.md`](audit/p2/P2-H-ARCHITECTURE-AND-SEMANTIC-AUDIT.md) ·
Migrations & schema: [`audit/p2/P2-H-MIGRATION-AUDIT.md`](audit/p2/P2-H-MIGRATION-AUDIT.md) ·
Release gate: [`audit/p2/P2-H-RELEASE-GATE.md`](audit/p2/P2-H-RELEASE-GATE.md)

---

## 1. What P2-H did

Verification only. No production file was modified, no refactor was performed, no business behaviour was
added. Six tests were added: the two creation fail-closed tests the brief requires (plus two companions in
the same suite), and two that close the single real gap the audit found.

| File | Tests | Why |
|---|---|---|
| `Persistence.Tests/P2/OtaCreationFailClosedTests.cs` | 4 | §4 — direct runtime proof that both customer-facing creation endpoints fail closed without customer context. Drives the **real** `OtaController` / `OtaPanelController` with the real guard and a real MediatR pipeline; asserts 2890 / 403, creation handler invocations = 0, `Orders` row count unchanged, every provider adapter untouched, no domain event dispatched. Not a reflection test. |
| `Persistence.Tests/P2/SettlementOnlyPricingGateTests.cs` | 2 | §12 — settlement-only pricing had domain-level coverage but no persistence/outbox gate. Now asserts exactly one `OrderPricingChanged` with `DerivedCustomerBalanceImpact = 0`, `CustomerTotal` and `ObligationVersion` unchanged, `FinancialSequence` +1, all lines `SettlementOnly`, and one outbox row across a replay. |

## 2. Entry baseline

HEAD was exactly `6fc7d22` with a clean working tree. The P2-G.1 seam is intact and unchanged:
`IOrderCustomerAccessGuard.RequireCustomerId()` / `EnsureOwnedAsync(orderId, cancellationToken)` over
`IOrderRepository.FindCustomerIdAsync`, which is still an Ordering-local `AsNoTracking` projection of
`Order.CustomerId`. No Core / Identity / Aegis / Pricing / Offer call participates in establishing
ownership. All 17 P2-G.1 tests re-run green.

## 3. Architecture boundary

`AeroTech.Ordering.Domain` references only `Framework.Core` and `Contracts/AeroTech.Messages`.
**`AeroTech.Messages.AirPrice.*` in Domain: zero occurrences** — all five AirPrice-touching files live in
`Providers/Offer` (the ACL). No `Offer`, `FareFamily`, `AirFare`, `Bound` or `Flight` aggregate mirroring
upstream structure exists in the Domain.

One boundary finding, classified as **legacy pre-existing debt, not a P2 defect**: `OrderSegmentLeg.StopType`
persists `FlightStopType` from `AeroTech.Messages.FlightFlow.Enums`, and three legacy port records under
`Domain/Providers/` do the same. `git log --diff-filter=A` dates all of them to the repository's first
commit, before P0; they are the documented `Domain → Contracts` enum inversion already deferred to P3 in
`DomainAuditRemediationPlan.md`, and none is on the P2 pricing or commercial path. Correcting it needs an
Ordering-owned enum plus a migration — a new domain concept, which §1 forbids here.

## 4. Semantic coercion

The AirPrice ACL uses `AirFareId` exactly once, for `AcceptedProductSnapshot.ProductIdentifier`, and passes
`SourcePricingReference: null` and `SourcePolicyReference: null` explicitly; `SourcePolicyVersion` is never
supplied. `ProductCode`, `ProductName`, `BrandCode` and `SupplierCode` stay null; `BrandName` appears only
when the source supplies a fare-family label; carrier ids appear only when unambiguous; absent refundability
or changeability evidence becomes `Unknown`, never a permission. No source identifier is relabelled into a
second semantic reference.

## 5. Fare-construction inference

`AirPriceOfferNormalizer` never supplies `FareConstructions`, so the current source produces **none** and
the Order persists none. Across `src/`, `openjaw|isroundtrip|infer` returns **zero hits** and `FareBasis` is
never string-parsed. True RT, OW+OW, OpenJaw, through fares (one component over several services and
segments) and fare breaks are each representable when the source declares them, and each is covered by a
test. Absence stays absence.

## 6. Pricing, versions, events and atomicity

`PricingLine` remains customer monetary truth: polarity, effects, non-negative magnitudes, commission never
reducing customer balance, tax never settlement-only, reversal provenance and joint caps, occurrence
identity, allocation ownership and Complete/Partial/Unavailable reconciliation all still enforced.
Allocations never contribute to totals; `Order.CustomerTotal` stays derived from customer-balance lines.
Fare construction remains non-monetary accepted context.

`CommercialVersion` advances once per accepted commercial mutation and never for reservation, funding,
document issuance or projection; `FinancialSequence` once per committed `PriceChangeSet`; `ObligationVersion`
only on customer-total movement. Siblings share the final version with distinct ordinals. Exactly one
`OrderPricingChanged` per committed set, carrying the final version and never `ExpectedCommercialVersion`;
none for reserve, funding, ET issue, EMD issue, recovery, provider confirmation, projection refresh, replay,
rejection or failed quote. No event was back-filled.

The only explicit transaction in `src/` is opened and committed inside `OrderingUnitOfWork.SaveChangesAsync`,
so command state, read model and outbox commit together and no provider call can occur inside it.

## 7. OrderView and local read

`GetOrderDetailsQuery : IRequest<OrderView?>`; no `object`, `dynamic`, `JsonElement` or anonymous nested DTO
in the query or view contract. `SnapshotJson` stays a local storage representation and `SchemaVersion` the
payload marker. Raw `AttributesJson` never reaches the Query, Synchronizer or RestApi layers. Both GET
routes are satisfied entirely from local projection data with zero upstream fanout. `ProjectionRevision`
advances on issuance while `CommercialVersion` stands still, and `CommercialVersion` is not relabelled as an
IATA / NDC / external order version.

## 8. OTA ownership

`CurrentCustomerId` no longer appears anywhere in `src/` — the `?? 0` defaulting is gone from the whole
solution. Owner reads succeed; foreign and unknown reads take the identical non-disclosing 404 branch;
absent or unauthenticated customer context fails closed; a cross-customer change calls the quote provider
zero times and leaves receipts, operations, claims, commercial state, pricing state, projection and outbox
byte-identical; a non-owner replaying the owner's `Idempotency-Key` learns nothing and mutates nothing; both
creation endpoints now have direct runtime proof that they fail closed and create no Order. Backoffice takes
no guard and still operates for an airline caller with no `CustomerId`.

## 9. Migrations and schema

The test database was dropped and rebuilt from zero by the migration chain (22 migrations) and the full
suite ran green against the result. Every migration file has exactly one commit — none was edited after the
fact. P2-G, P2-G.1 and P2-H add no migration. The only data-moving SQL is the P2-D structural TPT→composition
move, the P2-D.1 price-treatment derivation from existing accepted pricing evidence (which never infers
`Complimentary`), and fail-closed `THROW` guards; nothing inserts an outbox message, a fare construction, a
source policy reference or a product identity.

Live schema inspection confirms every unique index is a composite pair de-duplication, never a single-column
unique on a parent key: many services and segments per `FareComponent`, many travellers per `PricingGroup`,
many beneficiaries per service, many coupons per EMD and per ticket all remain representable.

## 10. Warnings

`dotnet build AeroTech.Ordering.sln` reports 2 warnings, both the same **NU1510** (a redundant
`Microsoft.Extensions.Hosting.Abstractions` `PackageReference` in `AeroTech.Framework.Infrastructure`,
counted once per project and once per solution). It is not a correctness, nullability, migration,
serialization or domain-contract warning, so it is **not a P2 blocker**. Test-project builds additionally
emit pre-existing xUnit analyzer and EF1002 warnings in test code. No unrelated warning cleanup was
performed.

## 11. Deferred-debt register

| Item | Class | Note |
|---|---|---|
| `OrderSegmentLeg.StopType` and three legacy `Domain/Providers` records use `AeroTech.Messages.FlightFlow.Enums` | Legacy pre-existing debt | Pre-P0; the documented Domain→Contracts enum inversion; needs an Ordering-owned enum + migration |
| `Domain/Providers/{FlightFlow,Pricing,Payment}` sit outside the P2 `Domain/Ports/<Area>` convention | Legacy pre-existing debt | Pre-P0; consumed only by P1-era FulfillmentTask, Split, Payment and LastTicketingDate paths |
| `Payment` aggregate, `PaymentService`, `PayOrderCommandHandler`, `POST Internal/v1/Orders/{id}/Payments` | Legacy pre-existing debt | Pre-P0; reachable only via the Internal maintenance channel; no P2 functionality depends on it |
| `PingController` route casing differs from the Order routes | Cosmetic pre-existing debt | Shared liveness endpoint; §33 forbids route redesign |
| `Service/v1/Bookings` is a declared controller with no actions | Future scope | Service-to-service sync channel, not yet needed |
| Refund, refund mask, exchange, reissue, revalidation; EMD refund/exchange/void; ticket exchange; remove service; itinerary change; document-void commercial consequences | **Future P3 requirement** | Not started, deliberately |
| SSR lifecycle; OrderHistory API; DCS delivery; disruption servicing; group booking | **Future P3 requirement** | Seams documented, not implemented |
| Full authoritative change/refund rule evaluation; ATPCO filing / rule engine | **Sibling-service dependency** | Belongs to Pricing/AirPrice, not Ordering |
| Ledger posting; JetPay redesign; stored-value / wallet redesign; IATA/NDC external order version | **Sibling-service dependency** | Ordering publishes `OrderPricingChanged` and stops there |

No P2 defect was found. No blocker.

## 12. Compliance statement

This report claims only what was executed and observed in this repository. Ordering's flows and vocabulary
were kept traceable to established PSS/IATA practice under the standing Airline Flow Benchmark Rule, and the
P2-F EMD work follows IATA 725f/g/h structure and RFIC/RFISC semantics — but **no schema conformance,
certification or standards compliance is claimed** for any IATA, NDC or ONE Order message. `OrderPricingChanged`
is an internal Ordering integration contract, not an NDC message.

## 13. Exit gate

| Gate | Result |
|---|---|
| Full build succeeds | Pass — 0 errors |
| All tests pass | Pass — 811 / 0 |
| Migration chain succeeds from an empty database | Pass — 22 migrations, suite green |
| P2-H adds no unapproved business behaviour | Pass — zero production-code changes |
| Domain has no provider DTO/schema coupling | Pass — zero AirPrice references in Domain; F-1 is pre-P0 legacy debt, deferred |
| No semantic source-reference coercion remains | Pass |
| Current AirPrice does not fabricate FareConstruction | Pass |
| PricingLine remains customer monetary truth | Pass |
| Fare construction remains non-monetary accepted context | Pass |
| OrderService composition invariants hold | Pass |
| EMD semantics remain correct | Pass |
| Typed OrderView is complete and local | Pass |
| GET performs no upstream fanout | Pass |
| Version semantics hold | Pass |
| One `OrderPricingChanged` per committed `PriceChangeSet` | Pass |
| No duplicate pricing events on replay | Pass |
| No pricing event on non-pricing operational mutations | Pass |
| Outbox / state atomicity intact | Pass |
| OTA ownership closes cross-customer read, write and replay | Pass |
| Both customer-facing creation paths fail closed without customer context | Pass — now with direct runtime tests |
| Backoffice not accidentally customer-restricted | Pass |
| No Ledger call introduced | Pass |
| No active P2 dependency on a local Payment aggregate | Pass |
| All deferred P3 behaviour remains deferred | Pass |

**All exit gates pass. P2 is frozen.** P3 is not started.
