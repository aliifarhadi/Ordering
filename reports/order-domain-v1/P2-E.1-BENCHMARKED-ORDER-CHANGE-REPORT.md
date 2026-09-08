# P2-E.1 — Benchmark-Aligned Order Change / Add Service

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline: P2-E, **649 passed / 0 failed**. P0 – P2-D.1 frozen. **P2-F not started.**

Evidence: [`audit/p2/P2-E.1-TEST-RUN.txt`](audit/p2/P2-E.1-TEST-RUN.txt) ·
Scope audit: [`audit/p2/P2-E.1-BENCHMARK-AND-SCOPE-AUDIT.md`](audit/p2/P2-E.1-BENCHMARK-AND-SCOPE-AUDIT.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **399 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **272 passed**, 0 failed (real SQL Server) |
| Total | **671 passed, 0 failed** (baseline 649 → +22, zero regressions) |
| Migration | none — P2-E.1 corrects vocabulary and contracts, not storage |

## 2. What was wrong

P2-E's internal mutation was correct, but it was exposed under an invented business vocabulary. `AddProduct`
named the *implementation shape* — one `OrderItem` plus its services — as if it were an airline operation. No
industry standard or PSS benchmark has a post-sale "AddProduct" operation, and the request carried an
arbitrary `SourceReference` string that expressed nothing about what the caller was actually accepting.

## 3. The benchmark-aligned flow

```
existing Order
    ↓  ServiceList / SeatAvailability          (upstream, NOT in Ordering)
select ancillary OfferItem(s)
    ↓  OrderQuote                              (upstream, NOT in Ordering)
    ↓  OrderChange — AcceptSelectedQuotedOfferList
Ordering validates and commits the accepted quoted decision
    ↓
updated OrderView
```

Ordering implements only the third step. It does not shop, price, reprice, calculate tax or calculate
commission; it accepts an authoritative quoted decision, validates it, and commits it.

## 4. Public contract

| Before (removed) | After |
|---|---|
| `POST Backoffice/v1/Orders/{orderId}/AddProduct` | `POST Backoffice/v1/Orders/{orderId}/Change` |
| `POST Api/v1/Bookings/{orderId}/AddProduct` | `POST Api/v1/Bookings/{orderId}/Change` |
| `AddProductRequest { SourceReference, ExpectedCommercialVersion }` | `OrderChangeRequest { ExpectedCommercialVersion, AcceptSelectedQuotedOfferList[] { QuotedOfferId, SelectedOfferItemIds[] } }` |
| `OtaAddProductRequest` | (same request type for both channels) |
| `AddProductOutcome` as the public body | `OrderChangeResponse { OperationId, CommercialVersion, Order }` where `Order` is the local `GetOrderDetails` projection |

No public alias was retained: no consumer exists, so the endpoints were deleted rather than deprecated. The
routes stay PascalCase and carry no `RQ`/`RS` message suffixes — the semantics align, the transport stays
REST-native, and no NDC XSD was copied into Ordering.

The caller sends **only** which quoted offer item is accepted, the expected commercial version and the required
`Idempotency-Key` header. Price, tax, commission, product or commercial-terms snapshots, service detail
internals, fulfilment profile and generic attributes JSON are all authoritative quote facts and cannot be sent —
asserted by reflection tests over the public request types.

Both channels call the same `IOrderChangeService.AddServiceAsync`; the controllers adapt identity and transport
only, and exactly one implementation of the interface exists (asserted).

## 5. Semantic port

`IAcceptedProductAdditionPort` became `Domain/Ports/OrderChange/IOrderChangeQuoteProvider`:

```
AcceptedQuotedOfferSelection(OperationKey, OrderId, OperationId, QuotedOfferId, SelectedOfferItemId, SaleCurrencyId)
        ↓
AcceptedAddServiceChange(SourceSystem, QuotedOfferId, SelectedOfferItemId, PricingSource, Product, PricingLines, SourcePricingReference?)
```

Its only responsibility is resolving an accepted quoted Add-Service selection. It is deliberately not a
universal servicing interface — refund, exchange and reprice will each get their own authoritative decision
model. The deterministic test double and the fail-closed `UnconfiguredOrderChangeQuoteProvider` (2863 / 501)
remain the only implementations; no sibling Offer, Pricing or Shopping service was written.

**Expired or rejected quote → the change fails (2862) and nothing mutates.** Ordering never substitutes a
current price, never reprices locally and never accepts a stale amount; the caller must obtain a new quote.
Provenance is kept in distinct fields with distinct meanings: `QuotedOfferId` on the price change set's
`SourceOfferId`, `SelectedOfferItemId` on the order change's `ExternalReference`, `SourceProductReference` on
the product snapshot, and `SourcePricingReference` only when actually supplied.

## 6. Single selected offer item — documented subset

The request shape is the standard list. The implemented atomic mutation creates exactly one `OrderItem` per
commercial change, so more than one selected offer item is **rejected explicitly (2864) before the quote
provider is called** rather than partially executed or silently split into several hidden operations. This is a
current implementation subset of OrderChange / Add Service, not a different business flow; the full deviation
record (benchmark compared, limitation, reason, benefit, compatibility impact) is in the audit.

## 7. Durable identifiers

`ServicingOperationKind.AddProduct` → **`AddService`**, numeric value 9 preserved; it was introduced in P2-E
only and is not production-established. `OrderChangeType.AddProduct = 2` and `PriceChangeReason.AddProduct = 2`
are pre-existing durable values with rows already persisted: the members and numbers are kept, their display
names now read "Add Service", and they are recorded as **internal historical implementation labels, not public
business vocabulary** — neither appears in any public contract. No enum value was reordered and no migration
was needed.

Internal domain members (`Order.AddProduct`, `StageProductAddition`, `AttachProductAddition`, the
`AcceptedAdded*` records) are unchanged: they accurately describe attaching a new commercial item, they are
private to the Domain, and no cosmetic mass rename was performed. Only the boundary types were renamed —
the port, its request, its result (`AcceptedAddServiceChange`) and its argument wrapper.

## 8. Idempotency fingerprint

The intent now fingerprints the actual business request:

```
Operation = "OrderChange" · Subtype = "AddService" · OrderId
QuotedOfferId · SelectedOfferItemIds · ExpectedCommercialVersion
```

Same key with a different quoted offer, a different selected offer item or a different expected version is
rejected by the existing P0 receipt fingerprint (2703) — each asserted by its own test.

## 9. Ancillary vs SSR

A paid seat, prepaid bag, lounge, Wi-Fi, hotel or ground transport is an ordinary ancillary service and is
**not** an SSR. Real SSR semantics must not live permanently inside `GenericServiceDetail.AttributesJson`, so
P2-E.1 records the target concept — a first-class **`SpecialServiceRequest`** with SSR code, status, traveller
and segment/service association, carrier context, quantity and structured or free-text content — and defers it
to its own benchmarked slice. Nothing speculative was persisted, **no SSR code or status was invented**, and no
SSR subsystem was implemented. Which services are genuinely SSR-based stays carrier/product configuration.

The seat sale keeps its benchmark distinction: shopping may originate from SeatAvailability, but order
acceptance is still a quoted selected seat service through OrderChange. There is no `POST /AssignSeat`;
operational DCS seat assignment remains a separate future concern.

## 10. P2-E internals preserved

Nothing in the commercial mutation changed. Still exactly one `OrderChange`, one `OrderItem`, one or more
`OrderServices` with immutable membership links and one committed `PriceChangeSet`; `CommercialVersion` +1,
`FinancialSequence` +1, `ObligationVersion` only on customer-balance movement; exception-atomic staging;
`OrderChange.OperationId` recovery and post-commit replay; `PriceTreatment` evidence rules; no fabricated air
policy on ancillary items; no EMD creation; ETKT document-family scope intact. All P2-E correctness tests were
carried over unchanged in substance and only re-pointed at the new public operation — none was weakened.

## 11. Not implemented

ServiceList, SeatAvailability, OrderQuote, offer/pricing/inventory redesign; the full SSR lifecycle, host
transmission or confirmation workflow; EMD and EMD issuance; refund, refund mask, exchange, reissue,
revalidation; remove service; air itinerary change; JetPay; Ledger. No generic `switch(ChangeType)` executor,
universal servicing command or handler registry was created — only `OrderChange → Add Service` exists, and the
existence of a `/Change` endpoint does not authorise any other change type.
