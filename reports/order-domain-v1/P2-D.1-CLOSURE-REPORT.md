# P2-D.1 — Service Price-Treatment & Referential-Integrity Closure

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline: P2-D (`42ebb59`), **516 passed / 0 failed**. P0 – P2-C.1 frozen. **P2-E and P2-F not started.**

Evidence: [`audit/p2/P2-D.1-TEST-RUN.txt`](audit/p2/P2-D.1-TEST-RUN.txt)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `dotnet ef database update` (`DotAirOrderNew`) | applied; no orphan check raised; no multiple-cascade-path error |
| `AeroTech.Ordering.Domain.Tests` | **324 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **225 passed**, 0 failed (real SQL Server) |
| Total | **549 passed, 0 failed** (baseline 516 → +33, zero regressions) |

## 2. Defect 1 — Air `PriceTreatment` was hardcoded

`AcceptedProductBuilder.AddService()` stamped `ServicePriceTreatment.SeparatelyPriced` on every air service
the moment it was created, before any pricing evidence had been normalized. Existing alone was treated as
proof of an independent product price.

### The rule now applied

`PriceTreatment` describes the **commercial relationship between a service and accepted price evidence**. It
is decided after normalization, from the accepted pricing facts themselves:

| Evidence found for the service | Treatment |
|---|---|
| an **Original**, **CustomerBalance** line with `BasisType = OrderService`, `ServiceRef = this service`, and a **primary** component (`Fare` or `ProductCharge`) | `SeparatelyPriced` |
| no such line, but the parent product carries the same kind of primary line at `BasisType = OrderItem` | `Included` |
| neither | `SupplierOpaque` |
| — | `Complimentary` is **never inferred** |

A `Tax`, `CarrierSurcharge` or `Fee` that happens to be service-scoped is **not** primary value evidence: an
item-priced round trip whose taxes are per-segment leaves both air services `Included`, and the item price is
still counted exactly once. Absence of evidence resolves to `SupplierOpaque`, never `SeparatelyPriced`.

### Where it lives

`ServicePriceTreatmentResolver` (ACL, `Providers/Offer/Services/`) owns the rule. The builder now accumulates
`PendingAirService` descriptors and materializes `AcceptedService` in `Build(acceptedLines)`, once the
traveller's pricing facts are known — the second approach the brief allowed. The Domain never inspects AirPrice
DTOs and no pricing interpretation moved back into `Order`.

### Money is untouched

The change reads pricing evidence and writes one enum. It creates, deletes and splits no `PricingLine`, moves
no `CustomerTotal`, advances no `FinancialSequence` or `ObligationVersion`, and touches no allocation set —
each asserted by test against two sources that differ only in pricing basis.

## 3. Correcting already-migrated data

`P2DServiceComposition` gave every pre-existing service `PriceTreatment = SeparatelyPriced` through the
column default. That migration was **not** edited. The new migration
**`20260908134158_P2D1ServiceTreatmentAndIntegrity`** re-derives the value from the persisted P2-A ledger using
the same rule, expressed against the real columns and real enum values (`Effect = 1` CustomerBalance,
`LineRole = 1` Original, `ComponentType IN (1,2)` Fare/ProductCharge, `BasisType = 3` OrderService /
`= 2` OrderItem):

- direct service-level primary value → `SeparatelyPriced` (1)
- parent-item primary value only → `Included` (2)
- neither → `SupplierOpaque` (4)
- rows already marked `Complimentary` (3) are excluded from the update, so an explicitly supplied
  complimentary service is never reclassified and no row ever becomes complimentary by inference

The statement is idempotent (tested) and lives in `P2D1ServicePriceTreatmentBackfill.Sql` so the migration and
its tests execute the identical text rather than two drifting copies. No historical `PricingLine` is read
destructively or deleted.

## 4. Defect 2 — target references had no foreign keys

The P2-D tables already used explicit typed identifiers instead of `ReferenceType + ReferenceId`, but ten
target references were indexes only. All are now real FKs, configured with `HasOne<T>().WithMany()` and **no
navigation properties added to the entities**:

| Dependent | Column | Principal |
|---|---|---|
| `OrderServices` | `OrderItemId` | `OrderItems` |
| `OrderServiceBeneficiaries` | `OrderTravellerId` | `OrderTravellers` |
| `OrderServiceCoveredSegments` | `OrderSegmentId` | `OrderSegments` |
| `OrderServiceCoveredServices` | `CoveredOrderServiceId` | `OrderServices` |
| `OrderItemServiceLinks` | `OrderItemId` | `OrderItems` |
| `OrderItemServiceLinks` | `OrderServiceId` | `OrderServices` |
| `OrderItemServiceLinks` | `LinkedByChangeId` | `OrderChanges` |
| `OrderAirTransportServiceDetails` | `OrderSegmentId` | `OrderSegments` |
| `OrderSeatServiceDetails` | `AssociatedAirOrderServiceId` | `OrderServices` |
| `OrderLoungeServiceDetails` | `RelatedAirOrderServiceId` (nullable) | `OrderServices` |

Every one is `DeleteBehavior.NoAction`. The Order aggregate root keeps the single cascade path over its own
graph; these FKs assert referential integrity and deliberately do **not** redefine ownership, so SQL Server
raises no multiple-cascade-path error (asserted by querying `sys.foreign_keys` for
`delete_referential_action = 0`). Supporting indexes were added for the three columns that had none.

## 5. Database proves existence, Domain proves belonging

An FK only proves the target row exists. The Domain still fails closed on the facts SQL cannot express:
a beneficiary, covered service, covered segment, associated air service or related air service must belong to
**the same Order** and, where required, must actually be `AirTransportation`. Those rules are unchanged and
still tested, including a case proving a reference that only resolves in a different Order is rejected (2784).
No composite-key framework was introduced to push aggregate invariants into SQL.

## 6. Fail-closed migration

`Up` runs the treatment correction, then ten explicit orphan pre-checks — one per new FK — each of which
`THROW`s a named, self-describing error identifying the exact table and column if historical data violates it.
Nothing is deleted, repointed, reattached to another Order, or invented. The check runs before
`AddForeignKey` so the failure names the corrupt data instead of surfacing an opaque constraint error.

## 7. Unchanged by design

`OrderItemServiceLink.LinkedByChangeId` keeps its real commercial-change identity and was not weakened to a
correlation id; the historical link migration logic from P2-D was not redesigned. The generic-service profile
still resolves the registered schema **once, at creation**, and persists the result — no registry lookup was
added to redisplay or issuance. Enum numeric values and `OrderServiceType` ordering were not touched, and
blocked financial pseudo-services stay blocked. No `AddProduct`, EMD, supplier reservation, refund, exchange,
reissue, split, DCS, pricing/offer redesign, JetPay or Ledger work was started, and no sibling repository was
inspected or modified.

## 8. Tests added (33)

| Area | Count | File |
|---|---|---|
| Price-treatment derivation (§15) | 13 | `P2/ServicePriceTreatmentTests.cs` |
| Migration correction on real SQL Server (§16) | 6 | `P2/ServicePriceTreatmentMigrationTests.cs` |
| Foreign keys on real SQL Server (§17) | 12 | `P2/ServiceReferentialIntegrityTests.cs` |
| Cross-Order reference rejection (§17.31) | 2 | `P2/ServiceBeneficiaryAndCoverageTests.cs` |
