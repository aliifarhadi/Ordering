# P2-E — Idempotent AddProduct / Add Ancillary Commercial Mutation

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline: P2-D.1 (`853e2db`), **549 passed / 0 failed**. P0 – P2-D.1 frozen. **P2-F and P2-G not started.**

Evidence: [`audit/p2/P2-E-TEST-RUN.txt`](audit/p2/P2-E-TEST-RUN.txt) ·
Binding audit: [`audit/p2/P2-E-COMMERCIAL-MUTATION-AUDIT.md`](audit/p2/P2-E-COMMERCIAL-MUTATION-AUDIT.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `dotnet ef database update` (`DotAirOrderNew`) | applied; duplicate-operation pre-check raised nothing |
| `AeroTech.Ordering.Domain.Tests` | **399 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **250 passed**, 0 failed (real SQL Server) |
| Total | **649 passed, 0 failed** (baseline 549 → +100, zero regressions) |
| New P2-E tests | 75 domain + 25 persistence = **100** |

## 2. The mutation

```
existing Order + SourceReference + ExpectedCommercialVersion + Idempotency-Key
        ↓ IAcceptedProductAdditionPort
AcceptedProductAddition
        ↓ Order.AddProduct
1 OrderChange(AddProduct)        OperationId = P0 operation
1 OrderItem                      ProductSnapshot + CommercialTermsSnapshot, no fabricated policy
1..N OrderServices               activated, reservation/document still Pending
N OrderItemServiceLinks          LinkedByChangeId = the AddProduct change
1 committed PriceChangeSet       ChangeId = the same change, Reason = AddProduct
N PricingLines / AllocationSets  accepted verbatim
CommercialVersion +1  ·  FinancialSequence +1  ·  ObligationVersion +1 only if CustomerTotal moved
1 OrderProductAdded domain event
```

## 3. Exception-atomic by construction

`StageProductAddition` builds the entire candidate — change, item, both snapshots, every service with its
beneficiaries, typed detail and coverage, every membership link, the mapped pricing lines and their allocation
sets — validating all of it while the aggregate stays untouched. Only then does `AttachProductAddition` attach,
commit and version, performing no further validation. A rejected addition leaves items, services, links,
changes, price change sets, pricing lines, `CustomerTotal`, the amount and commission caches, the commercial
summary and all three counters identical — asserted by a full-snapshot comparison, including the cases
"invalid second pricing line" and "invalid allocation".

The full validation list and its reason codes are in the audit.

## 4. Idempotency and recovery

The P0 stack is reused unchanged; `ServicingOperationKind.AddProduct = 9` was appended. `OrderChange.OperationId`
is the durable commercial recovery proof, and replay is resolved from **persisted order state**, not from an
in-memory flag: a fresh process retrying the same key finds the same operation id, finds the committed
`OrderChange`, and returns the same `OrderChangeId`, `PriceChangeSetId`, `OrderItemId` and `ServiceIds`
without calling the provider (asserted by the provider call counter).

Replay recognition deliberately runs **before** the expected-version check, so retrying an addition after an
unrelated later mutation still resolves the original addition instead of failing on a moved
`CommercialVersion`. Same key with a different `SourceReference` or `ExpectedCommercialVersion` is rejected by
the existing receipt fingerprint. A rejected command releases its claim, so a stale version does not leave the
order blocked.

New migration **`P2ECommercialOperationUniqueness`** adds the filtered unique index
`UNIQUE(OrderId, OperationId) WHERE OperationId IS NOT NULL` on `OrderChanges`, preceded by a fail-closed
pre-check that `THROW`s on historical duplicates rather than deleting or repointing commercial history. No
previously applied migration was edited.

## 5. Boundary — no offer/pricing implementation

`IAcceptedProductAdditionPort` is the only new dependency. Its request carries Ordering-owned values only, and
its result is the Ordering-owned `AcceptedProductAddition` graph. The Domain never sees an AirPrice, Offer,
Pricing, Inventory or NDC type. The public command carries only `OrderId`, `SourceReference`,
`ExpectedCommercialVersion` and the required `Idempotency-Key` header — never price, tax, commission,
fulfilment rules, snapshot internals, attributes JSON, owner airline or actor, all of which come from the
accepted result and trusted caller context.

The port has a deterministic test double (`Providers/Testing/`, behind the existing deterministic-adapter
switch) and a fail-closed default (`UnconfiguredProductAdditionProvider`, 2863 / HTTP 501). No fake production
ancillary ACL was written and no sibling repository was touched. A provider that rejects an expired or unusable
quote surfaces 2862 and mutates nothing — Ordering never reprices locally.

## 6. Existing-order references, not source refs

Unlike Create, the order already has identities, so the accepted addition targets `OrderTravellerId`,
`OrderServiceId` and `OrderSegmentId` directly — no `"traveller:123"` magic strings and no provider database
ids. The P2-D service model is reused rather than duplicated: `AttachDetail` became one shared, id-based switch
used by both the Create path (which resolves source refs first) and the addition path (which validates order
ids first), so seat, baggage, meal, lounge, hotel, ground-transport and generic details keep exactly one set of
rules and one persisted shape. Only Seat and Lounge needed small id-carrying input records, because only they
carry a target reference.

Every P2-D invariant still holds for added services: beneficiary and coverage targets must belong to the same
order, a covered or associated air service must actually be `AirTransportation` and must not be cancelled,
`SeatAssignment` needs exactly one beneficiary, and generic services must satisfy the registered schema — whose
profile, resolved once at creation, is persisted onto the service.

## 7. What P2-E deliberately does not do

`AirTransportation` cannot be added (2852), so no journey, segment, traveller, fare construction, pricing unit
or fare component is ever created — existing air services and segments may only be **referenced**. Financial
pseudo-products (`Penalty`, `ServiceFee`, `Credit`, `Voucher`, `TaxAdjustment`, `ManualAdjustment`) and
financial pseudo-services stay blocked; an order-level `Fee`, `Tax`, `Discount` or `Commission` inside an
addition is a pricing line and never becomes a service. Reversals are rejected (2853) — P2-E is additive.
Commission and tax stay source-owned and are never calculated locally. No supplier is called, no reservation is
made, nothing is documented, no EMD, coupon, document number or `DocumentPriceLink` is created, and the
existing electronic ticket, its coupons and its value attribution are provably untouched.

`ProductType.Ancillary` was appended for generic services (Wi-Fi, priority, extra seat) that have no honest
existing category; no per-service product types were added and no existing value was reordered.
`OrderItem.Quantity` and `UnitOfMeasure` come from the accepted source — never from the service, beneficiary,
segment, night, piece or guest count.

## 8. Document family fix

Legacy ticketing treated every `RequiresDocument` service as one completion scope, which would have made an
already ticketed air order look unticketed as soon as a pending EMD ancillary was added. Completion is now
scoped to `DocumentKind == ElectronicTicket` through `RequiredElectronicTicketServiceIds()`,
`DocumentedElectronicTicketServiceIds()` and `IsElectronicTicketingComplete()`, used by `CompleteTicketing`,
`IssueEligibilityPolicy`, `IssueOrderService` and the legacy `OrderStatus.Ticketed` derivation. The broad
document view is retained for the read-model summary. No global `FullyDocumented` state was invented.

## 9. Item policy

`OrderItemPolicySnapshot` has no reader anywhere in the solution and every one of its fields is now owned by
`OrderService`. Rather than stamping a false `AirTransportPolicy()` on a hotel or baggage item,
`OrderItem.PolicySnapshot` is optional and added items receive `null`. No schema change was required; the
Create path is unchanged; no policy registry, catalogue or rule engine was created. The field-by-field
justification is in the audit.

## 10. API and projection

`POST Backoffice/v1/Orders/{orderId}/AddProduct` and `POST Api/v1/Bookings/{orderId}/AddProduct` both call the
single `IAddProductService` — one flow, two audiences, no duplicated business logic — and both require the
`Idempotency-Key` header, now shared through `RestApi/_Shared/IdempotencyKey` instead of a per-controller copy.

`GetOrder` shows the new item with its product and commercial-terms snapshots, quantity and unit of measure,
the new services with beneficiaries, coverage, price treatment, fulfilment profile and typed or generic detail,
the commercial change history with its operation id and the services each change created, and every
`PriceChangeSet` with its financial sequence and customer-balance impact. The order search projection reflects
the new total and commercial version through the existing synchronizer. The projector's expanded include graph
is split-queried, and no upstream call happens on read.

## 11. Not implemented

EMD aggregate, issuance, stock or coupon (P2-F); external `OrderPricingChanged` contract (P2-G); ancillary
supplier reservation; refund, refund mask, void, exchange, reissue, revalidation; remove or change product;
moving a service between items; split; new air segment, traveller or fare construction; JetPay; Ledger; offer,
pricing or inventory redesign. The P2-E lineage
(`OrderChange → PriceChangeSet → PricingLines` and `OrderChange → OrderItemServiceLinks → Item / Services`) is
sufficient for P3 to determine later which change added which item, which services came with it and which
accepted value priced them; no refund-specific field was added.
