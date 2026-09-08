# P2-G.1 — OTA Resource-Authorization Audit (binding, §24)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09

Bounded audit of the **current** public API surface for endpoints that accept an existing `OrderId`, to
establish that no customer-facing endpoint returns Order information or starts stateful/external work
without first proving `AuthenticatedCustomerId == Order.CustomerId`. Scope is the eight controllers that
exist today; no future or planned endpoint is covered.

---

## 1. Every endpoint that accepts an existing OrderId

| Endpoint | Read/Write | Authenticated identity source | Ownership check | Check occurs before external IO? | Result on mismatch | Decision |
|---|---|---|---|---|---|---|
| `GET Api/v1/Bookings/{orderId}` (OTA) | Read | `ICallerContext.CustomerId` (claim `customer_id`, via `IOrderCustomerAccessGuard`) | **Added** — `EnsureOwnedAsync(orderId)` | Yes — first statement, before `GetOrderDetailsQuery` | `BusinessException 2500` / HTTP 404, message `Order '{id}' was not found.` | **Fixed in P2-G.1** |
| `POST Api/v1/Bookings/{orderId}/Change` (OTA) | Write | `ICallerContext.CustomerId` (via `IOrderCustomerAccessGuard`) | **Added** — `EnsureOwnedAsync(orderId)` | Yes — before `IdempotencyKey.Require`, `OrderOperationCoordinator.BeginAsync`, CommandReceipt / ServicingOperation / OperationOrderClaim, `AcceptSelectedQuotedOfferAsync`, provider operation key, `Order.AddProduct`, PriceChangeSet, projection, outbox | `BusinessException 2500` / HTTP 404, identical to the read | **Fixed in P2-G.1** |
| `GET Backoffice/v1/Orders/{orderId}/Details` | Read | Airline principal (`AuthorizationSurface.Backoffice`) | None — deliberate | n/a | n/a | **Unchanged** — airline staff surface; a customer restriction would break it |
| `POST Backoffice/v1/Orders/{orderId}/Reserve` | Write | Airline principal | None — deliberate | n/a | n/a | **Unchanged** |
| `POST Backoffice/v1/Orders/{orderId}/Issue` | Write | Airline principal | None — deliberate | n/a | n/a | **Unchanged** |
| `POST Backoffice/v1/Orders/{orderId}/Change` | Write | Airline principal | None — deliberate | n/a | n/a | **Unchanged** — shares the same `IOrderChangeService` as OTA |
| `POST Backoffice/v1/Orders/{orderId}/Withdraw` | Write | Airline principal | None — deliberate | n/a | n/a | **Unchanged** |
| `POST Backoffice/v1/Orders/{id}/Cancel`, `/Split`, `/Remarks`, `/Documents/{documentId}/Void` | Write | Airline principal | None — deliberate | n/a | n/a | **Unchanged** |
| `GET Internal/v1/Orders/{id}` and `POST {id}/Reservations`, `/Payments`, `/Issuance`, `/Cancel`, `/Split`, `/Remarks`, `/Documents/{documentId}/Void` (8) | Read/Write | Service-internal | None — deliberate | n/a | n/a | **Unchanged** — maintenance/debug channel, not a production sell path |
| `POST Internal/v1/FulfillmentTasks/{id}/Run`, `/Retry` | Write | Service-internal | None — takes a task id, not an OrderId | n/a | n/a | **Unchanged** |

### Endpoints that do not take an existing OrderId

| Endpoint | Note |
|---|---|
| `POST Api/v1/Bookings/FlightOffers` (OTA) | Creation. Established `Order.CustomerId` from `_identity.CurrentCustomerId ?? 0`. **Changed** to `IOrderCustomerAccessGuard.RequireCustomerId()` — see §3. |
| `POST OtaPanel/v1/Bookings/FlightOffers` | Creation, customer-facing panel. Same `?? 0` defect, same fix. |
| `POST Backoffice/v1/Orders/FlightOffers`, `GET Backoffice/v1/Orders/Paginated` | Airline surface; unchanged. |
| `Service/v1/Bookings` | Declared shell, **no actions**. Nothing to guard today. |
| `V1/_Shared/PingController` | Liveness. No Order. |

**Finding:** exactly two active endpoints accepted an existing `OrderId` on a customer-facing channel
without an ownership check. Both are now guarded. No third instance was found.

## 2. The seam

```
IOrderCustomerAccessGuard          Application/OrderAggregate/Access/
  long RequireCustomerId()             trusted identity, fail closed
  Task EnsureOwnedAsync(orderId, ct)   ownership, non-disclosing
```

Implemented from **Ordering-owned local truth only**: `IOrderRepository.FindCustomerIdAsync(orderId)`
projects `Order.CustomerId` — one indexed key lookup, `AsNoTracking`, no aggregate materialisation, no
call to Core, Identity, Aegis, Offer, Pricing, JetPay or Ledger.

Fail-closed rule, in order:

1. `ICallerContext.IsAuthenticated == false` → `BusinessException 2890` / HTTP 403.
2. `ICallerContext.CustomerId is null` → `BusinessException 2890` / HTTP 403.
3. `ownerCustomerId != customerId` → `ExceptionFactory.OrderNotFound(orderId)` — 2500 / HTTP 404.

Step 3 is a single comparison against a nullable value, so **an unknown order and another customer's order
take the identical branch** and produce a byte-identical response apart from the id the caller itself sent.
No `?? 0`, no `?? -1`, no "treat absent identity as 0" anywhere on the path.

### Deviation from the suggested signature

The brief suggested `EnsureOwnedAsync(orderId, customerId)`. The implemented signature is
`EnsureOwnedAsync(orderId, cancellationToken)` and the guard resolves the customer itself from
`ICallerContext`. Reason: a two-argument form lets a call site pass a customer id, and the only safe value
it could ever pass is the one the guard would read anyway — the parameter would be pure risk. The narrowed
signature makes "authorize using a caller-supplied customer id" **unrepresentable**, which is the rule the
brief states. A test asserts the method takes no customer parameter.

## 3. `CurrentCustomerId ?? 0` on the creation paths

Both customer-facing creation endpoints minted `Order.CustomerId` from `_identity.CurrentCustomerId ?? 0`.
That is the exact defaulting pattern the brief prohibits, and it is load-bearing for this fix: an order
created with `CustomerId = 0` has a fabricated owner, so the ownership check downstream would be comparing
against a value that never belonged to anybody. Both now call `RequireCustomerId()` and fail closed with
2890 when customer identity is absent. This is the minimum change that makes the guard sound; no other
creation behaviour was touched.

## 4. Ordering of the write path

`OtaController.Change` establishes ownership **before** every observable effect:

```
EnsureOwnedAsync(orderId)          <-- throws here
IdempotencyKey.Require(Request)
IOrderChangeService.AddServiceAsync
   OrderOperationCoordinator.BeginAsync   (CommandReceipt, OperationOrderClaim, ServicingOperation)
   IOrderChangeQuoteProvider.AcceptSelectedQuotedOfferAsync   (provider operation key)
   Order.AddProduct                       (OrderChange, OrderItem, OrderService, PriceChangeSet, PricingLines)
   IOrderProjector.ProjectAsync
   IOutboxWriter (OrderPricingChanged)
```

The exit-gate test captures a full before/after record — CommercialVersion, FinancialSequence,
ObligationVersion, CustomerTotal, CommandReceipt / ServicingOperation / OperationOrderClaim counts,
OrderChange / OrderItem / OrderService / PriceChangeSet / PricingLine counts, pricing outbox message count,
`ProjectionRevision` and the projection `SnapshotJson` — and asserts equality, plus quote provider
`CallCount == 0` and zero dispatched domain events. A cross-customer request is therefore observationally
equivalent to one that never entered the commercial workflow.

## 5. Replay is not a bypass

Possession of an `OrderId`, an `Idempotency-Key`, a `QuotedOfferId` or an `OperationId` grants nothing.
The guard runs before the idempotency key is even read, so a non-owner replaying an owner's key gets the
same non-disclosing 404 as any other cross-customer request, calls no quote provider, and changes no
receipt, operation or claim state. Asserted by
`A_non_owner_cannot_replay_the_owners_idempotency_key`.

## 6. Non-disclosure

The mismatch response carries only `Order '{id}' was not found.` where `{id}` is the id the caller sent.
Tests assert the message contains no other customer's `CustomerId`, no traveller surname, no customer
total, and no source offer id — and that the unknown-order and cross-customer messages are identical once
the caller-supplied id is normalised.

## 7. Backoffice is untouched

`BackofficeController` and `BackofficeOrderLifecycleController` take no `IOrderCustomerAccessGuard`
(asserted by test) and continue to operate on any order regardless of `CustomerId` — including for an
airline caller whose `ICallerContext.CustomerId` is null, which is the normal Backoffice shape. Both
channels still invoke the single `OrderChangeService`; no OTA-specific commercial mutation, quote
acceptance or Add Service implementation exists (the pre-existing "single implementation" test still holds).

## 8. Not implemented

Aegis / RBAC / ABAC redesign; permission DSL or policy engine; generic resource ACL subsystem; customer
delegation; travel-agency delegated servicing; corporate hierarchy access; impersonation; shared-order
access; audit-log subsystem; OrderHistory; refund, exchange, reissue; SSR; P2-H; P3. `IIdentityService`
and `ICallerContext` were not redesigned — the guard consumes the existing claim-derived context.
