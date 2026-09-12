# 13 - Platform Ownership, Framework Reuse and Decision Gates

**Status:** binding implementation guardrail.  
**Purpose:** prevent coding agents from inventing cross-service concepts, duplicate primitives, hidden business rules or parallel infrastructure while implementing Ordering v1.

This document intentionally separates **what the Ordering domain means** from **how the existing AeroTech platform represents it**.

---

## 1. Absolute rule: need does not imply ownership

A field or fact needed by Ordering does not imply Ordering owns its definition, lifecycle, calculation engine or master data.

Examples:

- Ordering needs currency on accepted monetary facts; that does not make Ordering the currency master.
- Ordering may need the rate/converted result used in an accepted quote; that does not make Ordering the ROE owner.
- Ordering needs Customer/Agency/Office references; it does not own those business masters.
- Ordering needs payment/refund evidence; it does not own PSP/payment lifecycle.
- Ordering needs flight/capacity state; it does not own Inventory/FlightOps truth.
- Ordering needs tax/penalty values on an accepted change; it does not own fare/tax/penalty calculation rules.

For every cross-context fact, implementation must choose one of four treatments **based on existing platform evidence**, not agent preference:

```text
REFERENCE   -> retain owner identity/reference only
SNAPSHOT    -> retain transaction-time data supplied by owner for historical truth
PROJECTION  -> maintain local read/eligibility copy from owner facts
OWN         -> lifecycle/invariants genuinely belong to Ordering
```

If the correct treatment is not provable from current code/contracts/approved docs, the agent does not choose. Use the Decision Gate in section 5.

---

## 2. Confirmed AeroTech ownership map

This map uses current AeroTech repository/docs already reviewed. If a later approved service contract supersedes it, update this map before implementing against the new contract.

| Concept/capability | Authoritative owner / source | Ordering treatment |
|---|---|---|
| Customer, TravelAgency, Organization/Individual customer relationship | Core | reference + sale-time snapshot only where business/audit needs it |
| Airline/agency offices, business actors, legal/organizational context | Core | reference/snapshot needed for sales/servicing audit; do not administer them |
| Carrier/airline reference identity | BasicInfo/AirInfo | reference/snapshot |
| Currency reference metadata | AirInfo/BasicInfo; current Ordering already synchronizes currency read data from AirInfo | reuse existing local reference-data pattern; do not create currency master |
| Rate-of-exchange tables/source policy | AirPrice in current AeroTech architecture | retain accepted/source-applied snapshot/reference only when provided/required; do not create ROE tables/engine in Ordering |
| Initial/servicing fare, tax, surcharge, discount, penalty/reprice/refund calculation | Pricing/Offer/AirPrice capability according to current platform contract | accept source result + provenance; no local general fare/tax/penalty engine |
| Flight availability/capacity/reservation | Inventory/FlightFlow/availability owner | local evidence/projection + stable operation refs; no independent capacity truth |
| Flight schedule/operational event | FlightOps/FlightFlow/AirInfo according to platform flow | sold snapshot + current operational projection; no master schedule editor in Ordering |
| Check-in/boarding/flown/no-show/ancillary consumption | DCS/delivery owner | append observation/projection; no invented DCS truth |
| Payment authorization/capture/refund/FOP/provider outcome | Payment/JetPay | confirmed application/coverage/value-movement references/projection; no Payment aggregate/provider engine |
| Wallet/travel-bank/stored value | StoredValue owner | reference to confirmed value disposition; no balance ownership |
| Accounting/GL recognition and settlement accounting | LedgerFlow/Ledger | publish/retain commercial facts and acknowledgments; no GL in Ordering |
| Authentication | Identity | consume platform identity/security context |
| Permission/operation authorization | Aegis/authorization enforcement | use existing platform enforcement; Ordering adds resource/domain eligibility only |
| CQRS, aggregate/entity bases, domain/integration events, UnitOfWork, outbox/inbox, ID generation, clock, locks, transaction conventions | existing AeroTech Framework/repository | **reuse/extend existing implementation; never create a parallel framework** |
| ETKT/EMD/document commercial fulfillment truth | Ordering v1 document boundary unless a current certified issuer integration delegates execution | own local document history + provider evidence as defined in v1 |
| PDF/document rendering, templates, notification delivery | not established by this pack | discover existing AeroTech owner; if unclear, `BLOCKED_DECISION`; do not build renderer/notifier inside Ordering |

### Direct repository evidence already observed

The existing `Ordering` repository already contains Framework abstractions for aggregate/entity/value object patterns, repository/UnitOfWork, clock, distributed lock, ID generation, inbox/outbox and domain-event dispatch, plus Infrastructure implementations including command DbContext, Snowflake ID, UTC clock and distributed locking. The implementation agent starts from those classes and repository conventions.

Current Ordering ReferenceData also already has an AirInfo currency client/read model. This is evidence to reuse the existing reference-data mechanism, not permission to create another currency abstraction/master.

---

## 3. Framework-first implementation rule

### 3.1 What the agent must do

Before writing production code for a slice:

1. Inspect the exact existing Framework classes and the current Ordering usage patterns.
2. Inspect one or more mature sibling-service implementations when the same technical concern already exists in AeroTech.
3. Reuse the existing command/query buses, aggregate/entity bases, transaction decorators, outbox/inbox, synchronizers, error/result handling, ID generation, logging/telemetry, validation and API conventions.
4. Extend the **existing** Framework only when a real generic limitation is demonstrated and the change is compatible with existing consumers.
5. Prefer a small local adapter around an external contract when the concern is Ordering-specific; do not contaminate Framework with domain behavior.

### 3.2 What the agent must not do

The agent must not introduce a second:

- CQRS/mediator framework;
- UnitOfWork/repository framework;
- outbox/inbox mechanism;
- message envelope/correlation model;
- distributed transaction coordinator;
- ID generator;
- clock abstraction;
- general workflow/event-sourcing engine;
- currency/FX/reference-data subsystem;
- authentication/authorization stack.

This is a framework ownership rule, not a ban on improving the existing implementation. If the current Framework needs an upgrade, propose/implement the smallest compatible improvement **inside that framework/pattern**, with tests and impact analysis.

---

## 4. Domain specification versus code representation

Names and structures in the design pack describe business semantics. They do not force a particular C# representation unless the current platform contract already fixes it.

Examples:

- A semantic list of lifecycle states does not require a particular enum type or file layout.
- A monetary fact must carry enough amount/currency/provenance to be correct, but this pack does not authorize creation of a new `Money`, `CurrencyCode`, `ExchangeRate` or rounding library.
- A penalty has trigger/scope/collection/waiver semantics, but this pack does not prescribe one enum hierarchy for them.
- A servicing record must be immutable/redisplayable, but this pack does not prescribe whether the read side uses one table, several tables or an existing projection model.

Implementation mechanics are resolved by the repository/framework. The agent should **not ask the product owner about ordinary coding choices** that are already clear from repository conventions.

---

## 5. Mandatory Decision Gate: no hidden assumptions

An agent must stop the affected implementation and ask for confirmation when a decision would materially change any of the following and cannot be resolved from approved code/contracts/docs:

### Business/domain semantics

- who owns a lifecycle or authoritative value;
- whether a monetary component affects customer payable, settlement only, or is informational;
- refundability/reusability/forfeiture when no authoritative pricing decision exists;
- penalty calculation/waiver/collection treatment not returned by the source owner;
- whether a service/document can be changed, refunded, revalidated, reissued or voided when provider capability is unknown;
- whether a new domain aggregate/entity is required because current invariants cannot represent a proven scenario.

### Cross-service contract semantics

- currency identity or monetary representation;
- rate-of-exchange representation/source or conversion responsibility;
- tax/fee/commission semantics;
- Customer/Office/Carrier identity meaning;
- Payment/StoredValue/Ledger message meaning;
- DCS/FlightOps/Inventory provider outcome semantics;
- document/notification ownership;
- security/authorization context required by an endpoint or service call.

### Public/durable compatibility

- a new public API/event contract whose field meaning is not already established;
- a change to an existing shared Framework contract;
- a new persistence invariant that encodes an unconfirmed business rule;
- an irreversible provider operation whose retry/idempotency/unknown-outcome contract is unknown.

### Decision format

Create a `BLOCKED_DECISION` entry with:

```text
Decision ID
Affected slice/use case
Exact question (one business/contract decision only)
Why it is blocking correctness
Evidence inspected (files/classes/contracts)
What is known
What is unknown
Safe options, if more than one genuinely exists
Recommended option, only when evidence supports a recommendation
Code intentionally NOT written because of this decision
```

Ask the owner the exact question. Do not hide the choice behind a default, TODO, guessed enum, guessed precision, guessed currency rule or “temporary” provider behavior.

---

## 6. What does NOT require a Decision Gate

The agent should decide these from current framework/repository patterns without asking the product owner unless they expose a durable public/business semantic:

- class/file/folder naming consistent with the repo;
- enum vs value-object vs record implementation mechanics;
- private helper design;
- EF configuration organization;
- test fixture organization;
- dependency-injection registration style;
- controller/request mapping mechanics;
- logging syntax;
- code formatting/refactoring;
- replacing duplicated local technical code with an already-existing Framework primitive.

The goal is **no business assumptions**, not paralysis over ordinary engineering choices.

---

## 7. Monetary and currency gate

The prior draft over-specified monetary implementation. That instruction is superseded.

Binding semantics are only:

1. Every accepted monetary fact must retain the currency identity and amounts required to interpret it correctly.
2. Historical accepted monetary facts are not recomputed using today’s price, tax or FX rules.
3. Cross-currency values are never silently added/subtracted as if they were the same economic unit.
4. When Pricing/AirPrice/provider supplies original amount, converted/sale amount, applied rate reference/snapshot or rounding evidence, Ordering preserves the parts needed for audit/reversal according to the actual contract.
5. Ordering does not look up a new rate to reverse an old accepted transaction unless an explicit new Pricing operation instructs that treatment.
6. Pricing/tax source is authoritative for calculated accepted totals; Ordering validates consistency only where the source contract defines enough information to do so.
7. The concrete .NET/SQL/wire representation, scale/precision, currency key and rounding primitive are discovered from the current AeroTech platform. They are **not defined by this design pack**.

If those concrete representations are inconsistent across current services, implementation of the affected monetary boundary is `BLOCKED_DECISION`; the agent must show the conflicting evidence and ask for the platform decision.

---

## 8. Tax, commission and penalty gate

Ordering may need to preserve each accepted component, but calculation ownership remains external unless current platform contracts explicitly say otherwise.

- **Tax:** Pricing/tax owner calculates application and refund treatment. Customer-collected tax remains part of the customer commercial amount. Ledger decides accounting/settlement.
- **Commission:** source commercial/settlement result may be retained; Ordering does not derive agency commission from Core customer data.
- **Penalty:** Pricing/rule owner calculates/applies it. Ordering records the accepted outcome, scope, provenance, waiver/authority and whether source says it is netted or separately payable.
- **Manual adjustment:** requires an explicit authorized business operation/evidence. It cannot be a backdoor for missing tax/FX/pricing rules.

Any missing source result is not replaced by a local formula.

---

## 9. External-operation gate

For reservation, payment, issue, void, refund, reissue/revalidation, DCS control or supplier servicing, the adapter contract must define:

- stable operation/idempotency identity;
- accepted/pending/confirmed/rejected/unknown semantics;
- whether a timeout can imply NotExecuted (usually it cannot without provider guarantee);
- authoritative reconciliation/read-back method;
- irreversible boundary;
- capability/version needed for the requested operation.

If those are not known for the real provider, production execution for that operation is blocked. A mock adapter does not satisfy the release gate.

---

## 10. New domain concept gate

The design is intentionally extensible but not generic. During implementation, add a new domain concept only if:

1. a concrete accepted scenario cannot be represented safely by the current model; and
2. the missing concept has an Ordering-owned lifecycle/invariant, not merely data needed from another service; and
3. the new concept reduces ambiguity rather than mirroring a vendor message; and
4. the owner approves the domain-semantic change when it was not already specified.

Provider-specific data that does not change Ordering invariants belongs at the boundary/profile/projection, following existing platform patterns.

---

## 11. Phase-0 output required before material domain coding

P0 begins with a repository evidence pass. It must produce:

`docs/order-domain-design-v1/implementation/P0-DISCOVERY-AND-DECISIONS.md`

containing:

- exact Framework classes/patterns reused;
- Ordering projects/files/patterns reused;
- current monetary/currency/ID/time representation discovered;
- ReferenceData owners and current synchronization paths;
- existing external integration contracts for Pricing, Inventory, Payment, DCS, FlightOps, Core and Ledger that are available to the agent;
- each unresolved semantic/cross-service question as `BLOCKED_DECISION`;
- confirmed deviations/gaps where the existing Framework needs a compatible improvement;
- production code that is safe to implement before blocked decisions, if any.

**P0 is discovery plus unambiguous foundation work, not permission to invent missing platform primitives.**

---

## 12. Source-of-truth acceptance checklist

Before merging a slice, reviewers must be able to answer “yes” to all applicable items:

- Does every external fact have a named owner?
- Is Ordering storing a reference/snapshot/projection only when justified?
- Was an existing AeroTech primitive reused instead of duplicated?
- Are monetary calculations performed by the documented owner?
- Are provider Unknown outcomes represented without optimistic success?
- Are new public contracts backed by approved semantics?
- Are unresolved questions visibly blocked rather than hidden in defaults?
- Can a future owner/service implementation replace the adapter without changing Order core invariants?
- Did the implementation avoid copying vendor-specific workflow/data structures into the domain merely because a benchmark showed them?

