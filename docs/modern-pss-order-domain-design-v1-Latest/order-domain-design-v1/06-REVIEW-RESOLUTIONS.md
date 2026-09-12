# 06 - Technical Review Dispositions and Evidence

**Design remains v1. Review date: 2026-09-07.** This register records how the supplied `AeroTech-Ordering-Technical-Review.md` was evaluated. A finding is not automatically a verified production incident, and an accepted concern does not mean the review's proposed implementation is adopted unchanged.

## 1. Review basis and limits

The six original design documents and manifest, the supplied review and the completed discovery questionnaire were read. Relevant source snippets from the three repositories were already available in this conversation. This review additionally fetched current Ordering `CLAUDE.md`, `CommandDbContext.cs` and provider registration through the connected GitHub account. No repository writes, deployment changes, production database inspection or .NET integration tests were performed.

Source-code observations establish risks in those implementations. They do not establish that those paths are deployed with real cards, that an incident occurred, or that a separate Payment service does not exist. Current provider registration explicitly binds `IPaymentProvider` to `MockPaymentProvider`.

The questionnaire is the product-scope source: single owner airline initially, no SaaS requirement, no legacy-data migration/API compatibility requirement, GroupBooking in v1, and DCS/disruption expressly required by the user. The review is advisory and cannot silently reverse those priorities.

## 2. Critical findings

| ID | Disposition | Applied resolution |
|---|---|---|
| C-1 | Accept risk; qualify incident claim | `04` sections 5 and 12 select one local projector with command/query work on the same SQL connection and explicit transaction. No concurrent inline/event writer pair. A rebuild command repairs views. No claim of observed permanent production divergence. |
| C-2 | Accept durable-intent problem; correct key prescription | `03` section 3 and `04` section 10 require intent commit before dispatch and stable business OperationId/StepId across retries. A persistent record ID is a valid key; regenerating it is the fault. An incrementing retry attempt number MUST NOT generate a new payment key. A FlightFlow HTTP classifier is not by itself evidence about the payment adapter. |
| C-3 | Accept lifecycle guards; refine late-fact behavior | `07` supplies operation/document guards; Payment owner must separate authorization/capture/refund/release. Verified late contradictory money facts are recorded and reconciled, not discarded merely because a local state was terminal. Removal of a local aggregate alone does not prove safety. |
| C-4 | Accept | `03` section 3 and `08` section 6 require uniquely identified confirmed application/refund movements. Mutable summary counters are rebuildable caches, never the only evidence. |
| C-5 | Accept no fabricated capture; refine amount test | Use confirmed monetary evidence in the platform representation or an explicit confirmed guarantee. Partial and excess amounts have explicit treatment. Whole-order exact equality is not a universal issue guard: funded subset and approved agency credit require scoped coverage evidence. See `02` section 5 and `07` IssueEligibility. |

## 3. Domain/design findings

| ID | Disposition | Applied resolution |
|---|---|---|
| D-1 | Accept currency-safety gap; supersede representation prescription | Ordering must preserve authoritative amount/currency/provenance and reject ambiguous cross-currency treatment. `13` now forbids inventing a new Money/Currency/FX stack; concrete representation is discovered from AeroTech contracts/Framework in P0. |
| D-2 | Accept correctness need; supersede local rounding prescription | Rounding/precision comes from the authoritative Pricing/AirPrice/provider/platform contract. Ordering preserves/validates accepted results where possible and does not impose an invented ties-to-even/largest-remainder policy. Ambiguity is a `BLOCKED_DECISION`. |
| D-3 | Accept | Multiple Payment applications/tenders supported externally. Whole-order funding can be sufficient; detailed payment allocation is required only when restrictions/partial fulfillment need it. Price allocation and tender routing remain different concepts. |
| D-4 | Accept capability gap | Explicit cancellation, refund, exchange and revalidation policies, commands, outcomes and price treatment in `02`, `07`, `08`. A declared enum member is not an implemented workflow. |
| D-5 | Accept fault; refine proposed one-line fix | Reversal is explicit `LineRole=Reversal`, not reason-based and not merely nonnull OriginalPricingLineId. Lineage is also used for transfer/adjustment. Cumulative reversal bounds and historical currency preservation are mandatory. |
| D-6 | Accept controlled-stock need; qualify universal rule | Stock reservation, overlap/uniqueness checks and uncertain-number retention are mandatory. Format, check-digit and number-issuing authority come from the actual certified issuer profile. Do not claim every serial range is directly assigned by IATA. |
| D-7 | Accept | Preserve voluntary/involuntary/source reason, waiver authority and raw unmapped code. Never map all reasons to passenger request. |
| D-8 | Accept | `SalesContext`, owning airline, FinancialCustomer, seller, operating/marketing/validating roles are explicit. Snapshot relevant historical facts; do not duplicate Core administration. |
| D-9 | Accept boundary principle | Domain uses domain types; boundary adapters map wire enums and DTOs. Retain platform framework behavior unless a documented change is needed. |
| D-10 | Accept simplification | One projector entry point per affected Order, not fourteen semantically identical upsert APIs. One writer strategy selected; no mandatory seven-table read model. |
| D-11 | Accept operational concern; scoped remediation | Use async persistence/dispatch for this workflow. A sync entry point may be explicitly disabled after caller inspection. Do not patch shared framework blindly or assume a demonstrated deadlock. Failed units are discarded after cleared-event/save failures. |
| D-12 | Accept release gate | Automatic tests of money, eligibility, unknown outcomes, persistence and adapters are required before release. The old paused-tests instruction cannot be treated as evidence of correctness. `05` separates documented cases from executed reference checks. |
| D-13 | Do not elevate to domain redesign | Comment style is not a financial/domain invariant. Preserve useful approved repository style; requirement IDs live in specs/tests. `SpecRef` attributes are optional existing tooling, not a new reflection framework. |

## 4. Findings on the design document

| ID | Disposition | Applied resolution |
|---|---|---|
| R-1 | Accept | `07` is a binding eligibility matrix and lifecycle specification for every business-operation family, not issuance alone. Unknown/stale evidence blocks unsafe finalization. |
| R-2 | Accept semantic currency safety; supersede type mandate | Monetary facts require sufficient authoritative amount/currency/provenance. No particular Money/CurrencyCode class, SQL scale or wire shape is mandated by this pack; P0 maps the existing platform representation. |
| R-3 | Accept | One sign dimension: nonnegative magnitudes plus Debit(+)/Credit(-). Effect identifies balance domain, not sign. Full component/effect matrix, reversal bounds, FX and rounding fixtures are in `02`. |
| R-4 | Reject proposed scope removal; accept staged implementation | DCS, flight disruption and GroupBooking remain in v1 because the user explicitly requires them. Build vertical slices, but do not call a sale-only slice the finished requested product. Interline/NDC protocol rollout and destructive full Order merge are not silently claimed complete. |
| R-5 | Reject equivalence; clarify storage model | Append-only financial rows and delivery evidence do not force event-sourced aggregate reconstruction. Current commercial state is loaded from state tables; histories support audit and projection recovery. This is state-based domain modeling with immutable ledgers/evidence, not a mandatory Order event store. |

## 5. Gap findings

| ID | Disposition | Applied resolution |
|---|---|---|
| G-1 | Accept | Conditional M-0 safety gate precedes real money/document enablement; dual-write and durable-intent protections are explicit before new feature rollout. |
| G-2 | Accept transition need only where applicable; reject unsupported absence claim | Default is a fresh domain build without legacy data. For any real live payment transition, pin ExecutionOwner per intent, drain/reconcile old intents, import verified opening application references and prevent double ownership. Do not delete unresolved legacy money. |
| G-3 | Accept risk-first sequencing | Monetary correctness, idempotency, atomic local views and document numbering precede live enablement; concrete monetary representation comes from P0 platform discovery, not a new Ordering primitive. |
| G-4 | Accept ambiguity; intentionally replace old rule | CommercialVersion increments once per committed business mutation, not once per emitted event. Create=1; no-op/duplicate=unchanged. EventOrdinal, FinancialSequence, ServiceVersion and RowVersion are separately scoped. Old per-event CLAUDE rule is explicitly superseded. |
| G-5 | Accept | Required client Idempotency-Key for mutations including creation before OrderId exists. Receipt hash/conflict/replay/authentication and retention specified in `04`/`08`. |
| G-6 | Accept, with consumer defense | FinancialSequence per Order/PriceChangeSet, producer stream claim, consumer dedup/gap staging and replay baseline. No promise of exactly-once broker delivery or globally ordered independent streams. |
| G-7 | Accept | RebuildOrderReadModel is an authorized, resumable, fenced operational command with dry-run, checkpoints, privacy checks and no business side effects. |
| G-8 | Accept conflict; do not mandate unnecessary cryptographic infrastructure | Separate protected PII payloads from immutable money. Retention/legal-hold decision, payload/search/cache purge, downstream notification and replay suppression required. Key destruction is an option only with suitably granular existing encryption; not a substitute for deleting plaintext copies. |
| G-9 | Resolve from user scope | One owning airline/deployment in v1, trusted OwnerAirlineId in roots/contracts and owner-keyed constraints. Partner carriers are separate roles. Future pooled tenants require an explicit isolation change, not speculative SaaS now. |
| G-10 | Accept | `IMPLEMENTATION-INSTRUCTIONS.md` supplies the exact instruction merge boundary and document installation path. Existing repository CLAUDE/AGENTS have NOT been changed by this document task. No broken references to absent legacy specs are required. |

## 6. Additional domain corrections found during this review

These are design conclusions of this review, not assertions that the supplied reviewer requested them:

- **Fare construction is Order-owned immutable context, not necessarily nested inside one OrderItem.** A source pricing unit can cover services from more than one sold item. Preserve source binding rather than duplicate a coupled PU as independent units.
- **Unchanged service identity survives price-only changes.** Repricing a used outbound portion does not create a second delivered air service. Many-to-many replacement and immutable item membership handle split/reaccommodation.
- **Historical and current ownership are different.** Moving a service during split does not rewrite old pricing, coupon valuation or source-event OrderId. Current membership invariants must not invalidate historical references.
- **Shared hotel/transfer/pooled-baggage services have multiple beneficiaries.** Split cannot duplicate one room/car/allowance between child orders without supplier/pricing partition.
- **EMD-S can document money without a deliverable service.** Fee, deposit and residual-purpose linkage is explicit; issuer profile controls support. See B-007 for one real carrier example, not universal rules.
- **Group capacity is checked per flight block.** Fifty names over two flights need capacity in each block, not the sum of the two. Bulk row receipts prevent duplicate materialization.
- **DCS event identity supports batches and source aspects.** One source message can contain many passengers; dedup cannot discard everyone after the first observation. Boarding, seat and travel outcomes use distinct source version scopes.
- **Projected state can be stored and indexed while remaining semantically derived.** The assertion that every query over a derived lifecycle requires loading the full Order is not a general limitation of the proposed CQRS design.
- **Cancellation is not refund completion; allocation is not refund value.** A retained cancellation penalty, partial credit, historical FX and returned cash must remain independently representable.
- **Removing Payment from Ordering is not itself a safety fix.** The external owner/adapters must pass stable-intent, coverage and duplicate-result tests before irreversible operations are enabled.

## 7. Evidence register

The following public primary sources were checked for the narrow conclusions listed. They are not a compliance certification or a reconstruction of proprietary PSS internals. Versioned IATA model pages are used as identified references, not claimed to be the latest mandatory protocol release.

| ID | Source | Narrow supporting observation |
|---|---|---|
| B-001 | [IATA AIDM 25.2 OrderItem](https://airtechzone.iata.org/aidm_model/25.2/EARoot/EA6/EA1/EA2/EA7/EA10550.htm) | Individually priced commercial item made up of services; external message semantics do not dictate database aggregates. |
| B-002 | [IATA AIDM 25.2 Service](https://airtechzone.iata.org/aidm_model/25.2/EARoot/EA6/EA2/EA2/EA3/EA11503.htm) | Ordered air service granularity supports passenger/segment correlation. Internal non-air stay/vehicle scope needs its own validated mapping. |
| B-003 | [IATA ONE Order](https://www.iata.org/en/programs/airline-distribution/retailing/one-order/) | Order-centric industry direction alongside the transition from legacy records/documents. |
| B-004 | [IATA AIDM 25.1 Order](https://airtechzone.iata.org/aidm_model/25.1/EARoot/EA6/EA1/EA2/EA2/EA11558.htm) | Order supports nonhomogeneous passenger items; differing journeys alone need not mandate a split. |
| B-005 | [IATA AIDM 22.1 Pricing Unit](https://airtechzone.iata.org/aidm_model/22.1/EARoot/EA4/EA1/EA2/EA6384.htm) | Pricing-unit identity is distinct from journey display and delivered-service count. |
| B-006 | [ATPCO Optional Services](https://faremanager.atpco.net/atpapps/fmhelp/mergedProjects/Optional%20Services/what_are_optional_services.htm) | Ancillary service/application categories differ; product kind alone does not determine every pricing and fulfillment rule. |
| B-007 | [Delta EMD-S guidance](https://pro.delta.com/content/agency/jp/en/policy-library/reservations-and-ticketing/electronic-miscellaneous-document--standalone--emd-s-.html) | Concrete issuer examples include monetary EMD-S purposes such as fees/deposits/residuals. Exact issuer rules remain adapter/profile specific. |
| B-008 | [Microsoft EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions) | Multiple contexts need shared connection and transaction for a shared relational transaction. Two sequential SaveChanges calls are not enough. |
| B-009 | [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests) | Stable key and consistent payload identify one operation; provider behavior must be understood rather than inferred from retry count. |
| B-010 | [Stripe low-level error handling](https://docs.stripe.com/error-low-level) | Network/server failures can leave an uncertain outcome; provider-specific retry and reconciliation matter. |
| B-011 | [Microsoft Event Sourcing pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing) | Event sourcing makes event history the basis for state reconstruction; an immutable ledger within a state-based system is not automatically full aggregate event sourcing. |
| B-012 | [GDPR Article 17, official EUR-Lex text](https://eur-lex.europa.eu/eli/reg/2016/679/art_17/oj/eng) | Erasure rights and exceptions require an applicable retention/legal-basis decision, not unconditional destruction of all financial history. |
| B-013 | [IATA Reference Business Architecture](https://www.iata.org/reference-architecture) | Business capability modularity is compatible with one initial deployable; it does not require one aggregate for the whole airline. |
| B-014 | [Travelport Exchange/Refund guide](https://support.travelport.com/webhelp/jsonapis/airv11/content/air11/General/ExchangeRefundGuide.htm) | Servicing involves source-specific quoting and execution; preserving context is not implementing the supplier's pricing engine inside Order. |
| B-015 | [Microsoft CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs) | Write behavior and materialized query representation can differ; query simplicity need not determine commercial state ownership. |

The original pack's high-level SabreMosaic, Amadeus Nevio and Navitaire comparisons remain directional historical context only. No proprietary internal object model, newly verified feature inventory or vendor conformance is claimed by this revision.

### Direct repository evidence checked in this review

| File | Blob SHA | Observation |
|---|---|---|
| `Ordering/CLAUDE.md` | `d94e3595a9a30a5dc67efcc8c75590857ffd6277` | Greenfield declaration, old global state guard, per-event version rule, no client idempotency and paused tests conflict with the revised binding design. |
| `Ordering/Framework/AeroTech.Framework.Infrastructure/Persistence/CommandDbContext.cs` | `c8ac4b8221a9c62bd774483a838f2554cbadc14f` | Domain dispatch before base save supports local outbox; collected events are cleared before completion, so failed mutated contexts must not be reused blindly. |
| `Ordering/src/AeroTech.Ordering.Providers/DependencyInjection.cs` | `363db9e53ef035d80500900a37e44ba61ec6ce9f` | Payment provider registered as MockPaymentProvider; deployment of real payment services is not established. |

The earlier inspected PaymentService, OrderingUnitOfWork, OrderService, PricingLine, V1 GroupBooking and V2 Order/Allocation snippets support the design discussion but were not all re-cloned or re-executed in this review. Findings about them are static code observations, not production-runtime verification.

## 8. Readiness statement

The updated pack is a binding design and implementation specification, not a claim that the repository already implements it. `05` records concrete acceptance cases and separately records executed reference-specification checks. Production release still requires actual .NET persistence/concurrency tests, real adapter contract tests and approved issuer/tax/retention configuration. Those are explicit release gates, not hidden defaults to be improvised by developers.


## Follow-up review findings resolved in this pass

| Finding | Disposition | Binding change |
|---|---|---|
| FUP-1 CommercialVersion consumer break risk | **Waived by product owner for current consumers** | Current external consumers are explicitly not a compatibility constraint; they will be changed after Ordering implementation. The new producer contract binds CommercialVersion/EventOrdinal/FinancialSequence/ObligationVersion directly and P0 has no consumer-inventory gate. |
| FUP-2 FlightCancelled N-order fan-out cost | **Accepted** | Flight event is stored once, then durable async fan-out applies one short atomic transaction/fence per Order. Added 1,000-Order <=30s baseline P5 gate and race test. |
| FUP-3 Tax SettlementOnly foot-gun | **Accepted** | `Tax + SettlementOnly` is forbidden in Ordering v1. Customer-collected tax is CustomerBalance; settlement is separate. Harness has a rejection fixture. |
| FUP-4 Missing validation harness | **Accepted** | `reference_harness.py` is delivered beside the documents and executed from the package; .NET fixture-port gate is explicit. |
| FUP-5 Missing per-slice reading map | **Accepted** | Added `11-SLICE-READING-MAP.md` with mandatory shared sections, slice-specific sections and scenario IDs for P0-P6. |
| FUP-6 Through-flight passenger segment vs physical leg | **Accepted from final stress test** | Corrected `JourneySegment` to represent the sold passenger board-to-off segment; one segment may bind multiple operational legs. Physical legs no longer force extra commercial Services/coupons. |

The additional flight/ancillary coverage gap is closed at design level by scenarios S-131..S-220 and `10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md`. Runtime release still requires the adapter/provider and load gates named in the relevant slice.


## Final servicing/platform-assumption correction pass

This final pass supersedes any earlier review-resolution wording that accidentally turned a business invariant into a new shared implementation primitive. In particular, earlier remedies around `Money`, `CurrencyCode`, exchange-rate snapshots, numeric precision, rounding algorithms or type/enumeration layout are authoritative **only for the business correctness they were trying to protect**, not for their proposed C#/SQL representation. `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` is now binding for those mechanics.

The post-sale domain was re-benchmarked against IATA Offers & Orders servicing/AIDM, ATPCO Category 16/31/33 and Optional Services, Amadeus refund/reissue/revalidation operational flows, Travelport exchange/refund/void and structured fare-rule flows, plus Sabre Offers & Orders / schedule-change evidence. The applied resolution is `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and scenarios S-221..S-308.

Key closure decisions:

- cancel, void, refund, revalidation and exchange/reissue are separate semantic operations/outcomes;
- partially used refund may require source repricing/valuation of the used portion; allocation is never refund entitlement;
- penalties retain source timing/scope/waiver and netted-vs-separately-payable treatment; Ordering does not implement Cat16/31/33;
- add-collect, refund, residual/reusable value and penalty can coexist as distinct accepted outcomes;
- involuntary servicing preserves authority/reason and still uses an explicit source financial/document plan;
- no-show is operational evidence, not an automatic financial/commercial decision;
- ancillary/EMD servicing follows dependency/supplier/issuer evidence;
- a finalized semantic ServicingRecord is redisplay/audit evidence, while Refund/Exchange Notice and receipts are presentation artifacts owned by the platform capability discovered in P0;
- framework/infrastructure is reused and improved in place when a real generic gap exists; an Ordering feature is not permission to create a parallel platform framework.
