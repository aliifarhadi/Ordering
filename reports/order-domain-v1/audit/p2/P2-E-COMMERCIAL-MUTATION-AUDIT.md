# P2-E — Commercial Mutation Audit (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08

---

## 1. AddProduct atomicity

`Order.AddProduct` is `StageProductAddition` → `AttachProductAddition`, the same two-phase shape P2-A introduced
for pricing. Everything that can reject the candidate happens during staging, while the aggregate's collections
are untouched:

| Staged and validated before any attach | Reason code |
|---|---|
| order is commercially active | 2850 |
| product type is not a financial pseudo-product | 2851 |
| quantity is positive, at least one service | 2860, 2855 |
| accepted pricing evidence is present | 2768 (P2-A invariant, unchanged) |
| service type is sellable / not air transportation | 2820, 2852 |
| service reference uniqueness inside the addition | 2861 |
| generic schema registered, versioned, attributes valid, service type matches | 2826–2828, 2824 |
| beneficiary exists in **this** order; exactly one where required | 2858, 2821, 2822 |
| seat / lounge / coverage air service exists in this order, is air, is not cancelled | 2858, 2859 |
| covered segment exists in this order | 2858 |
| typed detail invariants (baggage, meal, lounge, hotel, ground) | 2829–2838 |
| pricing basis is Order / OrderItem / OrderService | 2857 |
| no reversal, no `OriginalPricingLineId` | 2853 |
| every `SeparatelyPriced` service has primary customer value | 2854 |
| pricing line validity, source occurrence uniqueness, currency coherence, allocation reconciliation | P2-A rules, unchanged |

`AttachProductAddition` then performs no validation at all: it adds the item, the services and the links, calls
the existing `AttachPriceChange`, activates **only the new services**, increments `CommercialVersion` once,
recomputes the summary and raises one event. The only method it calls that can throw is
`OrderPriceChangeSet.Commit`, which rejects an already-committed set — impossible for a freshly staged set.

Staged entities are never reachable from the aggregate until attach, so EF never sees them: a rejected addition
leaves `Items`, `OrderServices`, `ItemServiceLinks`, `Changes`, `PriceChangeSets`, `PricingLines`,
`CustomerTotal`, `Amount`, `Commission`, `CommercialSummary` and all three counters byte-identical (asserted by
a full-snapshot comparison test).

## 2. Idempotency

No new idempotency mechanism was created. `AddProductService` uses the P0 stack unchanged —
`CommandReceipt` → `OperationOrderClaim` → `ServicingOperation` through `OrderOperationCoordinator.BeginAsync`
with the new `ServicingOperationKind.AddProduct = 9` (appended; no value reordered).

| Case | Behaviour |
|---|---|
| first call | one mutation; `OrderChange.OperationId` = the P0 operation id |
| exact replay | the committed `OrderChange` for that operation id is found **in persisted order state**; identical `OrderChangeId`, `PriceChangeSetId`, `OrderItemId` and `ServiceIds` are returned, no version moves, the provider is **not** called again |
| retry after local commit but before operation resolution | a fresh process/scope finds the same receipt → same operation id → the persisted `OrderChange` → resolves without re-mutating and without calling the provider |
| replay after a later unrelated mutation | replay recognition runs **before** the expected-version check, so the retry still resolves the original addition even though `CommercialVersion` has moved on |
| same key, different `SourceReference` or `ExpectedCommercialVersion` | rejected by the existing `CommandReceipt` fingerprint (2703) — both fields are inside the request intent |
| two commercial mutations for one operation | impossible: filtered unique index `IX_OrderChanges_OrderId_OperationId` |

A **rejected** command releases its claim before rethrowing, so a stale expected version does not lock the
order behind the recovery lease; a subsequent correct command succeeds immediately (tested).

## 3. Version semantics

| Counter | Rule | Where |
|---|---|---|
| `CommercialVersion` | `+1` once per accepted addition, never per service or per pricing fact | `IncrementCommercialVersion()` in `AttachProductAddition`, immediately before `Causes(...)` |
| `FinancialSequence` | `+1` once, because exactly one `PriceChangeSet` is committed | existing `AttachPriceChange` |
| `ObligationVersion` | `+1` only when `CustomerTotal` actually moves | existing `AttachPriceChange`; a settlement-only addition advances `CommercialVersion` and `FinancialSequence` but **not** `ObligationVersion` (tested) |
| `EventOrdinal` | reset by the commercial-version bump, then `1` for `OrderProductAdded` | `CommercialEventSequence` |

`ExpectedCommercialVersion` is mandatory for this mutation (2856 when absent, 2730 when stale) and is checked
only on the non-replay path.

## 4. `OrderItemPolicySnapshot` decision (§20, §60)

An exhaustive search of `src/` and `tests/` found **no reader of any `OrderItemPolicySnapshot` field**. The
entity is written at `Order.Create`, copied on split, persisted, and never read.

| Field | Current consumer | Superseded by P2-D service model? | Independent item-level meaning? | Source | Decision |
|---|---|---|---|---|---|
| `DeliveryModel` | none | yes — `OrderService.DeliveryModel` | no | hardcoded air constant | not populated for added items |
| `AccountingGranularity` | none | partly | unproven — no accounting consumer exists yet | hardcoded | not populated |
| `AssignmentMode` | none | yes — beneficiaries express assignment | no | hardcoded | not populated |
| `RequiresPassenger` | none | yes — `OrderServiceBeneficiary` | no | hardcoded | not populated |
| `RequiresSegment` | none | yes — air detail / coverage | no | hardcoded | not populated |
| `RequiresSupplierConfirmation` | none | yes — `OrderService.RequiresSupplierConfirmation` | no | hardcoded | not populated |
| `RequiresDocument` | none | yes — `OrderService.RequiresDocument` + `DocumentKind` | no | hardcoded | not populated |
| `RequiresFulfillment` | none | yes — `OrderService.RequiresReservation` | no | hardcoded | not populated |
| `CanBeUnassignedAtPurchase` | none | no | unproven | hardcoded | not populated |
| `CanBeTransferred` | none | no | unproven | hardcoded | not populated |
| `CanBePartiallyConsumed` | none | no | unproven | hardcoded | not populated |
| `RefundRuleRef` / `ChangeRuleRef` / `CancellationRuleRef` | none | no | would be real if a source supplied it | never supplied | not populated |
| `SupplierPolicyRef` | none | no | would be real if a source supplied it | never supplied | not populated |
| `SnapshotAt` / `SnapshotVersion` | none | n/a | only meaningful with a snapshot | hardcoded `"1.0"` | not populated |

**Decision:** `OrderItem.PolicySnapshot` is now **optional**. An added ancillary or mixed item receives `null`
rather than a fabricated `AirTransportPolicy()`. No schema change was needed — the FK already lives on the
dependent — so only the required-navigation configuration was removed. `Order.Create` is unchanged: its only
production source is air-fare-only, and P2-E is explicitly not a legacy cleanup. No `ItemPolicyRegistry`,
ancillary policy catalogue or rule engine was created. Removing the entity outright is deferred until the
Create path is revisited.

## 5. PriceTreatment consistency

The P2-D.1 definitions are frozen and reused. No `ServiceType → default PriceTreatment` mapping was
reintroduced: the accepted source states the treatment and the Domain **validates** it. A service declared
`SeparatelyPriced` must be backed by an **Original**, **CustomerBalance**, **primary** component
(`Fare` or `ProductCharge`) whose basis is `OrderService` and whose basis reference is that service, or the
whole addition is rejected (2854). A service-scoped `Tax`, `CarrierSurcharge` or `Fee` alone is not primary
value (tested).

The primary-value predicate now lives once, in `Domain/OrderAggregate/Policies/ServicePriceTreatmentPolicy`,
and both the AirPrice ACL resolver (create path) and `Order.AddProduct` (addition path) use it.

An item-priced bundle leaves both services `Included`, moves the total once, and invents no per-service
allocation. A source-supplied complete allocation is preserved verbatim and still reconciles against the line.

## 6. Document-family ticketing fix (§43–§45)

Before P2-E, `RequiredDocumentServiceIds()`, `IsTicketingComplete()`, `IssueEligibilityPolicy` and
`DeriveLegacyStatus()` all treated **every** `RequiresDocument` service as one ticketing scope. Adding a
pending EMD ancillary to an already ticketed air order would have made the order look unticketed.

Document completion is now family-scoped:

- `RequiredElectronicTicketServiceIds()` / `DocumentedElectronicTicketServiceIds()` /
  `IsElectronicTicketingComplete()` filter on `RequiresDocument && DocumentKind == ElectronicTicket`.
- `CompleteTicketing`, `IssueEligibilityPolicy` and `IssueOrderService`'s outstanding-set all use the ETKT
  scope, so an EMD or provider-document service can never block or reopen ticket issuance.
- `DeriveLegacyStatus()` derives the legacy `OrderStatus.Ticketed` from the ETKT scope only, keeping the old
  "all issued or cancelled" shape narrowed to that family.
- `RequiredDocumentServiceIds()` / `DocumentedServiceIds()` keep their broad meaning and still feed the
  read-model document summary.

No global `FullyDocumented` state was invented; `Service.DocumentStatus` + `DocumentKind` remain the
authoritative truth, and the independent EMD summary belongs to P2-F.

## 7. Sibling-service dependency boundary

P2-E depends on exactly one new semantic port, `Domain/Ports/ProductAddition/IAcceptedProductAdditionPort`,
whose request carries only Ordering-owned values (`OperationKey`, `OrderId`, `OperationId`, `SourceReference`,
`SaleCurrencyId`) and whose result is the Ordering-owned `AcceptedProductAddition` graph. The Domain sees no
AirPrice, Offer, Pricing, Inventory or NDC type, and the public command carries only
`OrderId + SourceReference + ExpectedCommercialVersion + Idempotency-Key` — no price, tax, commission,
fulfilment rule, snapshot internal, attributes JSON, owner airline or actor.

Two implementations exist and neither is a fake production ACL:

- `DeterministicProductAdditionAdapter` (`Providers/Testing/`, registered only behind the existing
  deterministic-adapter switch) — the P2-E test double, which also counts provider calls.
- `UnconfiguredProductAdditionProvider` — the default registration; it fails closed with 2863 (HTTP 501)
  because no real accepted-addition source exists yet.

Provider rejection (expired or unusable quote) surfaces as 2862 and mutates nothing; Ordering never reprices
locally, and a new price requires a new source decision. No sibling repository was read or modified.

## 8. Reason codes added

`2850` order not eligible · `2851` product type not sellable · `2852` air transportation cannot be added ·
`2853` addition cannot reverse · `2854` separately-priced service without value · `2855` addition requires a
service · `2856` expected commercial version required · `2857` pricing basis not supported ·
`2858` reference not resolved · `2859` target already cancelled · `2860` quantity must be positive ·
`2861` repeated service reference · `2862` accepted addition not usable · `2863` source not configured.
