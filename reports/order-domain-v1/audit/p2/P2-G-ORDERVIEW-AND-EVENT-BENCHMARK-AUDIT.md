# P2-G — OrderView and Event Benchmark Audit (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09

The standing **Airline Flow Benchmark Rule** (P2-E.1) applies: an externally visible flow must correspond to an
industry standard, an established PSS benchmark, or a documented justified modernization. P2-G implements only
the subset the current Ordering service actually requires, REST-native, with no literal NDC XML and no cloned
IATA XSD.

---

## 1. Concept-by-concept

| Concept | Benchmark | AeroTech semantic | Implemented? | Deviation? | Reason | Future owner |
|---|---|---|---|---|---|---|
| **OrderView** | The materialised representation of an Order returned to a requester | `Query.OrderAggregate.View.OrderView`, one strongly typed Ordering-owned model built by the projector and returned by the query | Yes | Naming only — the type is `OrderView`, the transport is REST | Business semantics align; transport does not imitate XML message naming | — |
| **OrderRetrieve** | The request that returns an OrderView | `GET /Api/v1/Bookings/{OrderId}` (OTA) and the existing `GET /Backoffice/v1/Orders/{OrderId}/Details` | Yes | No `OrderRetrieveRQ` / `OrderViewRS` route or class names | REST-native resource retrieval is the platform convention | — |
| **OrderChange response** | A change returns the changed Order | `POST .../{OrderId}/Change` returns `OrderChangeResponse { OperationId, CommercialVersion, OrderView Order }` | Yes | Adds stable operation metadata beside the view | Platform convention; the business result is still the changed Order | — |
| **CommercialVersion** | — | AeroTech commercial concurrency/version: `Create = 1`, `+1` per accepted committed commercial mutation, unchanged by document issuance, reservation observation, funding observation or projection refresh | Yes | **Deliberately not renamed** | It is not the IATA external Order Version and is exposed honestly under its own name | — |
| **IATA OrderVersion** | External order versioning with its own policy flexibility | **Not created.** No `ExternalOrderVersion` / `NdcOrderVersion` field exists | No | Documented deferral | No current consumer requires it; a future NDC adapter may map and maintain it separately | NDC adapter |
| **ProjectionRevision** | — | Read-model materialisation revision plus `UpdatedAt`; technical freshness evidence only | Yes | — | Never used as a commercial concurrency token and never presented as an Order Version | — |
| **DocumentVersion** | — | Per-document lifecycle version on ET and EMD | Yes (P1/P2-F) | — | Third, separate version concept; no generic version framework was created | — |
| **OrderChangeNotif** | `ChangeOperationGroup`: one Order version increase carrying one or more changes | `OrderChange` = one `CommercialVersion` increase, one or more domain events, and one `PriceChangeSet` when pricing changes | Analogous | **Analogous, not identical** | Recorded explicitly in §2 below; no schema conformance is claimed and no `OrderChangeNotifRQ` is published | P2-H / NDC adapter |
| **OrderHistory** | A capability returning the servicing history of an Order | **Not implemented.** `OrderView.Changes` carries current commercial-change audit context only | No | Documented deferral | The domain holds commercial change evidence, but P3 servicing history does not exist yet; exposing a "complete OrderHistory" API would mislead | P3 |
| **OrderPricingChanged** | Change-notification principle: one committed commercial change group and its accepted pricing consequence | Internal integration event, exactly one per committed `PriceChangeSet` | Yes | Internal contract, not an NDC message | Named for the fact it states; explicitly not `OrderChangeNotifRQ` | P2-H consumers |
| **OrderCreated** | — | Existing legacy-compatible creation contract with total summary | Unchanged | — | Full pricing history is the new event's concern, not this one | — |
| **OrderIssued** | — | Existing legacy contract publishing legacy-translated pricing lines through `LegacyPricingLineTranslation` | Unchanged | — | Legacy compatibility; its polarity model was not silently replaced | migration owner |
| **Pricing event payload** | Immutable accepted commercial facts a consumer reasonably needs | Envelope facts plus every `PricingLine` of the set with its allocation evidence | Yes | Focused middle ground | Not only ids (consumers would have to call back), not the whole Order | — |
| **Outbox** | — | Existing `IOutboxWriter` / `OutboxMessages`, written inside the same UnitOfWork as the commercial mutation | Reused | — | No second outbox, no broker-specific code in Domain/Application, no new deduplication table | — |

## 2. Recorded analogy — change group vs commercial mutation

```
IATA:      ChangeOperationGroup  = one Order version increase + one or more changes
AeroTech:  OrderChange           = one CommercialVersion increase
                                 + one or more domain events
                                 + one PriceChangeSet when pricing changes
```

These are **analogous but not identical protocols.** Ordering claims no schema conformance to any NDC message,
and `OrderPricingChanged` is an internal integration contract rather than an airline-facing operation.

## 3. Version and ordinal semantics

All events raised by one commercial mutation share that mutation's **final** `CommercialVersion` and carry
distinct, deterministic `EventOrdinal` values:

| Mutation | Events | CommercialVersion | EventOrdinal |
|---|---|---|---|
| `Order.Create` | `OrderCreated`, `OrderPricingChanged` | 1, 1 | 1, 2 |
| Order Change / Add Service | `OrderProductAdded`, `OrderPricingChanged` | 2, 2 | 1, 2 |

The pricing event is raised only after the version is final, so it never carries the price change set's
`ExpectedCommercialVersion` (which stays 1 for an addition committed at version 2) as its own version. Both are
exposed separately in the view and the event.

## 4. Practicality review of the view surface

Every root section earns its place: totals and facets (commercial, reservation and the two document families),
travellers, journeys and segments, items with their accepted snapshots, services with beneficiaries, coverage,
price treatment, fulfilment profile and typed or generic detail, fare constructions, commercial changes,
committed pricing history, reservations, electronic tickets and miscellaneous documents, time limits and
external references. Nothing is exposed merely because EF persists it: internal technical grouping keys, provider
internals, raw host data and legacy pricing categories are absent.

**Generic services** expose schema identity (`SchemaName`, `SchemaVersion`) only. The P2-D registry does not
classify attributes as public or sensitive, so the safe minimum is shown and the raw `AttributesJson` is never
projected (asserted by test). Building a data-classification framework was out of scope.

**Totals** come from the existing `OrderAmount` cache and `Commission`; the projector computes no new monetary
value, sums no service prices and infers no tax or refund figure. `DerivedCustomerBalanceImpact` on a price
change set and on the event is clearly named as derived; pricing line amounts remain non-negative magnitudes with
explicit `Direction`.

## 5. Schema marker

`OrderView.SchemaVersion` is stamped with `OrderView.CurrentSchemaVersion` (currently `1`) so a future
deserialiser can recognise the payload shape it is reading. It is a projection-payload marker and is unrelated to
`CommercialVersion`. No schema registry or versioning framework was built.

## 6. Not implemented in P2-G

`OrderHistory` API; literal NDC XML or XSD cloning; refund, refund mask, exchange, reissue, revalidation; EMD
refund, exchange or void; ticket exchange; remove service; itinerary change; SSR lifecycle; DCS; disruption;
Ledger integration (Ordering publishes its commercial truth and makes **no** Ledger call); JetPay integration;
offer, pricing or inventory redesign; event sourcing; a new message broker, outbox framework or projection
framework. The projector remains a synchronous command-side local projection — P2-G is not a CQRS
infrastructure redesign.

No historical `OrderPricingChanged` events were back-filled for existing `PriceChangeSet` rows: historical
publication timing cannot be reconstructed safely without an explicit migration requirement, so the new event
applies to mutations from deployment onward.
