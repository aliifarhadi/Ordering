# P2-H — Migration & Relational Integrity Audit

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09
**Migration created by P2-H:** none — no schema defect was found.

---

## 1. Clean-database chain verification (§29)

The test database was dropped outright and rebuilt from zero by the migration chain, then the whole
persistence suite was run against the resulting schema:

```
sqlcmd -Q "ALTER DATABASE [DotAirOrderNewP0Tests] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
           DROP DATABASE [DotAirOrderNewP0Tests];"      -> dropped

fixture Database.Migrate()  (commands, queries, reference contexts)
                                                        -> 22 migrations applied

dotnet test AeroTech.Ordering.Persistence.Tests         -> 360 passed, 0 failed
```

Applied chain, in order: `InitialCreate` ×3 · `RenameOfficeIdToAirlineOfficeId` ×2 ·
`P0OperationsAndRatePrecision` · `P0CommercialVersion` · `P05OperatorSettingsProjection` ·
`P06ClaimConcurrencyToken` · `P1OrderVerticalSlice` · `P1OrderDetailsProjection` ·
`P11IssuerCarrierIdentity` · `P2PricingFoundation` · `P2A1PricingProvenance` ·
`P2BAcceptedSourceSnapshots` · `P2B1DomainSemanticDecoupling` · `P2CAirFareConstruction` ·
`P2CIgnoreComputedFareComponents` · `P2DServiceComposition` · `P2D1ServiceTreatmentAndIntegrity` ·
`P2ECommercialOperationUniqueness` · `P2FElectronicMiscDocument`.

**P2-G, P2-G.1 and P2-H add no migration** — they changed the query contract, the integration contract and
resource authorization, not the schema. `The_migration_is_applied_on_the_current_database` (EMD) and
`ServicePriceTreatmentMigrationTests` additionally assert applied state from inside the suite.

No separate deployment framework was invented; the existing `Database.Migrate()` path is the upgrade
mechanism and it was exercised end-to-end from an empty server.

## 2. No migration was edited after the fact (§29)

`git log --diff-filter=A` and `git log | wc -l` per migration file:

| Migration | Introduced in | Commits touching it |
|---|---|---|
| `InitialCreate`, `RenameOfficeIdToAirlineOfficeId` | `e133140` | 1 |
| `P0OperationsAndRatePrecision` | `ebf06d1` | 1 |
| `P06ClaimConcurrencyToken` | `c4979b5` | 1 |
| `P1OrderVerticalSlice` | `d56f976` | 1 |
| `P11IssuerCarrierIdentity` | `83d45c6` | 1 |
| `P2PricingFoundation` | `7d5fcb0` | 1 |
| `P2A1PricingProvenance` | `450cab6` | 1 |
| `P2BAcceptedSourceSnapshots` | `bd13c1d` | 1 |
| `P2B1DomainSemanticDecoupling` | `c29dc60` | 1 |
| `P2CAirFareConstruction`, `P2CIgnoreComputedFareComponents` | `9b564a2` | 1 |
| `P2DServiceComposition` | `c187865` | 1 |
| `P2D1ServiceTreatmentAndIntegrity`, `P2D1ServicePriceTreatmentBackfill` | `853e2db` | 1 |
| `P2ECommercialOperationUniqueness` | `d353b77` | 1 |
| `P2FElectronicMiscDocument` | `72f4fe2` | 1 |

Every file has exactly one commit — **no previously applied migration was edited.**

## 3. Data-moving SQL in P2 migrations

Only three migrations contain `migrationBuilder.Sql`, and every statement was reviewed:

| Migration | Statement | Verdict |
|---|---|---|
| `P2DServiceComposition` | `INSERT INTO OrderAirTransportServiceDetails … SELECT … FROM OrderAirTransportServices` | Structural move of existing rows during the TPT→composition change. Copies real columns; invents nothing. |
| `P2DServiceComposition` | `INSERT INTO OrderServiceBeneficiaries … SELECT a.Id, a.TravellerId …` | Promotes the existing single traveller to the beneficiary collection. Real data. |
| `P2DServiceComposition` | `INSERT INTO OrderItemServiceLinks … WHERE EXISTS (… OrderChanges …)` | Materialises the existing item↔service relation; guarded so no link is created without a real change row. |
| `P2D1ServiceTreatmentAndIntegrity` | `P2D1ServicePriceTreatmentBackfill.Sql` | Derives `PriceTreatment` **from existing accepted pricing lines only** (`Effect=CustomerBalance`, `LineRole=Original`, `ComponentType ∈ {Fare, ProductCharge}`), `SeparatelyPriced` on a service-basis line, `Included` on an item-basis line, otherwise `SupplierOpaque`. `WHERE PriceTreatment <> 3` preserves any explicit `Complimentary`. **`Complimentary` is never inferred**, as required by P2-D.1. |
| `P2D1ServiceTreatmentAndIntegrity` | six `IF EXISTS … THROW` guards | Fail closed on corrupt historical data instead of repairing it silently. |
| `P2ECommercialOperationUniqueness` | duplicate `(OrderId, OperationId)` probe + `THROW 62001` | Fail-closed guard before creating the filtered unique index; explicitly refuses to delete or repoint commercial history. |

**No migration inserts an outbox message, a fare construction, a source policy reference or a product
identity.** `grep -in "INSERT INTO"` across all migrations returns only the three structural inserts above —
so §32's "no fabricated historical data" holds: no historical `OrderPricingChanged` was back-filled, no
historical `AirFareConstruction` was invented, absence stayed absence.

## 4. Nullable, default and enum transitions

`P2D1ServiceTreatmentAndIntegrity` is the pattern for the whole set: the column is added nullable or with a
neutral default, the backfill derives real values from accepted evidence, the fail-closed guards run, and
only then is the constraint tightened. `ServicePriceTreatment` has no `0`/`Unknown` member; the migration's
fallback is the semantically honest `SupplierOpaque = 4`, not a silent zero. Persisted enum numeric values
were not reordered in P2 — `LegacyWireTranslationTests` and the EMD/`EmdType` restoration recorded in P2-F
guard the durable ones.

## 5. Relational integrity — cardinality (§30)

Queried live from the rebuilt schema (`sys.indexes` where `is_unique = 1`):

| Table | Unique index | Cardinality it permits |
|---|---|---|
| `OrderFareComponentServices` | `(FareComponentId, OrderServiceId)` | **many services per fare component** — through fares representable |
| `OrderFareComponentSegments` | `(FareComponentId, OrderSegmentId)` | **many segments per fare component** |
| `OrderFarePricingGroupTravellers` | `(PricingGroupId, OrderTravellerId)` | **many travellers per group**, and a traveller may appear in more than one group across constructions |
| `OrderServiceBeneficiaries` | `(OrderServiceId, OrderTravellerId)` | **many beneficiaries per service** |
| `OrderServiceCoveredServices` | `(OrderServiceId, CoveredOrderServiceId)` | many covered services |
| `OrderServiceCoveredSegments` | `(OrderServiceId, OrderSegmentId)` | many covered segments |
| `OrderItemServiceLinks` | `(OrderItemId, OrderServiceId)` | many services per item |
| `EmdCoupons` | `(ElectronicMiscDocumentId, CouponNumber)` | **many coupons per EMD** |
| `TicketCoupons` | `(TicketId, CouponNumber)` | many coupons per ticket |
| `OrderFareComponents`, `OrderFarePricingUnits`, `OrderFarePricingGroups` | primary key only | no restrictive unique |

Every unique index is a **composite pair de-duplication**; not one is a single-column unique on a parent
foreign key. None of the prohibited implications (`one OrderService == one FareComponent`,
`one JourneySegment == one FareComponent`, `one traveller == one PricingGroup always`,
`one service beneficiary only`, `one EMD coupon only`) is expressed anywhere in the schema.

Foreign-key correctness is separately asserted by `Every_new_target_foreign_key_exists_and_takes_no_delete_action`
(12 P2-D.1 relations, `NoAction`), `The_document_tables_carry_no_shadow_foreign_keys`,
`The_document_tables_exist_with_no_action_delete_rules`, `The_document_number_is_globally_unique`, and the
eleven `ServiceReferentialIntegrityTests` / `EmdPersistenceTests` negative cases that prove each FK actually
rejects a dangling reference on a real server.

## 6. Immutability of accepted history (§31)

| Artefact | Guard |
|---|---|
| `ProductSnapshot`, `CommercialTermsSnapshot` | `The_accepted_product_and_terms_snapshots_survive_a_reload_unchanged` |
| `PriceChangeSet` and its lines once committed | `A_committed_change_set_is_immutable` |
| `AirFareConstruction` history | `A_historical_construction_cannot_be_mutated`, `A_successor_construction_preserves_lineage_and_becomes_the_current_one`, `A_construction_cannot_supersede_itself` |
| Issue-time document price attribution | `Issue_time_price_attribution_is_retained_on_the_document`, `The_price_links_round_trip` |
| Issued documents | `An_issued_document_is_immutable` (EMD), `The_existing_electronic_ticket_is_unchanged_by_document_issuance` |

Successor/history semantics exist for fare constructions; P3 servicing successor flows were **not**
implemented here.

## 7. Result

No schema defect, no edited migration, no fabricated historical data, no accidental restrictive cardinality.
**No migration was created by P2-H.**
