# 11 - Implementation Slice Reading Map

## Purpose

This file makes the design pack usable during implementation without requiring every developer to reread the entire specification for every vertical slice.

Rules:

1. `00-EXECUTIVE-DECISIONS.md` and `IMPLEMENTATION-INSTRUCTIONS.md` are mandatory for every slice.
2. Read the shared sections listed below for every slice.
3. Read only the slice-specific sections/scenarios before coding that slice; later slices are optional context unless a referenced invariant crosses the boundary.
4. A referenced scenario is an acceptance specification, not proof that code already passes it.
5. If an implementation decision contradicts a mandatory section, record an ADR/design decision before coding around it.

---

## Shared minimum for all P0-P6

Read:

- `00-EXECUTIVE-DECISIONS.md` - all business/domain decisions, especially OrderItem/OrderService/fare construction boundaries and state ownership.
- `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` - mandatory ownership/reuse/no-assumption rules and `BLOCKED_DECISION` protocol.
- `01-DOMAIN-MODEL.md` - sections 2 (Order root), 5 (OrderItem), 6 (OrderService), 8 (lineage), 17/18 current summary/version semantics, 19 shared/platform semantic requirements, 22 privacy/owner boundary.
- `07-ELIGIBILITY-AND-LIFECYCLES.md` - sections 1, 2 and 5: eligibility policy shape, state-independent guards, durable operation/claim behavior.
- `08-CONTRACTS-AND-DATA-DICTIONARY.md` - sections 1/2 command/idempotency envelope and section 9 event/version envelope.
- `04-IMPLEMENTATION-BLUEPRINT.md` - sections 5 read model/projector, 9/9A integration/version rules, 10 API rules, 12 transaction boundary, 14 slice gates, 15 test strategy.

Do **not** use current external consumers as a reason to preserve old per-event OrderVersion semantics. The producer baseline is this pack; downstream consumers will be changed afterward.

---

# P0 - Platform discovery and unambiguous foundation

## Goal

Prove what the existing AeroTech repository/Framework and sibling contracts already provide before material domain coding. Produce the ownership/reuse/decision map, then implement only foundation work whose semantics are unambiguous. P0 is **not** permission to create a new Money/Currency/FX/time/ID/CQRS/outbox/inbox/locking framework.

## Mandatory reading

- `MANIFEST.md` and `IMPLEMENTATION-INSTRUCTIONS.md`.
- `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` - all.
- `00` - framework/state ownership decisions.
- `01` - root/version semantics and section 19 shared/platform semantics.
- `04` - Framework boundary, transaction/projector/idempotency/version sections and P0 gate.
- `08` - semantic command/idempotency/event/support-record requirements.
- `09` - evidence limits.
- Existing repository `Framework/`, Ordering infrastructure/ReferenceData, and available sibling contracts/docs needed to populate `P0-DISCOVERY-AND-DECISIONS.md`.

## Required P0 artifact

`docs/order-domain-design-v1/implementation/P0-DISCOVERY-AND-DECISIONS.md` as defined in `13`. Any unresolved business/cross-service representation becomes a precise `BLOCKED_DECISION`; unblocked work may continue.

## Acceptance scenarios

S-083..S-107, S-121..S-123, S-129, S-149, S-219..S-220, S-299..S-308 as applicable to discovered infrastructure.

## Not required yet

No P1 sale/issue workflow, no new fare/refund rules engine, and no speculative replacement of shared platform primitives.

---

# P1 - Create, reserve, payment coverage, stock/ETKT issue, GetOrder

## Goal

A production-safe simple sale/issue path with partial/unknown external outcomes and no duplicate money/document/capacity operations.

## Mandatory reading

- Shared minimum.
- `01`: sections 3 buyer/sales context, 4 Journey, 11 FulfillmentReservation, 12 ElectronicTicket, 14 DocumentStock.
- `03`: sections 2 Inventory, 3 Payment evidence/coverage, 4 documents/provider unknown outcome.
- `07`: Create/Reserve/Issue eligibility and document/control guards; pre-ticket withdrawal/release only where existing Inventory/Payment contracts make its semantics unambiguous.
- `08`: command result contracts, reservation/document/application records.
- `04`: P1 persistence + provider intent-before-dispatch rules.

## Acceptance scenarios

S-001..S-004, S-040..S-048, S-083, S-084, S-093, S-094, S-097..S-102, S-112..S-114, S-128, S-131, S-132, S-136, S-137, S-144, S-145, S-158, S-159.

## Not required yet

Complex exchange/refund pricing, DCS reducer internals, disruption optimization, group materialization.

---

# P2 - Fare construction, ancillary catalogue, tax/charge/commission, EMD

## Goal

Correct commercial representation and monetary history across traditional/dynamic fares and the full required ancillary product spectrum without product-specific ledgers.

## Mandatory reading

- Shared minimum.
- `01`: sections 4 Journey topology, 5/6 item/service/type details, 7 FareConstruction, 13 EMD.
- `02`: all sections. This is the primary P2 document.
- `08`: quote/source normalization, typed/registered service schema, EMD purpose rules.
- `10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md`: sections 1, 5 and 6.

## Acceptance scenarios

S-005..S-039, S-046, S-053, S-054, S-086..S-092, S-112, S-118, S-125, S-127, S-133..S-138, S-151, S-154, S-176..S-210, S-217.

## Product-specific implementation rule

Use typed detail tables only for product types with materially different invariants/query shape. Priority/WiFi/SIM/CIP/assistance/PETC/AVIH/etc. use registered versioned detail schemas until real behavior justifies a dedicated table. Do not create an `OrderXService` class/table merely because the catalogue has a new code.

---

# P3 - Complete voluntary servicing and financial/document outcomes

## Goal

Implement production-safe pre-ticket cancellation, void, voluntary cancellation/refund, reshop/change, revalidation, exchange/reissue, no-show disposition, ancillary servicing, split/value transfer and traveler/name correction without conflating commercial, document and payment outcomes.

## Mandatory reading

- Shared minimum.
- `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` - **all; primary P3 business document**.
- `13` - all Decision Gates relevant to Pricing/Payment/issuer/currency/notice ownership.
- `01` - service/document/current-vs-history lineage and ServicingRecord section.
- `02` - pricing changes, allocations, refunds, penalties/waivers, fare-construction coupling.
- `07` - servicing eligibility/finalization/control guards.
- `03` - Pricing/Payment/document integrations and Unknown recovery.
- `08` - servicing semantic contracts/data records.
- `04` - P3 transactions/provider intent/idempotency/projection.

## Acceptance scenarios

S-047..S-059, S-087..S-102, S-112..S-120, S-127..S-130, S-166, S-173, S-188..S-205, S-209..S-210, and **S-221..S-308 except the disruption-only execution details that belong to P5**.

## Exit rule

No P3 flow may calculate a fare/tax/penalty/refund/FX result that belongs to Pricing/AirPrice/Payment or another source owner merely because the source contract is missing. Missing authoritative semantics are `BLOCKED_DECISION`, not local defaults.

---

# P4 - DCS receive/send and delivery state

## Goal

Bidirectional delivery integration with correct identity, batching, ordering, corrections, quantity and document-control semantics.

## Mandatory reading

- Shared minimum.
- `03`: sections 5 and 6 in full.
- `01`: OrderService beneficiaries/coverage and document/coupon identity.
- `08`: delivery observations, aspect versions, legacy correlation, current/historical ownership aliases.
- `10`: section 4 DCS matrix.

## Acceptance scenarios

S-060..S-067, S-104, S-108..S-111, S-124, S-126, S-146, S-156, S-161..S-175, S-191, S-193, S-195.

## Adapter release gate

Real certified DCS mapping/correlation/control ACK behavior is mandatory. A mock that emits `Flown` cannot certify P4.

---

# P5 - FlightOps, disruption and involuntary recovery

## Goal

Operational schedule/disruption truth is consumed without rewriting sold history; high-fan-out events remain resumable and per-Order atomic; recovery invokes ordinary protected servicing.

## Mandatory reading

- Shared minimum.
- `01`: section 4 Journey/Passenger Segment vs operational legs, section 8 lineage.
- `03`: sections 7 and 8 in full.
- `12`: involuntary servicing, planned schedule-change decision window, revalidation/reissue/refund, ancillary-impact and waiver/authority sections.
- `07`: involuntary change/recovery eligibility and durable claim rules.
- `08`: disruption/fan-out contracts.
- `10`: sections 1, 3, 4 and 7.
- `04`: P5 load/concurrency gates.

## Acceptance scenarios

S-068..S-073, S-120, S-127, S-133..S-143, S-147..S-175, S-177, S-178, S-189, S-190, S-193, S-218..S-220, S-271, S-274..S-277, S-283..S-288, S-295..S-298, S-301..S-303, S-308.

## Mandatory runtime gates

- FlightCancelled affecting 1,000 indexed Orders: local fan-out <=30s baseline.
- Normal per-Order transaction/fence <500ms in the production-like test environment.
- No provider I/O or N-Order transaction/fence.
- Concurrency test for FlightOps + DCS + agent servicing proves no lost facet.

These are operational release criteria, not domain constants.

---

# P6 - Group/charter materialization

## Goal

Seat blocks, unnamed/name slots, deadlines, deposit references and idempotent bulk materialization/issuance without one giant Order or repeated successful side effects.

## Mandatory reading

- Shared minimum.
- `01`: GroupBooking aggregate, Group/Order boundary.
- `08`: section 11 group data and capacity rules.
- `07`: group/materialization/issue guard interactions.
- `02`: charter/provider-defined price path; no mandatory FareConstruction.
- `04`: P6 persistence/batch/idempotency gate.

## Acceptance scenarios

S-014, S-074..S-077, S-115, S-116, S-125, S-211..S-217.

## Not required for P6 completion

Cross-carrier settlement automation or NDC wire certification. The model may carry partner references, but unsupported workflows remain explicit adapter gaps.

---

## Final developer shortcut

If the task is narrowly scoped, use this rule:

| Work type | Minimum slice docs |
|---|---|
| Monetary/line/allocation bug | 00 + 02 + P0 section above + referenced scenarios |
| New ancillary product | 00 + 01 section6 + 02 section8 + 10 section5 + its scenarios |
| Fare/exchange/refund | 00 + 01 section7/8 + 02 + 07 + P3 scenarios |
| DCS event | 00 + 03 section5/6 + 08 delivery section + P4 scenarios |
| FlightOps/disruption event | 00 + 01 section4 + 03 section7/8 + P5 scenarios |
| Ticket/EMD/stock | 00 + 01 document sections + 07 document guards + P1/P3 scenarios |
| Group import/charter | 00 + group sections in 01/08 + P6 scenarios |

No developer is required to understand unrelated future slices before implementing a bounded slice, but shared invariants and cross-referenced acceptance scenarios remain binding.
