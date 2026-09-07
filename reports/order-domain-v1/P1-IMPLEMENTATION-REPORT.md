# P1 — Core Order Vertical Slice: Implementation Report

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
**Scope:** first coherent production-quality Ordering vertical slice. P0/P0.5/P0.6 treated as frozen foundation. P2 not started.

Evidence: [`audit/p1/P1-TEST-RUN.txt`](audit/p1/P1-TEST-RUN.txt)
Prior phases: [`P0-CLOSURE-REPORT.md`](P0-CLOSURE-REPORT.md) · [`P0.5-CLOSURE-REPORT.md`](P0.5-CLOSURE-REPORT.md) · [`P0.6-CLOSURE-REPORT.md`](P0.6-CLOSURE-REPORT.md)

---

## 1. Status

**The P1 vertical slice runs end to end against real SQL Server with deterministic test adapters.**

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | **0 errors**, 2 warnings |
| `AeroTech.Ordering.Domain.Tests` | **63 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **114 passed**, 0 failed (real SQL Server) |
| Total | **177 passed, 0 failed** (was 121 at end of P0.6) |

The exit-gate flow works: create order → persist canonical Order → synchronous read projection → reserve air services through a port → persist reservation evidence → verify funding coverage through a port → evaluate issue eligibility → allocate a controlled ticket number → issue through a document port → persist `ElectronicTicket` + `TicketCoupon`s → update the projection → replay safely on the same operation → redisplay the complete result locally.

---

## 2. What was implemented

### 2.1 Order aggregate moved toward the v1 model

Evolved in place — **no `OrderV2` / `DomainV2` was created**, and there is one Ordering domain model.

| Added | Purpose |
|---|---|
| `Order.CommercialSummary` | commercial truth (`Draft/Active/Cancelled/Inactive/Closed`), deterministically recomputed from current service state in the same transaction as the mutation |
| `OrderTimeLimit[]` | typed time limits (`Ticketing/Payment/Reservation/NameEntry`) with lifecycle, replacing a bare `TimeToLive` field as the model |
| `OrderExternalReference[]` | provider/offer/quote/document references, deduplicated |
| `OrderLineage` | `RootOrderId` / `ParentOrderId` / `SplitFromChangeId`, rooted at the order on create |
| `Order.ObligationVersion` | payment/payable staleness version supplied to the funding port |
| `Order.AcceptCommercially`, `ApplyReservationOutcome`, `ApplyReservationReleased`, `ApplyIssuedDocuments`, `WithdrawBeforeTicketing` | the P1 mutations, with correct version semantics |

**The global `OrderStateMachine` was deleted.** `OrderStatus` survives only as a **derived legacy facet** recomputed from canonical facts — it no longer gates anything. See §5.

Existing structures already matched the target shape and were kept rather than renamed: `OrderItinerary` ≈ Journey, `OrderSegment` ≈ JourneySegment (already with `OrderSegmentLeg` children, so a sold passenger segment can span several operational legs), `OrderTraveller` ≈ Traveler, `OrderItem`/`OrderService` with typed `OrderAirTransportService` detail by composition (no EF TPT).

### 2.2 New aggregates

| Aggregate | Notes |
|---|---|
| `FulfillmentReservation` (+ `FulfillmentReservationService`) | external binding/evidence, **not** capacity truth. Member statuses `Pending/Waitlisted/Confirmed/Rejected/Released/Expired/Unknown`; root status is a deterministic roll-up including `Mixed`. One reservation per operation (unique index). |
| `DocumentStock` (+ `DocumentStockAllocation`) | controlled sequential allocation inside a configured range; **no random ticket numbers**. Allocation is idempotent per `(OperationId, DocumentRole)`; states `Reserved/Issued/Retired`; retired numbers are never recycled; unique indexes on `(stock, serial)`, `(operation, role)` and document number; `RowVersion` for concurrency. An unconfigured check-digit profile is **refused**, not invented. |
| `ElectronicTicket` (+ `TicketCoupon`, `DocumentPriceLink`) | replaces the legacy combined `TrafficDocument` as the P1 target document model. Coupon carries its `IssuedSegmentSnapshot`, its `OrderServiceId`/`JourneySegmentId` binding, and its issuance value. Financial status (`Open/Used/Void/…`) is kept separate from `ControlStatus`; no check-in/boarding/flown status is modelled as a financial state. `DocumentPriceLink` freezes the issue-time monetary attribution. |

### 2.3 Ordering-owned ports (external services are not implemented)

`IReservationPort`, `IFundingCoveragePort`, `IDocumentIssuancePort` — each with explicit **Confirmed / Rejected / Pending / Unknown** semantics, plus release and recovery operations. No FlightFlow/JetPay/issuer wire DTO was frozen; no sibling service was implemented or audited.

Deterministic adapters live in `Providers/Testing` and are registered **only** when `Providers:UseDeterministicTestAdapters` is explicitly true (Development only). By default **no port is satisfied**, so a fake can never silently become production evidence.

### 2.4 Eligibility policies replace the global state machine

`IssueEligibilityPolicy`, `ReserveEligibilityPolicy`, `WithdrawEligibilityPolicy` return an `EligibilityDecision` (`Allowed | Denied | PendingEvidence`) with stable reason codes and the effective service scope. Issue evaluates canonical facts: commercial summary, service state, traveler/segment validity, per-service reservation confirmation, document stock availability, and confirmed funding covering the customer total. **Pending or Unknown funding never authorizes issuance.** The command re-evaluates; no read-model label authorizes anything.

### 2.5 Application slice on the P0 foundation

`CreateOrderService`, `ReserveOrderService`, `IssueOrderService`, `WithdrawOrderService`, coordinated by `OrderOperationCoordinator`:

```
CommandReceipt (stable OperationId) → OperationOrderClaim → ServicingOperation → provider step
```

Provider idempotency keys derive from the operation (`{step}:{operationId}`), never from an attempt counter. Stock numbers are allocated and committed **before** the issuance dispatch. A `Pending`/`Unknown` document outcome returns without resolving the claim, so the operation stays blocking and no replacement number is allocated. `Create` uses the receipt to allocate and remember the Order identity before any effect, so a retry returns the original order.

### 2.6 Read model and redisplay

`OrderDetails` (JSON snapshot with independent facets), plus the existing targeted `OrderSearch` / `OrderTravelerSearch` / flight correlation tables — not a normalized query schema. `OrderProjector` re-reads canonical state inside the transaction and writes all read models through the **P0 shared command/query transaction**; `ProjectionRevision` is tracked separately from `CommercialVersion`. `GetOrderDetails` reads only the local projection — proven by a test asserting zero port calls during redisplay.

### 2.7 API

`Backoffice/v1/Orders/{orderId}/Details|Reserve|Issue|Withdraw`, PascalCase, existing channel conventions, `Idempotency-Key` header required for mutations. No internal provider action is exposed.

---

## 3. Version semantics (verified by test)

| Event | CommercialVersion |
|---|---|
| Create | **= 1** |
| Reservation confirmed / released / waitlisted / rejected / unknown | **unchanged** |
| Funding coverage verified | **unchanged** |
| Document issued | **unchanged** |
| Pre-ticket withdrawal | **+1, exactly once** |

`RowVersion` remains the separate concurrency token; `ProjectionRevision` is separate again.

---

## 4. Test coverage against the P1 requirement list

All 25 required behaviours are covered. Highlights, all against real SQL Server unless noted:

multi-traveler creation; multi-segment creation; multiple items/services (including an item holding several services when the source priced them together); stable service identity; **create idempotency** (replay returns the original order; same key + different body → 2703); create = version 1; one commercial mutation = one increment; reservation does not increment; confirmed reservation permits issue; waitlisted/rejected reservation blocks issue (2729) and an **unknown** reservation blocks it via its unresolved claim (2700); insufficient/pending/unknown funding all block issue; confirmed funding satisfies the gate; **fakes are namespace-isolated and opt-in only, and no production adapter implements a P1 port**; **concurrent allocation from one stock never persists a duplicate number**; retry issues no second ticket and calls the document port only twice; unknown issuance triggers no blind reissue and leaves the number `Reserved`; coupons map to the correct traveler/service/segment; issue-time price attribution is retained and reconciles to the order total; GetOrder redisplays with zero upstream calls; projection commits atomically with the command; pre-ticket withdrawal releases the reservation and creates **no** fake refund; the operation claim blocks a conflicting irreversible action; replay of a completed issue resumes the original operation and ticket numbers.

Two defects were found **by** the tests and fixed: `CommandReceiptStore.AttachOrderAsync` was saving through the P0.6 write boundary while the new Order was pending (it now enlists so receipt and Order commit atomically), and the harness reused a fixed ID seed across tests.

---

## 5. Legacy that remains, and why

This session deliberately did **not** attempt a full legacy purge; the brief asked for incremental replacement, and a same-session purge of 51 files touching `OrderStatus` alongside the whole slice would have risked leaving the repository broken.

| Legacy | Status |
|---|---|
| `OrderStateMachine` | **deleted** |
| `OrderStatus` | retained as a **derived, non-normative legacy facet** (`DeriveLegacyStatus`) so existing events/read model/consumers keep working. Gates nothing in P1. Removal belongs with the slice that replaces the remaining events. |
| `Payment` aggregate, `PaymentService`, `PayOrder` | still present, **off the P1 path**. P1 uses `IFundingCoveragePort` only. Removal is P3/JetPay work. |
| `TrafficDocument` aggregate + void/cancel services | still present, **off the P1 path**. `ElectronicTicket` is the P1 target model. Removal belongs with P3 servicing. |
| Legacy `OrderReservationService` / `OrderIssuanceService` / ticket-number generator | still present and still wired to the old commands; the P1 endpoints use the new services. Both paths compile; only the new one is P1. |
| `Order.Split`, `Order.Expire`, remark mutators | untouched P3 concerns; already recorded as non-normative in P0.6. |

**This duplication is temporary and tracked.** It is the main piece of P1 debt.

---

## 6. Genuine Ordering blockers

**None.** No external service blocked P1: every boundary is an Ordering-owned port with explicit outcome semantics and a deterministic test adapter, per the standing external-dependency rule.

Carried, non-blocking:

- **BD-3** — the real JetPay contract still blocks *enabling production* payment-backed issuance. It did not block P1.
- **BD-2** — `RoundingFactor` representation debt.
- **F-6 / F-7 / I-2** — legacy Domain→Contracts coupling, hardcoded `TenantId`, retired identity accessors.
- Review item 6 from P0.6 (three internal operation enums living in `Contracts/AeroTech.Messages`) remains open by owner instruction; enum placement was not reopened in this session.

---

## 7. Not implemented (correctly out of P1 scope)

Refund, exchange/reissue, revalidation, EMD completeness, DCS, disruption recovery, group booking, full fare construction, ATPCO/tax/commission engines, JetPay/Ledger/StoredValue implementations, interline. No Ledger event was added and no GL logic exists. Pricing remains the minimal accepted commercial value needed to establish the funding requirement and issue-time attribution.

P2 was not started.
