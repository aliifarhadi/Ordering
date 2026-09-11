# 00 - Executive Decisions

## 1. Objective

Design the Order capability for a modern airline PSS with enough fidelity for airline retailing, servicing, delivery and accounting integration, while avoiding a model that is theoretically elegant but operationally expensive.

The design intentionally distinguishes:

- **commercial truth** - what the customer bought and the commercial terms that apply;
- **pricing construction** - how the accepted price was built;
- **monetary history** - charges, credits, taxes, fees, penalties, discounts, commission and their allocation;
- **reservation/fulfilment truth** - supplier/inventory confirmation and documents;
- **delivery truth** - DCS/delivery observations;
- **operational impact** - flight disruption and reaccommodation context;
- **payment truth** - owned by Payment;
- **accounting truth** - owned by Ledger.

No single mutable Order status is allowed to represent all of these dimensions. Persisted, indexed summaries ARE allowed: semantic derivation is not the same as computing every property at query time.

---

## 2. Industry benchmark conclusions and evidence limits

### IATA

The following are benchmark interpretations, not a claim of IATA certification. Source definitions inform this internal model; they do not prescribe aggregate boundaries. Exact wire-message versions and partner certification remain adapter release gates. See the dated source register in `06-REVIEW-RESOLUTIONS.md`.

The design follows these practical conclusions from IATA Offers & Orders / ONE Order direction:

1. **Order is the primary commercial record.** Legacy PNR/e-ticket/EMD remain compatibility and fulfilment artifacts during transition, not the conceptual foundation of the commercial model.
2. **OrderItem is an individually priced item made up of one or more Services.**
3. **At Order time, air Service granularity must be capable of representing one passenger on one flight segment.**
4. Delivery and accounting interact with Orders but remain distinct capabilities.
5. The industry transition is modular; Product, Offer, Order, Delivery and Finance are not expected to collapse into one object or one synchronous transaction.

### ATPCO

The design follows these pricing conclusions:

1. Product and price must remain separable.
2. Existing fare construction remains relevant during the transition to dynamic offers because servicing, audit, refunds and partner interoperability still need construction context when it exists.
3. Ancillary application can be per segment, direction/bound, journey, item/piece, weight, ticket/document, booking/order or provider-defined.
4. Dynamic pricing must not force every accepted air price to look like a legacy filed fare.

### SabreMosaic / Amadeus Nevio / Navitaire Stratos

The prior design used public vendor material as a directional capability benchmark, not as evidence of proprietary internal domain models or successful production tests. The detailed corrections in this review rely on the primary sources listed in `06-REVIEW-RESOLUTIONS.md`. The practical lesson for AeroTech is **module separation without mandatory service proliferation**: several capabilities may initially live in one Ordering microservice and one database, provided state ownership is explicit.

---

## 2.1 Final servicing benchmark correction

A dedicated final pass compared voluntary/involuntary servicing and document/payment effects against IATA servicing/AIDM, ATPCO Category 16/31/33 and Optional Services, Amadeus refund/reissue/revalidation workflows, Travelport exchange/refund/void/involuntary workflows, and Sabre Offers & Orders/schedule-change/ticket-control evidence. The resulting canonical flows are binding in `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md`.

Key conclusion: an Order change, price/refund decision, document operation and payment/value movement are correlated but not interchangeable. The model therefore does not use one generic Cancel state to stand for pre-ticket withdrawal, ticket void, voluntary refund, revalidation or reissue. Penalties, waivers, fare/tax differences, residual/reusable value and ancillary dispositions are source-approved financial outcomes, not locally invented fare rules.

## 3. Final core model

```text
Order
|
+-- Traveler
+-- Contact
+-- Journey
|   +-- JourneySegment
|
+-- OrderItem                         # individually priced commercial item
|   +-- ProductSnapshot
|   +-- CommercialTermsSnapshot
|   +-- OrderService[]                # stable service identity, typed details/coverage
|   +-- FareConstructionRefs[]        # references, not duplicated construction ownership
|
+-- AirFareConstruction[]?            # Order-owned source context; may cover several items
|   +-- PricingGroup[]
|       +-- PricingUnit[]
|           +-- FareComponent[]
|
+-- OrderChange[]                     # business-change audit/lineage
+-- PriceChangeSet[]                  # immutable monetary operation grouping
|   +-- PricingLine[]
|
+-- PricingAllocationSet[]
|   +-- PricingAllocation[]
|
+-- TimeLimit[]
+-- ExternalReference[]
+-- OrderLineage
```

Separate aggregates/modules in the same microservice:

```text
FulfillmentReservation
ElectronicTicket -> TicketCoupon[]
ElectronicMiscDocument -> EmdCoupon[]
DocumentStock
GroupBooking

DeliveryObservation          # append-only observation, not an aggregate needing rich behavior
ServiceDeliveryState         # current projection
SegmentOperationalState      # current flight/schedule projection
DisruptionImpact             # local projection of externally owned disruption context
PaymentApplicationProjection # local payment visibility, Payment remains canonical
```

Technical/process mechanism retained separately from domain truth:

```text
ServicingOperation / OperationOrderClaim   # minimal durable application intent/fence
FulfillmentTask / ProviderInteraction       # reuse current execution machinery
CommandReceipt
OutboxMessage
InboxMessage
```

---

## 4. Key design decisions

### D-001 - No separate Entitlement in v1

`OrderService` carries the commercial service granularity needed for the current product. An additional Entitlement layer is rejected unless a future scenario proves that a stable commercial right must exist independently of both OrderItem and Ordered Service.

This intentionally avoids:

```text
OrderItem -> Entitlement -> FulfillmentUnit
```

when:

```text
OrderItem -> OrderService
```

already solves the required use cases.

### D-002 - OrderItem follows the sold pricing boundary, not passenger or segment count

The number of OrderItems is not derived from passengers, segments or service types.

Examples:

- one round-trip fare may be one OrderItem containing four air Services for two passengers and two segments;
- two separately priced one-way offers may become two OrderItems;
- a bundle may be one OrderItem containing air, baggage, seat and priority Services;
- a zero-charge wheelchair can be an OrderItem/Service even if it was not a paid OfferItem.

### D-003 - OrderService is typed, but pricing is generic

Operationally important service types require specific domain data:

- AirTransport
- Seat
- Baggage
- Meal
- Lounge
- Hotel
- GroundTransport
- Priority
- WiFi
- Insurance
- Generic/ThirdParty

Do not create a separate pricing implementation per service type. All monetary facts use the same `PricingLine` and `PricingAllocation` model.

### D-004 - Prefer composition over EF inheritance/TPT for service details

Persist a common `OrderService` row and type-specific one-to-one detail rows for important types. A generic JSON extension may be used for low-frequency third-party types.

This avoids a mandatory EF inheritance hierarchy while preserving type-specific invariants. It does not eliminate joins: normal commands load only relevant typed details; search uses local indexed projections.

### D-005 - Air fare construction is optional and separate from monetary lines

When a Pricing/Offer provider supplies fare-construction structure, snapshot it as:

```text
AirFareConstruction
  PricingGroup
    PricingUnit
      FareComponent
```

This is necessary to distinguish true round-trip pricing from one-way + one-way and to support correct repricing/refund/no-show semantics.

It is **not required** for charter, opaque dynamic/provider-defined pricing or non-air products.

### D-006 - PricingLine is an immutable commercial money fact

Never mutate historical commercial amounts. Reprice, refund, cancellation, exchange, void and correction append lines with lineage to original lines.

### D-007 - PricingAllocation is attribution, not refund entitlement

An allocation answers "how much value is attributed to this traveler/service/segment for a declared purpose?" It does not imply that the same amount is refundable.

Refund is a new pricing decision.

### D-008 - No global workflow Order state machine

Reject the primary lifecycle:

```text
Created -> Confirmed -> Paying -> Paid -> Ticketing -> Ticketed
```

Payment, reservation, document and delivery states have their own owners. Order exposes a persisted, deterministically recomputed commercial summary, not a writable workflow status. EVERY command has explicit eligibility guards, a scoped decision result and allowed state transitions in `07-ELIGIBILITY-AND-LIFECYCLES.md`. Removing the old workflow machine never removes guards.

### D-009 - Payment aggregate is not owned by Ordering

Payment Service owns authorization/capture/refund/chargeback/provider outcomes. Ordering consumes payment application facts/projections and uses explicit coverage/guarantee evidence to determine whether fulfilment is allowed. Moving the aggregate out does not, by itself, solve unsafe external execution: durable intent, stable operation idempotency, confirmed amount and reconciliation remain mandatory.

### D-010 - Inventory is canonical for flight capacity

Ordering keeps a `FulfillmentReservation` binding to supplier/inventory references and the last observed result, including unknown/unconfirmed outcomes. It does not become the inventory source of truth.

### D-011 - Ticket/EMD remain separate fulfilment aggregates

During the legacy transition, ETKT and EMD are explicit aggregates with their own lifecycle. They link to stable OrderService IDs. Order commercial identity does not depend on a document number.

### D-012 - Document numbers come from DocumentStock

Random document-number generation is rejected. Use controlled sequential stock/ranges with atomic allocation and audit.

### D-013 - DCS observations do not mutate commercial truth directly

DCS input is recorded as append-only `DeliveryObservation` and updates `ServiceDeliveryState`. Commercial consequences such as no-show cancellation are applied only by a separate commercial policy/change operation.

### D-014 - Sold schedule and operational schedule are different

The schedule accepted at sale is retained in the Order snapshot. Flight operations events update `SegmentOperationalState`. Reaccommodation to a different flight creates a commercial change/new segment/service lineage; a simple schedule update does not rewrite historical sale data.

### D-015 - Disruption is first-class integration, not Order-owned flight operations

Ordering stores local `DisruptionImpact` projections and references them from involuntary Order changes. A dedicated Flight Operations/Disruption capability remains canonical.

### D-016 - Stable globally unique service identity

Use platform-generated globally unique IDs for `OrderService`. Split should preserve a service ID when the same service is transferred to another Order. If the commercial product itself changes, create a successor service and record lineage.

### D-017 - Split may require commercial partitioning

If one OrderItem spans services that are divided across two Orders, create successor OrderItems for the partitions. Existing services may retain identity and be re-parented. Pricing value transfer must be explicit; if no defensible allocation exists, the split requires a Pricing result rather than inventing one.

### D-018 - Query simplicity is a design requirement

Clients do not fan out to Payment, DCS, Inventory, Documents and Disruption for a normal `GetOrder`. A denormalized Order details read model composes those facts locally.

### D-019 - Replacement version semantics are explicit and purpose-scoped

The new domain `CommercialVersion` advances once per committed commercial mutation. This pack defines the replacement Ordering contract baseline; existing external consumers are intentionally outside the implementation constraint and will be updated after the new Ordering model is implemented. Do not preserve the old per-event `OrderVersion` semantics inside the new domain merely for compatibility. Public replacement contracts use the explicit names `CommercialVersion`, `EventOrdinal`, `FinancialSequence` and payment-scoped `ObligationVersion` so each sequence has one purpose.

New contracts expose distinct concepts instead of overloading one number:

```text
CommercialVersion      # resulting commercial revision
EventOrdinal           # event position within one commercial mutation when needed
FinancialSequence      # contiguous Order pricing stream
ObligationVersion      # payable-state/staleness version supplied to JetPay
```

No consumer-inventory gate is part of P0. The new Ordering implementation must publish only the new version semantics defined by this pack; downstream services are coordinated after the producer contract is stable.

### D-020 - Flight operational fan-out is asynchronous across Orders, atomic within one Order

A `FlightCancelled` or other high-fan-out FlightOps event is persisted/deduplicated once, then a durable fan-out job resolves affected Order/Service IDs from local indexes. It must NOT hold one SQL transaction, one distributed lock, or one projection fence across all affected Orders. Each affected Order is updated in its own short transaction/fence using the same local projector.

This preserves immediate consistency for each completed Order update while preventing a flight-wide lock convoy. P5 has a production-like capacity release gate; the baseline reference workload is 1,000 affected Orders processed within 30 seconds, with no per-Order transaction held for provider I/O and no normal per-Order transaction exceeding 500 ms. This is an engineering release criterion, not a domain constant.

### D-021 - Customer-collected tax cannot be `SettlementOnly`

A Tax line that is part of the customer's accepted payable price is always `Effect=CustomerBalance`. How the airline, agency, government or partner later settles that tax is a separate settlement/accounting fact. Ordering v1 rejects `ComponentType=Tax, Effect=SettlementOnly`; supplier-only taxes not charged to the customer are `Informational` or remain outside Ordering according to the source contract.

### D-022 - Reference validation harness is a delivered artifact

`reference_harness.py` is bundled beside the specifications. It uses only Python standard-library types/`decimal`, contains named deterministic fixtures derived from the binding examples and invariants, and exits non-zero on failure. It is a specification oracle, not AeroTech production code. Equivalent .NET tests must port the relevant fixtures before the corresponding slice is released.

### D-023 - Every implementation slice has a bounded reading contract

`11-SLICE-READING-MAP.md` defines required sections and scenario IDs for P0-P6. Developers do not need to read unrelated disruption/group material to implement a payment or pricing slice, but shared invariants and cross-cutting contracts remain mandatory.

### D-024 - Flight/ancillary coverage uses risk-equivalence classes, not an infinite Cartesian product

The final catalogue covers all identified high-risk equivalence classes across topology, carrier/provider, reservation state, DCS outcome, ancillary family, pricing application, servicing action and failure/replay. This does not claim every airline/provider-specific code combination is knowable in advance. Unknown provider semantics are preserved as unsupported/unmapped evidence and must fail safely until a profile/adapter is certified. Coverage is indexed in `10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md`.

---

## 5. What is adopted from the three repository generations

### Current Ordering - keep

- immutable PricingLine concept and reversal lineage;
- allocation lineage;
- accepted historical monetary/conversion provenance support, using the platform/source representation;
- independent traffic-document concept;
- unknown provider outcome handling;
- Offer snapshots;
- passenger/segment air service granularity;
- FulfillmentTask/ProviderInteraction as operational execution mechanisms;
- outbox/inbox/idempotency patterns.

### Current Ordering - replace/refine

- global Order workflow state machine;
- duplicating payment truth on Order;
- duplicating delivery/document/financial state inside `OrderService`;
- random ticket-number generator;
- split by cloning every identity;
- PricingLine scope that mixes pricing basis with payment/document references.

### V2 - keep the ideas, simplify implementation

- commercial Order as primary truth;
- coarse commercial lifecycle with persisted, indexed derived summaries;
- explicit item/service lineage;
- stable identity during split where semantically valid;
- separate delivery/consumption observations;
- DocumentStock;
- allocation purpose/version/source concepts;
- strong snapshots for product, price, terms and sales context.

### V2 - do not import in v1

- separate Entitlement layer;
- every observation as a rich aggregate;
- a large speculative lock-compatibility engine; v1 nevertheless requires one durable exclusive servicing claim per Order and explicit document-control checks;
- model complexity that requires several domain objects for a simple sale.

### V1 - keep

- GroupBooking as a first-class aggregate;
- seat block/name slot/name deadline pattern;
- derived status semantics without full-aggregate query-time recomputation;
- currency-safe accepted monetary semantics and historical provenance (reuse the platform representation rather than introducing a duplicate primitive);
- document-number range discipline;
- explicit operational workflow concept as inspiration for technical FulfillmentTask/process managers.

### V1 - avoid

- letting Order own inventory, payment and delivery truth directly;
- generic DeliveryRecord as a replacement for explicit ETKT/EMD semantics.

---

## 5.1 Scope and deployment decision

The questionnaire is a source of business intent, not a list of infallible design answers. The following user decisions control this baseline:

- One operating airline/business deployment now; no pooled multi-tenant SaaS and no legacy data migration/API compatibility requirement.
- DCS INBOUND AND OUTBOUND, flight disruption/involuntary servicing, and GroupBooking/50-name charter materialization remain in the v1 domain and release acceptance scope. They are staged implementation slices, not silently deferred to a new version.
- Codeshare, interline protocol automation, full NDC/ONE Order wire certification and interline settlement are future integration scope. Store carrier/issuer/supplier/partner identifiers now; reject unsupported external actions explicitly.
- Payment, Inventory/FlightFlow, Pricing/Offer and Ledger are external owners. Their production capability is not inferred from contract names or from a MockPaymentProvider in this repository.
- Use the existing framework and project layout. A documentation update does not authorize changing sibling services, production databases or connected repositories.

## 5.2 Additional binding decisions

The earlier draft over-specified shared financial primitives. The table below supersedes those implementation-specific instructions. Semantic labels used elsewhere in the pack are not required C# type/enum names.

| ID | Decision | Rationale / specification |
|---|---|---|
| D-025 | Monetary correctness is binding; monetary **representation is platform-owned** | preserve amount/currency/provenance and historical accepted values, but reuse the current AeroTech representation; do not create a new Money/Currency/FX library from this document |
| D-026 | One unambiguous polarity/effect convention is required, but code shape follows the existing platform | a component must not be double-signed or disappear from customer payable through an accounting classification; `02` defines semantics, not enum implementation |
| D-027 | Named, testable eligibility behavior exists for every business operation | `07`; implementation may organize policies according to current framework conventions |
| D-028 | One durable unresolved servicing claim per Order is the v1 safety baseline | correctness before lock optimization; DCS/FlightOps observations are not blocked by the claim |
| D-029 | One local read-model writer; source state + outbox + local projection remain atomic where one database owns them | `04`; avoid competing write paths |
| D-030 | Persist external intent and stable business operation identity before irreversible dispatch | retry/reconciliation must not create a second payment/reservation/document action |
| D-031 | `CommercialVersion` advances once per committed commercial mutation | event position/payment obligation/financial sequence are distinct concepts; implementation representation follows existing contracts |
| D-032 | Immutable commercial/financial facts do not require perpetual retention of PII | protected personal-data lifecycle stays separable |
| D-033 | Scenario review is not production test evidence | `05`/`09` distinguish design coverage from runtime tests |
| D-034 | Fee-only EMD-S may document monetary purpose without inventing a deliverable Service | provider/issuer capability controls support |
| D-035 | Fare construction is immutable accepted pricing context and may span sold items | source grouping is preserved; no itinerary inference |
| D-036 | Stable service identity is separate from current Order/item/segment ownership | split/reaccommodation/history retain lineage |
| D-037 | Cancel, Void, Refund, Revalidation and Exchange/Reissue are distinct servicing semantics | `12`; they may be coordinated by one operation but their commercial/document/payment outcomes are separate |
| D-038 | Finalized refund/exchange servicing must be redisplayable without recalculation | use an immutable ServicingRecord/projection; do not introduce a new aggregate unless real invariants require it |
| D-039 | Customer notices/receipts are derived artifacts, not Order truth | discover/reuse the existing document/notification capability; do not build rendering inside Ordering by assumption |
| D-040 | Cross-service/shared ambiguities are explicit `BLOCKED_DECISION`s | `13`; an agent cannot invent ownership, monetary/FX/tax/penalty semantics or irreversible provider behavior |
| D-041 | Existing AeroTech Framework is the implementation platform | reuse/extend the existing framework; no parallel CQRS/outbox/inbox/UoW/id/security infrastructure |

## 6. Explicit non-goals

- no event-sourced Order aggregate: current relational state is loaded directly; append-only monetary records and delivery evidence support audit and rebuilding, not mandatory event-stream rehydration;
- no generic graph-based product engine in Order;
- no reproduction of IATA XML/XSD message hierarchy as domain classes;
- no ATPCO fare-rule engine inside Order;
- no synchronous distributed transaction with Inventory, Payment, DCS or Ledger;
- no new microservice for every bounded module in v1;
- no forced fare construction for opaque/dynamic/charter prices;
- no read-time fan-out for ordinary Order retrieval;
- no cross-aggregate foreign-key maze across external services.

---

## 7. Reference signals used in the design

Public benchmark sources include:

- IATA ONE Order / Fulfilment with Orders
- IATA Modern Airline Retailing / Offers & Orders transition material
- IATA AIDM OrderItem, Service and Pricing Unit definitions
- ATPCO Product Catalog / dynamic-offer material
- ATPCO Optional Services / baggage application material
- SabreMosaic public Offer / Order / Settlement / Delivery and Disruption architecture
- Amadeus Nevio public Offer Management / Order Management / Delivery Management product descriptions
- Navitaire Stratos public modern retailing architecture

Repository evidence reviewed from:

- `aliifarhadi/Ordering`
- `aliifarhadi/Ordering.V1`
- `aliifarhadi/Ordering.V2`
