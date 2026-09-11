# First Coding-Agent Prompt - Ordering v1 / P0

Copy the block below as the first instruction to Codex or Claude Code.

```text
You are implementing the reviewed AeroTech Modern PSS Ordering Domain Design v1 in the EXISTING Ordering repository.

Work on P0 only. Do not start P1 sale/issue/refund code in this session.

AUTHORITATIVE INPUTS
1. Existing AeroTech repository + Framework + already-approved sibling-service contracts for implementation mechanics and shared platform representations.
2. docs/order-domain-design-v1/13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md for ownership/reuse/no-assumption rules.
3. The rest of docs/order-domain-design-v1/ for Ordering business/domain semantics.

READ FIRST, IN THIS ORDER
- MANIFEST.md
- IMPLEMENTATION-INSTRUCTIONS.md
- 13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md
- 00-EXECUTIVE-DECISIONS.md
- P0 section of 11-SLICE-READING-MAP.md
- only the P0-relevant sections of 01-DOMAIN-MODEL.md, 04-IMPLEMENTATION-BLUEPRINT.md, 08-CONTRACTS-AND-DATA-DICTIONARY.md and 09-VALIDATION-REPORT.md

ABSOLUTE FRAMEWORK RULE
The platform already has a Framework. Before writing replacement infrastructure, inspect and reuse the existing implementation for CQRS/application dispatch, AggregateRoot/Entity patterns, repositories/UnitOfWork, transactions, outbox/inbox/idempotent messaging, message/correlation conventions, ID generation, clock/time handling, distributed locking/concurrency, validation/error/result handling, command/query databases, synchronizers/read projections, configuration, logging/tracing and hosting.

Do NOT create a second framework, event bus, outbox/inbox, UnitOfWork/repository stack, ID generator, clock, lock framework, workflow/event-sourcing platform, reference-data subsystem or financial primitive library.

If a demonstrated generic Framework limitation blocks a valid Ordering requirement, inspect sibling usage and propose/implement the smallest compatible improvement to the EXISTING Framework/pattern with tests and impact analysis. Do not build a local parallel substitute.

NO-ASSUMPTION RULE
Do not invent business or cross-service semantics. In particular, do not create/standardize a Money/Currency/ExchangeRate/rounding model, tax/commission/penalty policy, Customer/Office/Carrier identity model, payment/value lifecycle, provider status interpretation, document/notification subsystem or security context merely because Ordering needs that information.

First inspect the actual current implementation/contracts. When a material semantic or representation is still ambiguous, STOP ONLY THE AFFECTED WORK and record a BLOCKED_DECISION using the exact format in document 13. Ask the product owner one precise question. Continue unrelated unblocked P0 work.

Do not ask the owner about normal coding choices such as enum vs record/value object, private helper layout, EF configuration file organization, DI registration style or test folder structure. Follow the existing repository conventions for these.

P0 DISCOVERY - REQUIRED BEFORE MATERIAL DOMAIN CODING
Inspect at minimum:
- repository solution/project structure and root agent instructions;
- Framework/ and the exact primitives currently used by Ordering;
- Ordering Domain/Application/Persistence/Query/Consumers/Providers/ReferenceData/RestApi/ServiceHost/Synchronizer patterns;
- current currency/reference-data integration;
- current message contracts and provider adapters relevant to Pricing/Offer, Inventory/FlightFlow, Payment/StoredValue, DCS, FlightOps, Core/AirInfo and Ledger that are actually accessible;
- existing transaction/outbox/read-projection behavior and known failure boundaries.

Create this file before material implementation:
docs/order-domain-design-v1/implementation/P0-DISCOVERY-AND-DECISIONS.md

It must contain:
- exact files/classes/patterns to REUSE;
- exact existing pieces that conflict with the reviewed domain and need replacement/refinement;
- current platform monetary/currency/ID/time/reference-data representations found, WITHOUT inventing a preferred alternative;
- service-ownership and contract map for every external fact needed by P0/P1;
- demonstrated generic Framework gaps, if any, with smallest compatible improvement;
- all unresolved business/cross-service issues as BLOCKED_DECISION;
- P0 work safe to implement immediately;
- tests needed to prove the changes.

P0 IMPLEMENTATION SCOPE AFTER DISCOVERY
Implement only unambiguous foundation work required by the reviewed design, reusing the existing Framework: commercial/event version-purpose corrections, durable command/idempotency/operation semantics, local transaction/outbox/read-projection correctness, concurrency/failure behavior and required tests, only where the current contracts are sufficiently known. Exact implementation shape must follow the repository.

Do not add P1 Order sale/issue behavior yet.
Do not design HTTP endpoints for later slices yet.
Do not create compatibility machinery for current external consumers: the user has explicitly said those consumers will be changed after the new Ordering producer is implemented.
Do not edit sibling repositories as a side effect of this task.

EXIT EVIDENCE
- P0-DISCOVERY-AND-DECISIONS.md is complete and evidence-backed.
- No unresolved material assumption is hidden in code/defaults/TODOs.
- No duplicate platform/framework subsystem was introduced.
- All unblocked P0 changes compile and their domain/persistence/concurrency/failure tests pass.
- Any blocked item is stated precisely with the code intentionally not written.
- Provide a concise completion report listing changed files, reused Framework primitives, tests executed, and remaining BLOCKED_DECISION IDs.
```
