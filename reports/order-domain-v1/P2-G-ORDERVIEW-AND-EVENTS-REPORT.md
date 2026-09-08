# P2-G — Benchmark-Aligned OrderView, Projection & Pricing Change Events

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09
Baseline: P2-F (`72f4fe2`), **745 passed / 0 failed**. P0 – P2-F frozen. **P2-H not started.**

Evidence: [`audit/p2/P2-G-TEST-RUN.txt`](audit/p2/P2-G-TEST-RUN.txt) ·
Benchmark audit: [`audit/p2/P2-G-ORDERVIEW-AND-EVENT-BENCHMARK-AUDIT.md`](audit/p2/P2-G-ORDERVIEW-AND-EVENT-BENCHMARK-AUDIT.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **451 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **337 passed**, 0 failed (real SQL Server) |
| Total | **788 passed, 0 failed** (baseline 745 → +43, zero regressions) |
| Migration | none — the query contract, projection payload and integration contracts changed, not the schema |

## 2. One strongly typed local Order View

`GetOrderDetailsQuery` returned `OrderDetailsDto(..., object Snapshot)` and deserialised the stored projection
to a `JsonElement`. The public query contract is now the strongly typed `OrderView`:

```
GetOrderDetailsQuery : IRequest<OrderView?>
```

The database still stores the projection as JSON — the established local mechanism — but the projector now
builds the same typed `OrderView` (via `OrderViewBuilder`) that the query handler deserialises, so there is one
schema rather than a projector-side anonymous shape and a reader-side dynamic document. The nested anonymous
types are gone from `OrderProjector`.

The view carries `SchemaVersion` (currently 1) as a payload marker for safe deserialisation. It is not a schema
registry and is unrelated to `CommercialVersion`.

## 3. Retrieval and change share the view

| Surface | Contract |
|---|---|
| `GET /Backoffice/v1/Orders/{orderId}/Details` | `OrderView` (existing internal route kept) |
| `GET /Api/v1/Bookings/{orderId}` | `OrderView` (added — REST-native OrderRetrieve → OrderView) |
| `POST .../{orderId}/Change` (both channels) | `OrderChangeResponse { OperationId, CommercialVersion, OrderView Order }` |

No `OrderRetrieveRQ` / `OrderViewRS` naming, no duplicate Backoffice `/View` alias, no NDC transport cloning,
and no change-specific embedded order shape. Retrieval is entirely local: a test asserts that no quote,
reservation, funding, ticket or EMD provider is touched during a GET.

## 4. Version semantics kept honest

`CommercialVersion` is exposed under its own name and is **not** relabelled as an IATA Order Version; no
`ExternalOrderVersion` was speculatively created. `ProjectionRevision` and `UpdatedAt` remain technical
read-model freshness evidence and never act as a commercial concurrency token. `DocumentVersion` stays a
per-document lifecycle concept. A test proves that issuance advances `ProjectionRevision` while leaving
`CommercialVersion` untouched — document lifecycle and commercial concurrency are demonstrably distinct.

## 5. Complete P2 read surface

The view now redisplays the whole implemented vertical: totals from the existing `OrderAmount` cache (nothing
recomputed in the projector), commercial and per-document-family facets, travellers, journeys and segments,
items with `ProductSnapshot` and `CommercialTermsSnapshot`, services with beneficiaries, coverage,
`PriceTreatment`, current and original item membership, fulfilment profile, typed or generic detail and EMD
issuance evidence, **fare constructions** with their groups, units and components (previously missing entirely),
commercial changes with their operation and affected ids, committed **pricing history** with full pricing lines
and allocation sets, reservations, electronic tickets and miscellaneous documents.

Nothing is inferred while projecting: nullable fare-construction fields stay null, RT versus OW+OW stays
structural, a through component keeps its multi-service and multi-segment membership, and an item-level bundle
price stays one line with no fabricated allocation. Generic services expose schema identity only — the raw
`AttributesJson` never reaches the view (asserted).

## 6. Pricing history uses modern P2-A semantics

Committed price change sets are ordered by `FinancialSequence` and carry their reason, source, quote provenance,
`ExpectedCommercialVersion`, timestamps and a clearly named `DerivedCustomerBalanceImpact`. Pricing lines keep
non-negative magnitudes with explicit `Direction`, distinct `Effect` values, original and sale currencies,
exchange-rate provenance, separate `SourceLineRef` and `OccurrenceKey`, reversal linkage fields reserved for P3,
and their allocation sets. The legacy `OrderPricingLineCategory` model appears nowhere in the new view.

## 7. `OrderPricingChanged`

A new additive internal integration contract, produced from domain truth rather than by comparing database rows:
`Order.RaisePricingChanged` runs at the point where the price change set is committed and `CommercialVersion`,
`FinancialSequence`, `ObligationVersion` and `CustomerTotal` are final for the mutation.

**Exactly one event per committed `PriceChangeSet`** — never one per pricing line, service or tax:

| Flow | Result |
|---|---|
| `Order.Create` | one event, `CommercialVersion = 1`, `FinancialSequence = 1`, all original-sale lines in one envelope |
| Order Change / Add Service | one event, `CommercialVersion = 2`, `FinancialSequence = 2`, product charge + tax + settlement-only commission in one envelope |
| Settlement-only change set | one event, `FinancialSequence` +1, `ObligationVersion` unchanged, `DerivedCustomerBalanceImpact = 0` |

Sibling events from one mutation share the final `CommercialVersion` and take distinct ordinals
(`OrderCreated` 1 / `OrderPricingChanged` 2; `OrderProductAdded` 1 / `OrderPricingChanged` 2). The event never
uses the set's `ExpectedCommercialVersion` as its own version.

The payload is the focused middle ground: envelope facts plus every pricing line with its allocation evidence —
not only ids, and not a serialised Order. Amounts stay magnitudes with explicit direction, commission stays
`SettlementOnly` and is never subtracted from customer balance, repeated tax codes stay distinguishable by
`SourceLineRef` + `OccurrenceKey`, source references are never fabricated, allocations appear only when real, and
no AirPrice or Offer DTO reaches the contract. Nothing passes through `LegacyPricingLineTranslation`.

## 8. Non-pricing operations and idempotency

Reserve, ET issuance, EMD issuance, document recovery, provider confirmation and projection refresh commit no
price change set and therefore emit no pricing event — each asserted. A replayed Order Change writes no second
event and no second outbox row; a rejected change and a failed quote write none at all, leaving no orphan
message. The event is written through the existing `IOutboxWriter` in the same UnitOfWork as the commercial
mutation and the projection, so no committed mutation can lose its pricing fact and no uncommitted one can leak
one. No second outbox, no deduplication table, no broker-specific code in Domain or Application, and **no Ledger
call** — Ordering publishes its commercial truth and stops there.

## 9. Legacy contracts intact

`OrderCreated` is unchanged and still carries its total summary rather than full pricing history.
`OrderIssued` and its `LegacyPricingLineTranslation` polarity adapter are untouched and remain the legacy
compatibility path; asserted by a contract test that also proves the new line model carries
`Direction`/`Effect`/`LineRole` and no `Category`. No historical `OrderPricingChanged` events were back-filled.

## 10. Not implemented

`OrderHistory` API, literal NDC XML; refund, refund mask, exchange, reissue, revalidation; EMD refund, exchange
or void; ticket exchange; remove service; itinerary change; SSR lifecycle; DCS; disruption; Ledger; JetPay;
offer, pricing or inventory redesign; event sourcing; a new broker, outbox framework or projection framework.
The projector stays a synchronous command-side local projection.
