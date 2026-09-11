# Modern PSS Ordering Domain Design v1

**Same v1 baseline; reviewed and updated in place on 2026-09-07. No version bump.**

## Purpose and status

This pack is the binding business/domain specification for rebuilding the Ordering capability inside the existing AeroTech `Ordering` repository and framework. It defines domain ownership, invariants, servicing semantics, external evidence requirements, persistence boundaries and acceptance scenarios.

It deliberately does **not** define a second platform framework or invent shared primitives. Code representation and mechanics follow existing AeroTech conventions. Cross-service/shared semantics that cannot be proven from current code/contracts/docs are governed by the mandatory `BLOCKED_DECISION` gate in `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md`.

## Reading order and files

| File | Purpose |
|---|---|
| [00-EXECUTIVE-DECISIONS.md](00-EXECUTIVE-DECISIONS.md) | scope, ownership, final domain decisions and anti-overengineering boundaries |
| [13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md](13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md) | **mandatory before coding**: source-of-truth map, Framework reuse and no-assumption decision gate |
| [01-DOMAIN-MODEL.md](01-DOMAIN-MODEL.md) | Order/Service/fare/document/group domain and stable identity/history boundaries |
| [02-PRICING-AND-SERVICING.md](02-PRICING-AND-SERVICING.md) | immutable commercial pricing semantics, allocation, fare construction and servicing monetary invariants |
| [12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md](12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md) | **full servicing benchmark**: cancel/void/refund/change/revalidation/reissue/involuntary/no-show/EMD/penalty/waiver/residual/notices |
| [07-ELIGIBILITY-AND-LIFECYCLES.md](07-ELIGIBILITY-AND-LIFECYCLES.md) | operation eligibility and safe finalization checkpoints |
| [03-DCS-DISRUPTION-FULFILLMENT.md](03-DCS-DISRUPTION-FULFILLMENT.md) | Inventory/Payment/DCS/FlightOps/Disruption/Ledger/Pricing integration ownership and failure handling |
| [08-CONTRACTS-AND-DATA-DICTIONARY.md](08-CONTRACTS-AND-DATA-DICTIONARY.md) | semantic contract/data requirements; adapters map them to actual platform contracts |
| [04-IMPLEMENTATION-BLUEPRINT.md](04-IMPLEMENTATION-BLUEPRINT.md) | implementation slices, logical persistence, projector/idempotency/failure strategy and release gates |
| [05-SCENARIO-VALIDATION.md](05-SCENARIO-VALIDATION.md) | 308 design-reviewed acceptance cases covering sale, pricing, servicing, flight topology, DCS, ancillary, disruption, group and failure/replay |
| [10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md](10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md) | risk-equivalence matrix for flight, ancillary, pricing and servicing coverage |
| [11-SLICE-READING-MAP.md](11-SLICE-READING-MAP.md) | bounded reading contract for P0-P6 |
| [06-REVIEW-RESOLUTIONS.md](06-REVIEW-RESOLUTIONS.md) | review resolution/evidence register and final benchmark corrections |
| [09-VALIDATION-REPORT.md](09-VALIDATION-REPORT.md) | what was design-reviewed vs mechanically/executably checked; remaining runtime gates |
| [IMPLEMENTATION-INSTRUCTIONS.md](IMPLEMENTATION-INSTRUCTIONS.md) | binding coding-agent instructions to merge with repository agent guidance |
| [AGENT-START-PROMPT.md](AGENT-START-PROMPT.md) | copy/paste first prompt for Codex/Claude, starting at P0 discovery/foundation |
| [reference_harness.py](reference_harness.py) | implementation-neutral semantic reference checks only; not a production type/precision template |

## Normative hierarchy

When implementation guidance conflicts, use this order:

1. confirmed current AeroTech platform/service contracts and Framework behavior for **representation/mechanics**;
2. `13` for ownership and assumption gates;
3. this v1 pack for Ordering business/domain semantics;
4. current repository patterns for implementation mechanics not otherwise specified;
5. older Ordering/V1/V2 docs only as historical evidence where they do not conflict.

A vendor benchmark never overrides an approved AeroTech business decision. A code convenience never overrides a business invariant.

## Product constraints retained

- one home airline/operator deployment initially;
- no legacy-data migration or old-API compatibility requirement for the new Ordering domain baseline;
- DCS inbound/outbound, disruption/involuntary servicing, GroupBooking/charter, real ETKT/EMD, partial servicing and detailed air/ancillary pricing remain v1 scope;
- Payment/JetPay, Inventory/FlightFlow, Pricing/Offer/AirPrice, DCS/FlightOps, Core and Ledger remain external authoritative owners for their respective truths;
- cross-carrier automation, full NDC/ONE Order wire certification and interline settlement are future adapter/integration scope, not reasons to distort the Order core now.

## Deliberate simplicity

The model does not introduce Entitlement, Event Sourcing, a product-specific ledger, a generic workflow DSL, a second Payment aggregate, a second currency/FX subsystem or one service class/table per catalogue code. It keeps stable commercial service identity, optional fare-construction context, immutable accepted price history, separate fulfillment/document evidence and local read projections.

`ServicingRecord` is a redisplay/audit capability built from finalized facts, **not a new business aggregate by default**. Refund/exchange notices and receipts are derived presentation artifacts and must reuse/discover the existing platform owner.

## Implementation rule

The agent may make ordinary code-structure choices from the current repository without asking the product owner. It may **not** decide unconfirmed business/cross-service semantics. In particular, it must not invent monetary/currency/FX/rounding/tax/penalty/payment/reference-data abstractions just because Ordering needs the data.

If an unresolved decision changes domain meaning, a durable external contract, financial correctness or irreversible provider behavior, the affected code is blocked and the agent asks the precise question defined in `13`.

## Release and handoff

Install the whole pack under `docs/order-domain-design-v1/`. Start with `AGENT-START-PROMPT.md`; do not ask an agent to implement P1-P6 before P0 has produced its repository evidence/decision report.

Design completeness is not runtime proof. Production enablement still requires .NET/SQL concurrency/fault tests and real provider contract/capability tests for the implemented slice.
