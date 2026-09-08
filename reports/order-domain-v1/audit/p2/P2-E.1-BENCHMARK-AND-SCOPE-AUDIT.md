# P2-E.1 — Benchmark and Scope Audit (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08

Every business-facing P2-E concept is classified below. The binding rule applied is the
**Airline Flow Benchmark Rule** (see §5): a public Ordering flow must be traceable to an industry standard or
an established PSS benchmark, or be a documented, justified deviation. A clean internal abstraction is not a
justification.

---

## 1. Business-facing concepts

| Concept | P2-E implementation | Industry / PSS benchmark | Decision | Keep / Rename / Remove / Defer | Reason |
|---|---|---|---|---|---|
| `AddProduct` public operation | `POST Backoffice/v1/Orders/{id}/AddProduct`, `POST Api/v1/Bookings/{id}/AddProduct` | **none** — no airline standard or PSS exposes "AddProduct" as a post-sale operation | invented vocabulary | **Remove** | It named an internal implementation shape (one Item + services) as if it were an airline business operation |
| `OrderChange` | did not exist publicly | Offer/Order post-sale servicing message: the order is changed, the change is described by what was accepted | the correct public operation | **Keep (new)** | `POST /Backoffice/v1/Orders/{OrderId}/Change`, `POST /Api/v1/Bookings/{OrderId}/Change` |
| Add Service | expressed as "add product" | the standard change use case for adding an ancillary service to an existing order | the only change subtype implemented | **Keep (new)** | `IOrderChangeService.AddServiceAsync`; no generic change engine |
| ServiceList | not implemented | upstream shopping capability that lists ancillary services available for an existing order | **not an Ordering responsibility** | **Defer (outside Ordering)** | A future Offer/Shopping service owns it; Ordering never shops |
| SeatAvailability | not implemented | upstream seat-map/shopping capability | **not an Ordering responsibility** | **Defer (outside Ordering)** | Seat shopping originates there; Ordering only accepts the quoted seat |
| OrderQuote | not implemented | upstream capability that prices a selected change and returns a quoted offer | **not an Ordering responsibility** | **Defer (outside Ordering)** | Ordering must never price; it accepts an authoritative quote |
| Selected quoted offer | invented `SourceReference` string | `AcceptSelectedQuotedOfferList` with `QuotedOfferId` + `SelectedOfferItemId` | acceptance of an already priced offer item | **Rename / re-model** | The request now says *which quoted offer item is accepted*, not "some opaque source reference" |
| OrderView | `AddProductOutcome` was the public contract | the business result of a change is the changed order | the public result is the updated order | **Rename / re-model** | The response returns the local `GetOrderDetails` projection plus stable `OperationId` and `CommercialVersion` |
| SSR (Special Service Request) | not modelled; generic services carry a schema + attributes JSON | a first-class airline concept with its own code, status, traveller/segment association and host semantics | **not the same thing as an ancillary** | **Defer (seam recorded, §3)** | Implementing it now would require inventing codes and statuses without a benchmarked source |
| `GenericService` (P2-D) | registered schema + attributes JSON for Priority, WiFi, CIP, SIM card, ExtraSeat, SpecialAssistance | no direct standard equivalent; a bounded Ordering-owned mechanism | acceptable for genuinely attribute-shaped ancillaries | **Keep, bounded** | Explicitly **not** declared the permanent model for SSR; asserted by test |
| Seat sale | added through the P2-E flow | commercially a quoted seat offer item accepted onto the order; operationally distinct from DCS seat assignment | keep the benchmark distinction | **Keep via OrderChange** | No `POST /AssignSeat`; DCS seat assignment stays a separate future concern |
| Baggage sale | added through the P2-E flow | a quoted ancillary offer item | correct as an ancillary service | **Keep via OrderChange** | No SSR code is invented for it |
| EMD requirement | service records `RequiresDocument` + `DocumentKind = ElectronicMiscDocument`; nothing is issued | EMD is the document that fulfils a chargeable ancillary | requirement recorded, issuance deferred | **Defer (P2-F)** | P2-E.1 changes no document behaviour |

## 2. Explicit statement on upstream capabilities

**ServiceList, SeatAvailability and OrderQuote are upstream capabilities and are NOT implemented in Ordering
P2-E.1.** Ordering neither shops, prices, reprices, calculates tax nor calculates commission. It receives an
opaque already-quoted decision through one semantic port and validates and commits it.

The port is `Domain/Ports/OrderChange/IOrderChangeQuoteProvider`:

```
AcceptedQuotedOfferSelection (OperationKey, OrderId, OperationId, QuotedOfferId, SelectedOfferItemId, SaleCurrencyId)
        ↓
AcceptedAddServiceChange (SourceSystem, QuotedOfferId, SelectedOfferItemId, PricingSource, Product, PricingLines, SourcePricingReference?)
```

It is deliberately **not** a universal servicing interface: refund, exchange and reprice will each get their own
explicit authoritative decision model. Two implementations exist — a deterministic test double and a
fail-closed `UnconfiguredOrderChangeQuoteProvider` (2863 / HTTP 501). If the selected quote is no longer usable
the change fails with 2862 and mutates nothing; Ordering never substitutes a current price.

## 3. SSR seam (recorded requirement, not implemented)

`GenericServiceDetail.AttributesJson` must **not** become the permanent home for real SSR semantics. The target
concept is a first-class **`SpecialServiceRequest`** with practical semantics such as:

```
SSR code · status · traveller association · segment / service association
carrier context · quantity where applicable · structured or free-text content
provider / host reference
```

None of this is persisted now, because no current P2-E use case needs it and the status vocabulary must be
benchmarked against real host semantics before it is invented. **Decision: Defer** to its own benchmarked slice.

Two rules apply immediately and are asserted by test:

- an ancillary is **not** automatically an SSR — paid seat, prepaid baggage, lounge, Wi-Fi, hotel and ground
  transport are ordinary ancillary services;
- **no SSR code or status is invented** to make a P2-D service look host-compatible. When an authoritative
  carrier or host flow later supplies one it will be preserved; until then absent is correct.

Whether a given service is genuinely SSR-based (for example WCHR/WCHS/WCHC, PETC, AVIH, UMNR, special
assistance) is a carrier/product configuration decision and is not hardcoded here.

## 4. Durable identifiers reviewed

| Identifier | Introduced | Decision | Reason |
|---|---|---|---|
| `ServicingOperationKind.AddProduct = 9` | P2-E only, not production-established | **renamed to `AddService`**, numeric value 9 preserved | The operation is Order Change / Add Service; only P2-E test data used the old name |
| `OrderChangeType.AddProduct = 2` | pre-existing durable enum, rows already persisted | **member and value kept**; display name changed to "Add Service" | Renaming the member would create migration and compatibility work with no functional benefit. Recorded as an **internal historical implementation label, not public business vocabulary** — it appears in no public contract |
| `PriceChangeReason.AddProduct = 2` | pre-existing durable enum | same treatment | same reason |
| `Order.AddProduct`, `StageProductAddition`, `AttachProductAddition`, `AcceptedAdded*` | P2-E internals | **Keep** | They accurately describe attaching a new commercial item and are private to the Domain; they appear in no REST route, OTA contract, integration event or business documentation. No cosmetic mass rename was performed |

## 5. Standing decision — Airline Flow Benchmark Rule

Recorded durably in [`../../P2-IMPLEMENTATION-REPORT.md`](../../P2-IMPLEMENTATION-REPORT.md) and repeated here:

```
Ordering public business flows and vocabulary must be traceable
to an industry standard or established PSS benchmark.

Internal implementation abstractions do not justify creation
of new airline business operations.

Any intentional deviation requires:
- benchmark compared,
- limitation identified,
- reason for deviation,
- practical benefit,
- compatibility impact.
```

## 6. Documented deviation — single selected offer item

| Field | Value |
|---|---|
| **Benchmark compared** | `AcceptSelectedQuotedOfferList` is a list; a standard order change may accept several offer items atomically |
| **Limitation identified** | The implemented atomic mutation creates exactly one `OrderItem` per commercial change |
| **Reason for deviation** | Accepting several items would either fabricate multi-item support or split one standard request into several hidden operations, breaking the one-operation/one-`OrderChange` invariant |
| **Practical benefit** | The request shape stays standard and forward-compatible; the current subset is explicit and fails closed (2864) instead of partially executing |
| **Compatibility impact** | None outward: the list is already the contract. A later phase can support atomic multi-item acceptance without changing the public shape |

This is a **current implementation subset of OrderChange / Add Service, not a different business flow.**

## 7. Not implemented in P2-E.1

ServiceList, SeatAvailability, OrderQuote or any offer/pricing redesign; the full SSR lifecycle, SSR host
transmission or SSR confirmation workflow; EMD and EMD issuance; refund, refund mask, exchange, reissue,
revalidation; remove service; air itinerary change; JetPay; Ledger; inventory redesign. No generic
`switch(ChangeType)` executor, universal servicing command or handler registry was created — only
`OrderChange → Add Service` exists.
