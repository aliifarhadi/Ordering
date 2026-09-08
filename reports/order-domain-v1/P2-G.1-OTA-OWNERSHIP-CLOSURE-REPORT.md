# P2-G.1 — OTA Order Ownership / Resource Authorization Closure

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09
Baseline: P2-G, **788 passed / 0 failed**. P0 – P2-F frozen. **P2-H not started.**

Evidence: [`audit/p2/P2-G.1-TEST-RUN.txt`](audit/p2/P2-G.1-TEST-RUN.txt) ·
Audit: [`audit/p2/P2-G.1-OTA-OWNERSHIP-AUDIT.md`](audit/p2/P2-G.1-OTA-OWNERSHIP-AUDIT.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **451 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **354 passed**, 0 failed (real SQL Server) |
| Total | **805 passed, 0 failed** (baseline 788 → +17, zero regressions) |
| Migration | none — `Order.CustomerId` already existed; no schema change |

## 2. The defect

`GET /Api/v1/Bookings/{orderId}` and `POST /Api/v1/Bookings/{orderId}/Change` retrieved by `OrderId` alone.
Any authenticated OTA caller could read any Order and commit a commercial mutation against it: cross-customer
disclosure of travellers, pricing, documents and record locator, and cross-customer inventory, pricing and
accountable-document consequences. Both are closed.

## 3. The seam

One small explicit access guard in the Application layer — not an authorization framework:

```
IOrderCustomerAccessGuard                Application/OrderAggregate/Access/
  long RequireCustomerId()                  trusted identity or fail closed (2890 / 403)
  Task EnsureOwnedAsync(orderId, ct)        ownership or non-disclosing 404 (2500)
```

Backed by **Ordering-local truth only**: a new `IOrderRepository.FindCustomerIdAsync(orderId)` that projects
`Order.CustomerId` as one `AsNoTracking` key lookup — no aggregate materialisation, and no call to Core,
Identity, Aegis, Offer, Pricing, JetPay or Ledger. Identity comes from `ICallerContext` (claim `customer_id`),
never from a request body, query string or header.

The suggested `EnsureOwnedAsync(orderId, customerId)` signature was deliberately narrowed to
`EnsureOwnedAsync(orderId, ct)` so that no call site *can* supply a customer id. Recorded in the audit §2.

## 4. Fail closed, and disclose nothing

| Condition | Result |
|---|---|
| caller not authenticated | `2890` / **403** — no order is read |
| authenticated, no `customer_id` claim | `2890` / **403** — no order is read |
| order belongs to another customer | `2500` / **404**, `Order '{id}' was not found.` |
| order does not exist | `2500` / **404**, identical response |

The last two share one branch — `ownerCustomerId != customerId` over a nullable value — so they are
indistinguishable by code, status or message. No `?? 0` / `?? -1` defaulting exists anywhere on the path;
the two customer-facing **creation** endpoints that still used `CurrentCustomerId ?? 0` to mint
`Order.CustomerId` were corrected to the same fail-closed rule, since a fabricated owner would have made the
ownership check meaningless.

## 5. Ownership precedes every effect

`EnsureOwnedAsync` is the first statement of both OTA endpoints — ahead of `IdempotencyKey.Require`,
`OrderOperationCoordinator.BeginAsync`, CommandReceipt / ServicingOperation / OperationOrderClaim,
`AcceptSelectedQuotedOfferAsync`, the provider operation key, `Order.AddProduct`, PriceChangeSet creation,
projection and outbox publication.

The exit-gate test compares a full before/after record of the target order — CommercialVersion,
FinancialSequence, ObligationVersion, CustomerTotal, receipt/operation/claim counts, OrderChange /
OrderItem / OrderService / PriceChangeSet / PricingLine counts, pricing outbox count, `ProjectionRevision`
and the projection `SnapshotJson` — and asserts it unchanged, with quote provider `CallCount == 0` and no
domain event dispatched. A cross-customer request is observationally equivalent to one that never entered
the commercial workflow.

**Replay is not authorization.** The guard runs before the idempotency key is read, so a non-owner replaying
an owner's `Idempotency-Key` on the owner's `OrderId` receives the same 404, learns nothing, and mutates no
receipt, operation or claim state.

## 6. What did not change

`IOrderChangeService` and its single implementation, `Order.AddProduct`, the quote port, the P0 operation
foundation, the typed `OrderView` (still `OrderView`, no `object` / `JsonElement`), `OrderPricingChanged`,
the projector, and every P2-E / P2-F / P2-G semantic. Backoffice is **not** customer-restricted: neither
Backoffice controller takes the guard (asserted), and a Backoffice change succeeds for an airline caller
with no `CustomerId` at all (asserted). Both channels still call the same commercial mutation — there is no
OTA-specific Add Service, quote acceptance or OrderChange implementation.

## 7. Tests

17 new tests in `Persistence.Tests/P2/OtaOrderOwnershipTests.cs`, driving the **real `OtaController`** with
the real `OrderChangeService`, the real guard, the real repository and the real `GetOrderDetailsQuery`
handler over SQL Server: owner read returns the typed view with no provider touched; cross-customer read is
a non-disclosing 404 carrying no foreign customer id, traveller name, total or offer id; unknown and foreign
orders are indistinguishable; absent and unauthenticated customer context both fail closed; the
cross-customer change exit gate; the owner's change still succeeds; non-owner replay discloses nothing;
Backoffice stays unrestricted; and structural tests that the customer-facing controllers take the guard,
Backoffice does not, the public request carries no caller-supplied customer identity, the guard depends only
on `IOrderRepository` + `ICallerContext`, and the OTA read contract is still `OrderView`.

## 8. Not implemented

Aegis / RBAC / ABAC redesign; permission DSL or policy engine; generic resource ACL subsystem; customer
delegation; agency delegated servicing; corporate hierarchy access; impersonation; shared-order access;
audit-log subsystem; OrderHistory; refund, exchange, reissue; SSR; P2-H; P3.
