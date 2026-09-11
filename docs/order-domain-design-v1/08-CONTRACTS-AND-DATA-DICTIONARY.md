# 08 - Contracts and Data Dictionary

This document closes implementation ambiguities in the v1 domain specification. The names describe the required internal contracts, not a claim that an external service already implements these exact messages. Map existing platform contracts at adapters. Required business data must not be invented by an implementation agent.

## 1. Common semantic contract requirements

This data dictionary specifies information that must survive a boundary. It does **not** prescribe a new shared primitive/type system. Adapters use the actual AeroTech framework and owning-service contracts; unresolved semantic conflicts use `13`.

| Concern | Binding semantic requirement |
|---|---|
| Aggregate/entity identity | use current AeroTech identity generation/transport conventions; a display/business reference is not silently reused as database identity |
| Monetary value | carry the amount/value plus currency identity and provenance required by the owning source so the accepted commercial fact is unambiguous; reuse the current platform representation |
| Currency / conversion | currency identity comes from the established platform reference source; applied conversion evidence is retained only as supplied/required by the authoritative source; Ordering owns no currency/ROE master |
| Time | use existing platform timestamp/date/time conventions while preserving local flight date/time-zone context required for correct flight correlation; do not infer from server timezone |
| Scope | explicit current service/item/traveler/document scope; historical monetary/lineage facts retain scope-at-creation rather than inheriting mutable current ownership |
| Provenance | source system/reference/version plus accepted/observed time and policy/profile/rule reference when supplied and needed for audit/servicing |
| Authority | trusted deployment/business/security context from existing platform enforcement; user-supplied owner/permission flags are never authoritative |
| Evidence | stable owner/entity/operation identity, source version/time, result/status and validity/authority fields required by that owner contract; freshness alone is not a payment guarantee or document-control acknowledgment |

Lifecycle/status/component names in this pack are semantic labels. Their concrete code representation follows the current repository and public contract conventions; the design does not require a particular enum/value-object implementation.


## 2. Command receipt and result

Every external mutation has an Idempotency-Key. Existing-order commands also specify ExpectedCommercialVersion; group/document commands carry their respective expected version. Authorization is checked on every call, including replay.

```text
CommandEnvelope
  IdempotencyKey, OperationName
  OwnerAirlineId [trusted], CallerScope [trusted], Actor
  OrderId?, ExpectedCommercialVersion?
  CorrelationId?, Body

OperationResult
  OperationId, CommandReceiptId
  State: Completed | Pending | Unknown | Rejected | NeedsReconciliation
  OrderId?, ResultingCommercialVersion?
  TargetResults[]: TargetId, Outcome, ReasonCodes[], ExternalReference?
  ResourceReferences[], CommittedChangeIds[]
```

Check an existing receipt BEFORE enforcing a new expected-version comparison: a replay of a successfully completed request carries its original version and returns the original result. A request with the same key and a different normalized business body is a conflict. A deliberately new action requires a new key.

`RequestHash` covers business command name, owner/caller scope, normalized target sets, explicit amounts/currencies, quote ID/version, requested action and expected version. Sorting IDs is permitted only for unordered sets; itinerary order remains significant. Exclude tracing IDs and transport timestamps. Keep the normalized request or protected payload reference for comparison/recovery, not only a hash.

## 3. Business command semantics

All operations use the eligibility/finalization rules in `07` and the benchmarked servicing flows in `12`. Names in this table are semantic use-case labels; controllers/commands/DTOs follow the existing AeroTech framework and route conventions.

| Operation family | Required business input | Committed/unresolved result |
|---|---|---|
| Create from accepted Offer/direct sale | accepted source decision/version; selected items; traveler/contact data; FinancialCustomer; sale context | new Order commercial version and accepted services/pricing; no fabricated reservation/payment |
| Add item/ancillary | Order/version; accepted product/price decision; beneficiary/dependency mapping | new commercial obligations + immutable price history |
| Reserve | explicit eligible service scope + provider profile/context | per-service/coupling confirmed/rejected/pending/unknown result |
| Request payment/coverage | accepted payable requirement, monetary value using existing platform contract, purpose/scope and tender/value reference | external owner intent/coverage reference; never fabricated Captured/Paid |
| Issue documents | eligible service or monetary EMD purpose; issuer profile; accepted issue-time value view; funding/control evidence | per-document/coupon confirmed or unresolved result |
| Pre-ticket cancel | explicit service/item scope; reason; accepted cancellation treatment; release dependencies | commercial cancellation + release outcome; no fabricated ticket refund |
| Void document | document/coupon scope; provider/issuer eligibility/control; accepted reversal treatment | confirmed void or unresolved provider result; cash/value completion separate |
| Voluntary cancel/refund | accepted refund decision/version; target scope; original document/value/payment refs; disposition | one commercial credit/cancellation treatment + separately tracked payment/value return |
| Accept voluntary change | accepted change/reshop decision/version; old/new/preserved scope; dependency/document/value plans | replacement lineage + exact accepted price change + per-step outcomes |
| Revalidate | accepted change + exact existing document/coupon scope + confirmed issuer capability | existing document rebound or unresolved/rejected; no replacement ticket |
| Exchange/reissue | accepted change decision; old coupon/document refs; successor plan; accepted monetary treatment | confirmed old/new document lineage + commercial change or unresolved result |
| Involuntary recovery | disruption/recovery instruction/version; accepted option; authority/waiver/value treatment | correlated involuntary OrderChange + recovery outcome |
| No-show disposition | authoritative no-show evidence plus explicit servicing decision if commercial consequence requested | approved cancellation/penalty/forfeit/reuse action only; no automatic consequence |
| Split | traveler subset; accepted value partition; provider divide + payment/value transfer plans | stable child Order + ownership/lineage + balanced transfer evidence |
| Traveler/name correction | authorized corrected fields and provider/issuer/DCS/Pricing treatment where needed | corrected protected snapshot + correlated document/service outcomes |
| Time-limit change/expiry | limit identity/current version + requested/evaluated time + external evidence where required | updated/expired/blocked outcome according to provider truth |
| Close | closing reason/disposition + expected version | closed only after active/unresolved obligations have explicit disposition |
| Manual commercial adjustment | explicit source-approved/authorized monetary treatment, reason/evidence/scope | immutable PriceChangeSet; no arbitrary balance setter |
| Group operations | customer/contract, per-flight blocks, deadlines/deposit requirements, stable name/row identities | per-block/per-row result + materialized Order references without duplicate capacity/value |

Internal implementation may decompose these use cases into existing application/operation steps. Do not expose unrestricted “write price line”, “mark paid”, “mark refunded” or “set document status” APIs that bypass the authoritative workflow/evidence.


## 4. Accepted quote and change plan

The logical structure below states information that must remain correlated; it is not a required DTO/class shape. Existing Pricing/Offer contracts are mapped rather than replaced. If a required field meaning cannot be mapped safely, use `BLOCKED_DECISION`.

```text
AcceptedQuote
  QuoteId, QuoteVersion, SourceSystem, SourceReference
  ValidUntil, AcceptedAt, SaleCurrency, ExpectedCommercialVersion?
  RequestedScope[], PricingScope[], PreservedServiceIds[]
  Items[]: SourceOfferItemId, ProductSnapshot, TermsSnapshot, ServiceDefinitions[]
  FareConstructions[]?                  # immutable source construction, cross-item allowed
  PriceLines[]: SourceLineRef, LineRole, ComponentType, Effect, Direction,
               OriginalValue, SaleValue, Basis, SourceAppliedConversionProvenance?  # semantic content only; map owner contract, do not create ROE subsystem, TaxDetails?, CalculationSnapshot?
  AllocationSets[]?                    # one PricingLine per set/purpose/version
  OldToNewServiceLinks[], DependentServiceDispositions[]
  FinancialTreatment: FullReplacement | ComponentDelta | NoPriceChange
  ExpectedCustomerTotal, ExpectedCustomerDelta
  RequiredFunding, RefundDisposition?, DocumentPlan?, SupplierPlan?
```

RequestedScope and PricingScope are not necessarily equal. Changing one return segment may reprice a whole RT pricing unit; this does not authorize moving another passenger to a new flight. Every new or retained price line has a declared treatment. FullReplacement cannot also post the quoted delta. NoPriceChange creates no artificial sale/reversal, but an operational/document change can still occur.

For initial sale, each accepted component is persisted once and source quantity semantics are explicit. For a grouped ADT quote, line amounts are EXTENDED totals; Quantity does not cause a second multiplication. Each construction-to-service link identifies the traveler and segment. One PU spanning two OrderItems is stored once at Order level; items reference it.

A revised price context supersedes the previous immutable context. An unchanged outbound service may retain its ServiceId when it participates in a new price context. The new item membership links identify retained services. A new flight obligation receives a new service ID and a many-to-many predecessor/successor link as needed. A one-flight-to-two-flight reaccommodation is not forced into a single PredecessorServiceId field.

`TermsSnapshot` must retain a resolvable immutable rule/version or protected source payload, not just a mutable URL and a prose Refundable flag. Supplier-opaque pricing remains valid; automated exchange/refund is blocked unless that supplier or an authorized pricing policy provides a defensible quote. Order does not reverse-engineer a fare engine.

## 5. Execution records: reuse, do not duplicate workflow frameworks

| Record | Required key/data | Constraints |
|---|---|---|
| CommandReceipt | ReceiptId; OwnerAirlineId; CallerScope; OperationName; IdempotencyKey; RequestHash; PayloadRef; allocated resource/operation IDs; Status; ResultRef | Unique owner/caller/operation/key. Pending and unknown requests retained for recovery; no generic short cache eviction. |
| ServicingOperation | OperationId; ReceiptId; primary OrderId; Kind; State; ExpectedCommercialVersion; protected PlanRef; ClaimGeneration; timestamps | One business operation, possibly several existing FulfillmentTasks. A purely local command does not need a redundant external-step workflow. |
| OperationOrderClaim | OperationId; OrderId; Generation; IsBlocking; RecoveryLeaseUntil | Unique filtered OrderId for IsBlocking=true. Split claims both orders in sorted order; unresolved effects keep claim blocking after lease expiry. |
| FulfillmentTask / operation step | StepId; OperationId; Kind; target IDs; NormalizedRequestHash; ProviderIdempotencyKey; Outcome; next-reconcile time | Persist before dispatch. StepId/key and economic payload survive every transport attempt. Step outcome includes NotDispatched, Pending, Confirmed, NotExecuted, Unknown. |
| ProviderInteraction / attempt | AttemptId; StepId; attempt number; started/ended time; transport code; protected request/response refs | Diagnostic attempt identity is never the identity of a new purchase/refund. Redact sensitive payloads. |
| ReconciliationWorkItem | WorkItemId; operation/source references; reason; status; next check; resolution evidence | Lightweight operational work, not a new universal domain aggregate or generic workflow engine. |
| CrossOrderTransfer | TransferGroupId; SourceOrderId; TargetOrderId; OperationId; frozen line map; funding-transfer ref; status | Balanced pair; each original value moved once. No independent retry can create another child or another transfer. |

An adapter step plan must state its preconditions, irreversible boundary, exact expected provider evidence, query-by-key/reference method, compensation and per-target partial result behavior. Missing support blocks that capability. Do not use generic HTTP 500/429 classification as evidence of business nonexecution.

For a local issuer, document and commercial finalization can commit in one local transaction. For an external issuer, preserve the pending plan and actual confirmed document even when later local completion fails. Reconciliation resumes the same plan; it does not delete external truth or guess success.

## 6. Financial and servicing data dictionary

The records below describe **semantic content**. Concrete field types, monetary primitives, precision and wire representation must reuse current platform contracts and `13` decision gates.

| Record/concept | Required semantic content | Authoritative meaning |
|---|---|---|
| PriceChangeSet | Order/change identity, financial sequence, reason, source decision/quote/version, commit time | one immutable accepted commercial monetary operation |
| PricingLine | parent change set, distinguishable commercial component, customer/settlement effect, economic polarity, original/sale monetary values as supplied, pricing basis/scope and provenance | one charged/credited/settlement/informational commercial occurrence; implementation must use one unambiguous sign convention |
| Reversal lineage | explicit original-line reference, opposing economic effect/polarity and partial/full outstanding-value evidence | bounded reversal against an existing accepted value; not every lineage link is a reversal |
| PricingAllocationSet | parent line, purpose/version/source/method/completeness | one value-attribution view, never a second charge and never refund authority |
| PricingAllocation | parent set plus historical item/service/traveler/segment scope and attributed value/provenance | attribution of parent value; not a fare/refund rule |
| Fare-construction binding | construction/item/service/fare-component scope as returned by Pricing source | preserves repricing/rule coupling without forcing 1:1 with Service |
| Document price link | document/coupon + accepted pricing/value references frozen at issue | audit/servicing link; not recomputed from later Order totals |
| Payment/value application fact | external owner, stable movement/application identity, Order/operation, confirmed monetary value, source version and lineage | received confirmed external economic fact, applied locally once |
| Payment/value application projection | current application/coverage/refund/chargeback/transfer visibility derived from confirmed owner facts | eligibility/support projection; not payment ownership |
| Coverage evidence | owner/guarantee identity, allowed scope, available/consumed value, validity/version and issue authority supplied by owner | authority to proceed with irreversible fulfillment, not fabricated cash |
| Servicing decision | quote/decision identity/version/expiry, requested vs affected scope, preserved/replacement services, accepted financial treatment, document/supplier/value plan | side-effect-free authoritative plan accepted for execution |
| Servicing record | finalized operation/change, affected services/items, original/successor documents, accepted price-change refs, penalty/waiver refs, confirmed payment/value refs, provider/actor/time evidence | immutable redisplay/audit view; no recalculation of current rules |
| Customer notice/receipt reference | servicing/document record reference + existing renderer/storage/delivery artifact ref when such a platform capability exists | presentation artifact only; not Order/payment/accounting truth |

### Financial invariants

- Customer-collected tax remains part of customer commercial value. Settlement/accounting ownership cannot remove it from customer payable.
- Fare/tax/surcharge/fee/penalty/discount/commission remain distinguishable when the source distinguishes them.
- Pricing allocation is not refundable value.
- A partially used refund may require an authoritative reprice/valuation of used travel; Ordering does not infer it from allocation.
- Penalty may be netted from credit or collected separately according to source result.
- Residual/reusable value is not treated as an exact guaranteed current balance until an authoritative owner produces it.
- Actual GL recognition remains with Ledger; actual payment/value execution remains with Payment/StoredValue.
- Do not add a second currency, FX, accounting journal or monetary primitive library to Ordering from this specification.


## 7. Service and historical reference dictionary

| Record | Required invariant |
|---|---|
| OrderService | One current OrderId and OrderItemId, explicit typed details, ServiceVersion and validated beneficiaries |
| ServiceBeneficiary | Unique service/traveler/role; primary traveler must be a beneficiary; AirTransport has exactly one |
| ServiceCoverage | Ordered segment set OR appropriate location/time/stay coverage. AirTransport has exactly one current air segment. |
| Typed service detail | Exactly one registered detail shape matches ServiceType. Core air/seat/baggage/stay invariants in `01` section6.4.1 cannot hide in unvalidated JSON. |
| ServiceDependency | Defined association kind, valid target and disposition on change; no arbitrary rules graph |
| OrderItemServiceLink | Immutable item/service association with sold scope and ChangeId; retained after reparenting |
| OrderServiceLineage | FromServiceId, ToServiceId, ChangeId, relation; supports one-to-many/many-to-one replacement; acyclic |
| OrderItemPredecessor | Explicit prior/current item relation; a partition is not a second sale |
| OrderRelatedReference | Link type and owner/order identities; no money or passenger mutation |

Split moves a traveler's CURRENT membership, including their exclusively associated used services, while preserving service identity and immutable issue/sale/observation scope. Shared journey snapshots may be cloned with new local segment IDs; immutable old fare/document links keep the old historical IDs and valid source reference. Current correlation uses the stable service's current ownership, not the OrderId supplied on an old DCS message. Provider reservation divide must yield verified source/child binding references before completed split; preserve prior membership evidence and create/update the current per-order reservation bindings accordingly. If a required provider cannot divide its reservation, expose Pending/Unsupported instead of inventing a second confirmation. Local document association is administrative current servicing ownership, not a rewrite of original issue facts.

Historical refs may point to a service now owned by a different Order; this is intentional, not a cross-order current membership violation. Never cascade-delete historical pricing, traveler aliases, issued documents or lineage when a current membership moves. Authorized PII erasure affects protected personal values, not these structural identities.

## 7A. Reservation coupling and current booking evidence

`FulfillmentReservationService.ObservedStatus` includes `Waitlisted` in addition to Pending/Confirmed/Rejected/Released/Expired/Unknown. `ReservationCouplingGroup` carries `Independent | MarriedSegments | ProviderAtomicSet` plus exact service IDs and provider group reference. A confirmed member of a married/atomic group is not independently issue-eligible while another required member is Waitlisted/Rejected/Unknown.

Current provider RBD/cabin may be retained on the reservation link as observed evidence. It never overwrites the sold AirTransport snapshot or fare construction. Airport standby is a DCS aspect, not a reservation status.

## 8. Document-specific minimum data

`ElectronicTicket` includes issuer/validating-carrier profile, current Order association, issued-at Order/Traveler snapshot references, original/exchange chain, document currency and frozen document totals. Coupon includes stable service link, sold/priced segment reference, financial status, control status/holder and DocumentVersion. Flight association may be revalidated only under an explicit certified issuer operation; retain prior association evidence.

`EmdCouponPurpose` distinguishes Service, Fee, Deposit and ResidualValue. Service-purpose coupons link to a service; Fee links to a PricingLine/Change; Deposit/ResidualValue links to the external payment/stored-value obligation. EMD-A additionally carries the associated ticket COUPON identity and association history. No fake service is created solely to attach a cancellation fee. The issuer profile determines which purposes and associations it supports; unsupported issuance fails eligibility.

`DocumentStockAllocation` contains StockId, reserved number, OperationId/StepId, DocumentRoleKey, issuer namespace, status Reserved/Issued/Retired and confirmed document reference. DocumentRoleKey uniquely identifies type + traveler/purpose + issuance group + ordinal within the frozen operation plan. A repeated step/document role retrieves its existing number. Ranges are checked for overlap under issuer-scoped serialization, not just a unique RangeStart. A unique number prevents duplicates but does not alone prevent overlapping stock ranges. Format/check-digit requirements come from the certified issuer profile; no universal numbering rule is invented.

## 9. Message envelopes and ordering

```text
IntegrationEnvelope
  EventId, SchemaVersion, EventType, SourceSystem, OwnerAirlineId
  StreamType, StreamId, StreamSequence
  CommercialVersion?, EventOrdinal?, FinancialSequence?
  CorrelationId?, CausationId?, OperationId?
  OccurredAt, Payload
```

The emitted OrderPricingChanged stream has one envelope per committed PriceChangeSet with consecutive FinancialSequence for that Order. Unrelated DCS or payment updates do not create holes in this financial stream. Ledger stores duplicate identity and stream position, stages a gap and requests replay; it never books a reversal against a nonexistent original solely because RabbitMQ delivered it first. Publisher serialization is helpful but cannot replace consumer protection.

An initial subscription or historical replay has an explicit baseline/checkpoint; a consumer must not assume a first observed sequence of 57 means sequences 1-56 never existed. An operation may emit several nonfinancial events at one CommercialVersion; EventOrdinal/event identity distinguishes them without increasing the commercial version again.

**Replacement-contract rule:** this design does not preserve the old external `OrderVersion/Version` semantics. New Ordering contracts use explicit `CommercialVersion`, `EventOrdinal`, `FinancialSequence` and `ObligationVersion` fields. `ObligationVersion` is the payment/payable staleness contract; `FinancialSequence` is the pricing stream order; neither is inferred from `CommercialVersion`. Existing consumers are updated after the new producer contract is implemented.

Outbound delivery publication uses its OWN DeliveryPublicationVersion because new confirmed documents, control or funding evidence can affect readiness without changing commercial version. Acknowledgment of reception is not acknowledgment of coupon control release or actual service delivery.

## 10. DCS and disruption payloads

```text
DeliveryObservationInput
  SourceSystem, SourceEventId, ObservationKey
  ServiceId?; legacy correlation fields if absent
  SourceEntityId, Aspect, SourceEpoch?, SourceSequence?, SupersedesObservationId?
  Milestone/Outcome, OccurredAt, ReceivedAt
  Flight/SegmentRef?, Document/CouponRef?, Value/Quantity?
  ProtectedPayloadRef?, MappingProfileVersion
```

A batched message can contain many passengers/aspects. Inbox receipt is per consumer/message; semantic observation uniqueness is source/message/ObservationKey. If the source lacks a stable event key, the adapter uses a documented source-specific identity and retains the dedup evidence; it must not collapse two distinct identical-looking actions merely because their textual payloads match.

`ServiceDeliveryState` stores per-aspect authority/version, current delivery summary, operational check-in/boarding/travel outcome, current seat and last applied observation references. Prefer source sequence within the SAME authority/epoch/aspect. If no reliable sequence exists, occurrence time and allowed transitions help, but ambiguous terminal contradictions go to reconciliation rather than arrival-order overwrite. An offload can follow boarding; a later correction can correct an earlier NoShow. A flight departure event alone does not prove a particular passenger flew. `DeniedBoarding` maps to provider failure/non-delivery (`FailedToDeliver`), never to passenger `NoShow/NotClaimed`. `StandbyListed/StandbyCleared` are delivery/check-in aspects and do not rewrite commercial or reservation truth. Through check-in may carry several service/aspect members in one message; each member has its own observation key/version.

Unresolved correlation creates a quarantined observation/work item. Do not match solely on a passenger name. Return exact no-match/multiple-match reasons. After split, stable service/coupon aliases resolve late input without transferring financial history back to the old Order.

`DisruptionInstruction` contains canonical DisruptionId, ImpactVersion, InstructionId, affected service IDs, recovery choice, reason/waiver evidence and replacement references. `RecoveryInstructionOutcome` carries that instruction ID, OperationId, per-target applied/pending/rejected result and committed change references. Only confirmed completion marks local servicing resolved. A cancelled flight blocks issue/readiness from flight evidence even before an asynchronous disruption-impact message arrives.

## 11. Group/charter materialization contract

`GroupSeatBlock` identifies ONE flight/capacity product, reserved quantity, released quantity and provider block reference. A slot that travels on two flights consumes one unit from EACH block. Enforce `allocated(block) <= confirmed(block) - released(block)` separately. Summing capacity across flights is forbidden.

`MaterializeGroupNames` receives BatchId and stable ClientRowId per named passenger/order grouping. The row receipt contains its target slots, RowRevision, validated traveler data reference, allocated OrderId(s), pricing/contract quote, coverage transfer and completion status. Business row identity is `(GroupBookingId, BatchId, ClientRowId)`. Correcting a rejected row that has no external side effect uses a new RowRevision and idempotency key linked to the same row. A completed row is changed only by ordinary servicing; an unknown row must be reconciled before correction. Revision does not authorize a second materialization of the same completed slot. Rows complete independently with explicit results. Retry of a batch reuses completed row IDs; it does not issue another ticket or debit another deposit. No arbitrary 50/99 passenger platform ceiling is assumed: use configured admission limits and documented provider constraints; large batches are chunked, not loaded as one giant Order transaction.

Group deposit is an external funding application. Transfer/reserve its approved amount to materialized orders once; never copy the full deposit to every child. Contract/customer, capacity owner and selling agency roles remain distinct. Group creation and ordinary bulk import are separate intents; a 50-name CSV/API request does not inherently require one 50-person commercial Order.

## 12. Privacy, projection and extension boundaries

`PersonalDataPayload` has PayloadId, DataSubjectRef, purpose/category, protected payload/key reference, retained-until/review trigger, hold state and deletion marker. The deployment supplies retention rules; no legal retention duration is guessed. Immutable events contain minimal permitted payloads and references, with explicit broker/raw-log retention and downstream purge contracts. Rebuild reads current authorized PII state and deletion markers, not obsolete copies from financial event history.

`RebuildOrderReadModel` takes OrderId/range, dry-run flag and authorized audit reason. It uses the same per-order projector fence and source-state mapper as normal commits, stages/replaces views atomically and records a checkpoint. It emits no business charges, refunds, issue commands or delivery notifications.

New product type: add typed/versioned detail schema, scope/dependency validation, fulfillment profile and scenarios. New supplier: add adapter capability/result mapping. New payment tender: Payment owner implementation and existing coverage mapping. New fare method: source construction snapshot or opaque context; do not add another monetary ledger. Future multi-airline pooled SaaS/interline interoperability needs explicit isolation/protocol work; this model reduces rewrites but does not claim it makes that work free.
