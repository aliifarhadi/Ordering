# Implementation Instructions - Modern PSS Ordering Domain Design v1

## 1. Authority and first rule

Install this whole pack at `docs/order-domain-design-v1/` in the existing Ordering repository. `MANIFEST.md` is the entry point. For implementation mechanics, the **existing AeroTech Framework and already-approved cross-service contracts are authoritative**. This design pack is authoritative for Ordering business/domain semantics and ownership boundaries.

Read `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` before material coding. A requirement that Ordering needs data does not mean Ordering owns the master data, calculation or lifecycle.

## 2. Existing Framework is the platform

Reuse the current AeroTech implementations/patterns for CQRS/application dispatch, aggregate/entity bases, repositories/UnitOfWork, transactions, outbox/inbox/idempotent messaging, correlation/causation envelopes, ID generation, clock/time handling, locks/concurrency, validation/error/result handling, command/query separation, synchronizers/read projections, configuration, logging/tracing and hosting.

Do not create a parallel framework or alternative infrastructure stack for the redesign. If a **demonstrated generic Framework limitation** blocks a valid Ordering requirement, inspect current usage/sibling services and make the smallest compatible improvement to the existing Framework/pattern with tests and impact analysis. A missing business/cross-service concept is not a Framework gap.

This pack does not dictate enum versus record/value object/class, file layout, exact namespaces, EF configuration style, numeric SQL precision, or a new `Money`/`Currency`/`ExchangeRate` abstraction. Those mechanics follow the current repository/platform.

## 3. No-assumption / confirmation gate

The agent must not invent a business or cross-service semantic to keep coding. If approved code/contracts/docs do not unambiguously establish ownership or representation for a decision that affects financial correctness, public contracts, business lifecycle or an irreversible provider action, stop the **affected** work and create a precise `BLOCKED_DECISION` as defined in `13`. Continue unrelated unblocked work.

This especially applies to monetary/currency/FX/rounding representation, tax/commission/penalty rules, Pricing result semantics, Payment/StoredValue instructions, Core/AirInfo identity/reference semantics, Inventory/DCS/FlightOps provider outcomes, document/notification ownership and authorization context.

Do not ask the owner about normal internal engineering choices that are already clear from the repository/framework.

## 4. Ownership baseline

Ordering owns the durable commercial Order, stable service identity, accepted commercial/pricing history, servicing lineage, Ordering document/fulfillment evidence defined by this pack, and the local read/audit view of those facts.

External/source owners remain external: Pricing/Offer for calculated accepted price/refund/change/penalty results; AirPrice for platform ROE/master calculation where established; AirInfo/BasicInfo for currency/carrier/location reference data where established; Core for customers/offices/actors/legal organization; Inventory/FlightFlow for capacity; DCS for operational delivery observations/control; Payment/JetPay/StoredValue for actual value movement; Ledger for accounting. P0 must confirm the exact current contracts before implementing each boundary.

Ordering stores a reference/snapshot/projection only when commercial history, local decision safety or read performance requires it. It must not duplicate the source lifecycle.

## 5. Servicing semantics are binding

Use `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` as the primary post-sale business specification. Preserve these distinctions:

- pre-ticket cancellation/abandonment;
- document void;
- voluntary cancellation and refund;
- informative reshop/quote versus accepted change;
- revalidation versus exchange/reissue;
- even exchange, add-collect, refund, residual/reusable value and mixed outcomes;
- penalty/waiver treatment;
- no-show operational evidence versus commercial/financial consequences;
- ancillary/EMD servicing;
- involuntary schedule/disruption recovery;
- final semantic ServicingRecord versus customer-facing notice/receipt.

Ordering does not calculate a fare/tax/penalty/refund/commission/FX result that belongs to another owner just because the adapter is missing. Allocation is not refund entitlement. A provider timeout is not success/failure unless its contract proves that.

## 6. Superseded old Ordering assumptions

For this rewrite:

- a global Created/Paying/Paid/Ticketing/Ticketed Order state machine is not canonical business truth;
- internal `CommercialVersion` represents committed commercial mutations, not event count; current external consumers are not a compatibility constraint and will be updated after producer implementation;
- financial/provider operations need durable stable business identity before dispatch and explicit Unknown/reconciliation handling;
- automated tests are release gates; an old instruction that tests were paused does not waive them;
- domain meaning must not be dictated by legacy wire DTO shapes; boundary mapping follows existing AeroTech patterns.

Do not interpret this section as permission to rewrite unrelated Framework conventions.

## 7. Execution order

Implement P0 through P6 according to `04-IMPLEMENTATION-BLUEPRINT.md` and `11-SLICE-READING-MAP.md`. Start with P0 only.

P0 must first produce `docs/order-domain-design-v1/implementation/P0-DISCOVERY-AND-DECISIONS.md` with exact Framework classes/patterns reused, current platform monetary/currency/ID/time representations, ReferenceData ownership/sync paths, available sibling contracts, Framework gaps, and every unresolved cross-service/business question as `BLOCKED_DECISION`.

No P1 business implementation begins until the P0 evidence required by its dependencies is resolved. P0 may still implement unambiguous foundation corrections/tests while other decisions are blocked.

## 8. Tests and evidence

Use the scenario catalogue as acceptance specifications, not proof. Add domain, persistence, concurrency/failure-injection and real adapter contract tests for the applicable slice. Preserve stable operation keys across retries. Prove no duplicate capacity/money/document effect after response loss and Unknown outcomes.

`reference_harness.py` is a semantic fixture/reference checker only; it does not define platform classes, enums, numeric precision or rounding policy and does not replace .NET/SQL/provider tests.

## 9. Portable continuation context

Goal: a practical modern airline PSS Ordering capability in the existing AeroTech solution. Required v1 scope includes real ETKT/EMD, detailed air/ancillary price history, partial servicing, DCS inbound/outbound, disruption/involuntary servicing and group/charter. Avoid paper-only DDD and speculative generic infrastructure. The implementation should be easy to extend because ownership and stable identities are correct, not because every future provider concept is prebuilt.
