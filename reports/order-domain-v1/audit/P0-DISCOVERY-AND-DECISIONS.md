# P0 — Discovery and Decisions

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-07
**Gate:** required by `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` §11 and `04-IMPLEMENTATION-BLUEPRINT.md` §14 before material domain coding.

**Location note:** `/docs/` is git-ignored in this repository, so this durable audit record lives at
`reports/order-domain-v1/audit/P0-DISCOVERY-AND-DECISIONS.md`. The copy under
`docs/order-domain-design-v1/implementation/` is a working convenience only.

**Revision 2 (2026-09-08):** owner decisions on reservation semantics, `RoundingFactor`, JetPay and
CommandReceipt scope applied; §10 (identity/context audit) and §11 (verification of the earlier P0
changes) added; BD-1 and BD-2 restated against those decisions.

Baseline verified before any change: `dotnet build AeroTech.Ordering.sln` → **0 errors, 14 warnings**.

---

## 1. What was inspected

| Area | Evidence read |
|---|---|
| Design pack | `MANIFEST`, `IMPLEMENTATION-INSTRUCTIONS`, `13` (all), `00`, `11` (P0 + shared), `01` §2/§17–§23, `04` §2/§3/§5/§9/§9A/§10/§11/§12/§13/§14/§15/§16/§17/§18, `07` §1/§2/§5, `08` §1/§2/§5/§9, `09` (all) |
| Framework | all 44 source files under `Framework/` (Core, Infrastructure, Presentation) |
| Ordering solution | project graph + all `src/**/*.cs` file inventory; full read of persistence/messaging/synchronizer/host composition, `Order` root, money value objects, pricing line + its EF configuration, ReferenceData currency path |
| Contracts | `AeroTech.Messages` root envelopes, `Ordering/**`, `JetPay/**` money shapes, service-folder inventory |
| Siblings | `AirPrice` (ROE ownership + precision, `AirPriceUnitOfWork`), `FlightFlow` (`FlightFlowUnitOfWork`) |
| Cross-service ledger | `E:\Projects\DotAir\handoffs\` — `STATE.md`, `README.md` index, `SV-017` in full |

---

## 2. Framework primitives to REUSE (no replacement is permitted or needed)

All of these exist and are used by Ordering today. P0 reuses them as-is.

| Concern | Exact type | File |
|---|---|---|
| Aggregate base, domain-event buffer, `RowVersion` concurrency token | `AggregateRoot<TId>` | `Framework/AeroTech.Framework.Core/Domain/Aggregates/AggregateRoot.cs` |
| Entity base + audit stamps | `Entity<TId>` | `.../Domain/Entities/Entity.cs` |
| Domain event | `DomainEvent(EventId, AggregateId, TimeOfOccurrence)` | `.../Domain/Events/DomainEvent.cs` |
| Error contract with code + REST status | `BusinessException(Code, message){HttpStatus}` | `.../Domain/Exceptions/BusinessException.cs` |
| Unit of work | `IUnitOfWork.SaveChangesAsync` | `.../Domain/Repository/IUnitOfWork.cs` |
| Repository shape | `IRepository<TAggregate,TId>` | `.../Domain/Repository/IRepository.cs` |
| Clock | `IClock` → `UtcClock` | `.../ServiceContracts/IClock.cs`, `Infrastructure/Services/UtcClock.cs` |
| ID generation | `IIdGenerator` → `SnowflakeIdGenerator` (IdGen, fail-fast `IdGenerator:GeneratorId`) | `.../ServiceContracts/IIdGenerator.cs`, `Infrastructure/Services/SnowflakeIdGenerator.cs` |
| Distributed lock | `IDistributedLock` → `RedLockDistributedLock` | `.../ServiceContracts/IDistributedLock.cs`, `Infrastructure/Services/RedLockDistributedLock.cs` |
| Outbox / inbox ports | `IOutboxWriter`, `IInboxStore` | `.../ServiceContracts/` |
| Domain-event dispatch | `IDomainEventDispatcher` → `MediatRDomainEventDispatcher` + `DomainEventNotification<T>` | `Application/_Shared/Events/` |
| Command context + audit + event dispatch on save | `CommandDbContext` | `Framework/.../Persistence/CommandDbContext.cs` |
| Security context | `IIdentityService` (`CurrentUserId`, `CurrentCustomerId`, `Claims`, `CheckAccess`) | `.../ServiceContracts/IIdentityService.cs`, `Presentation/AspNetCore/Services/IdentityService.cs` |
| API error surface | `ExceptionHandlingMiddleware` + `ApiResult` | `Framework/AeroTech.Framework.Presentation/` |
| Paging/grid | `Pagination`, `GridAttribute`, `MetadataGenerator` | `Framework/.../Domain/Queries/` |

Ordering-side patterns reused unchanged: per-aggregate folders; `ExceptionFactory`/`ExceptionMessages` (`Domain/_Shared/Resources/`, codes 2001–2607 in use, **2700+ free**); MediatR command/handler/validator triads; `{Channel}/v{version}/{Resource}` PascalCase controllers; `ReferenceSyncerBase` pull-sync; MassTransit consumers + `InboxConsumeFilter`; `OutboxPublisher`; three EF contexts with `dbo.__CommandsMigrationHistory` / `dbo.__QueriesMigrationHistory` / `dbo.__ReferenceDataMigrationHistory`.

**Nothing in P0 creates a second CQRS bus, outbox, inbox, UoW, ID generator, clock, lock, reference-data subsystem or monetary library.**

---

## 3. Current platform representations discovered (NOT invented here)

### 3.1 Monetary value — **consistent across services; no new primitive is authorized**

The platform representation is `decimal Amount` + `int CurrencyId`, where `CurrencyId` is the AirInfo/BasicInfo currency master id.

| Evidence | Shape |
|---|---|
| `Domain/OrderAggregate/Entities/OrderPricingLine.cs` | `decimal Amount`, `int CurrencyId`, `decimal EquivalentAmount`, `int EquivalentCurrencyId`, `ExchangeRate?` |
| `Contracts/.../Ordering/IntegrationEvents/V1/OrderIssued.cs` | `decimal GrandTotal, int CurrencyId`; per line `Amount/CurrencyId/EquivalentAmount` + rate provenance |
| `Contracts/.../JetPay/IntegrationEvents/V1/PaymentCompleted.cs` | `decimal CapturedAmount, int CurrencyId` |
| `Contracts/.../JetPay/AsyncCommands/V1/SubmitPaymentFactCommand.cs` | `decimal Amount`, `decimal TotalAmount, int CurrencyId` |

Conversion provenance is already modelled and already flows on the wire:
`ExchangeRate { decimal RateOfExchange, int NumberOfDecimalPlaces, string? RateOfExchangeId, int RoundingFactor }`
(`Domain/OrderAggregate/ValueObjects/ExchangeRate.cs`), paired with `EquivalentAmount` / `EquivalentCurrencyId`.

**Conclusion:** `01` §19 and `13` §7 are satisfied by the existing representation. Ordering does **not** get a `Money`, `CurrencyCode`, `ExchangeRate` primitive library, currency master or rounding engine. A previous attempt in this repository to introduce those types was deleted before this document was written.

Currency reference data is already pulled from AirInfo: `ReferenceData/Syncing/CurrencySyncer.cs` → `CurrencyReadModel { int Id, string Code, int DecimalPlaces, double RoundingFactor }` in the `ReferenceData` schema, joined read-only by `OrderQueryDbContext`. AirPrice owns rate-of-exchange tables (`AeroTech.AirPrice.Domain/RateOfExchangeTableAggregate/**`), matching `13` §2.

### 3.2 Identity and time

- IDs: platform snowflake `long` via `IIdGenerator`; `Order.Id` is `long`, plus a separate `Guid UniqueIdentifierId` and a business `RecordLocator`. Matches `01` §2.2 ("preserve the existing Snowflake/IdGen `long` convention").
- Time: `IClock.GetDateTime()` → `DateTimeOffset` UTC; all domain timestamps are `DateTimeOffset`.
- Concurrency: `AggregateRoot.RowVersion` (`byte[]`, `[Timestamp]`).
- No new time or ID abstraction is introduced.

### 3.3 Numeric precision — **one discovered conflict, see F-3**

`OrderingDbContext.ConfigureConventions` applies `decimal → (18,2)` to **every** decimal, including `ExchangeRate.RateOfExchange`. The generated schema confirms it:
`Migrations/20260721003513_InitialCreate.cs:418` → `RateOfExchange decimal(18,2)`.

The owner of that value, AirPrice, stores it as `decimal(28,12)`
(`AeroTech.AirPrice.Persistence/RateOfExchangeTableAggregate/RateOfExchangeTableEntityTypeConfiguration.cs:39`, and the same in its query model).

---

## 4. Ownership and contract map for facts P0/P1 need

| Fact | Owner | Contract available to this repo today | Ordering treatment |
|---|---|---|---|
| Currency reference (code, decimal places, rounding factor) | AirInfo/BasicInfo | `ReferenceData/AirInfo/AirInfoClient` + `CurrencyDto` (live pull-sync) | PROJECTION (existing) |
| Rate of exchange | AirPrice | none consumed by Ordering; rate provenance arrives inside an accepted price | SNAPSHOT of what the source supplied |
| Accepted offer / price | Offer + AirPrice | `Providers/Offer/Services/OfferProvider` (HTTP, real), `Providers/Pricing/Services/PricingProvider` (HTTP, real, bound-reservation validation only) | SNAPSHOT |
| Flight capacity / hold | FlightFlow | `Domain/Providers/FlightFlow/IFlightFlowProvider` + `Providers/FlightFlow/Services/FlightFlowProvider` (HTTP, real) | REFERENCE + evidence |
| Payment / value movement | JetPay (+ StoredValue as a tender) | **`IPaymentProvider` is bound to `MockPaymentProvider`** (`Providers/DependencyInjection.cs`); real integration is handoff **JP-002 (Requested)**, plus JP-007, SV-011, SV-012 | REFERENCE/PROJECTION — production execution blocked (`13` §9) |
| Accounting / GL | LedgerFlow | Ordering publishes `OrderIssued` / `OrderPaid` / `OrderCancelled`; `SubmitPaymentFactCommand` is JetPay→Ledger | publish facts only |
| Customer / office / actor | Core | `ReferenceData/Core/CoreClient` (customers pull-sync) | REFERENCE + snapshot |
| Airline / airport / city | AirInfo | ReferenceData pull-sync | PROJECTION |
| DCS, FlightOps, disruption | Dcs / SkyDispatch / FlightFlow | **no adapter in this repository** | not started (P4/P5) |
| Authentication / authorization | Identity / Aegis | `IIdentityService` only; Gateway (PEP) not built (`handoffs/STATE.md`) | consume; see **BD-1** |

Open handoff items where Ordering is the owner: **SV-017** (implemented by this P0 — see §6), **SV-011**, **SV-012**, **JP-002**, **JP-007** (all P1+, left untouched). `CR-001` and `LG-001` are already Verified.

---

## 5. Findings: existing pieces that conflict with the reviewed design

| ID | Finding | Evidence | Design rule broken | P0 action |
|---|---|---|---|---|
| **F-1** | `OrderingUnitOfWork.SaveChangesAsync` calls `SaveChangesAsync` on the command context and then on the query context — two connections, two transactions, no atomicity. It is the effective `IUnitOfWork` because `AddSynchronizer()` is registered after `AddPersistence()` in `Program.cs`. | `Synchronizer/OrderAggregate/OrderingUnitOfWork.cs`; `ServiceHost/Program.cs:19-27`; `Persistence/DependencyInjection.cs:33` vs `Synchronizer/DependencyInjection.cs:14` | `04` §5.2, §12; gate **M-0.1**; D-029 | **Fix in P0** — share one `DbConnection`/`DbTransaction`. Both siblings (`AirPriceUnitOfWork`, `FlightFlowUnitOfWork`) contain the identical defect with the `TransactionScope` commented out, so there is no existing platform pattern to copy: this is a demonstrated generic gap, fixed with the smallest compatible change inside the existing UoW shape. |
| **F-2** | `InboxConsumeFilter` is check-then-act: `HasProcessed` → `next.Send` → `MarkProcessed` (which does its own `SaveChanges`). Concurrent redelivery is processed **twice**; a crash between effect and marker reprocesses on restart. | `Consumers/Inbox/InboxConsumeFilter.cs:29-42`; `Persistence/Inbox/InboxStore.cs:29-44` | `04` §12 ("Inbox completion + Outbox + local views commit together"); handoff **SV-017** | **Fix in P0** — apply the SV-017 remedy already applied verbatim in StoredValue, Ledger and JetPay. |
| **F-3** | `ExchangeRate.RateOfExchange` is stored `decimal(18,2)` while its owner AirPrice stores `decimal(28,12)`. Any rate that is not a 2-decimal number is silently corrupted on write. | `Persistence/OrderingDbContext.cs:52`; `Migrations/…InitialCreate.cs:418,714`; AirPrice `RateOfExchangeTableEntityTypeConfiguration.cs:39` | `01` §19 rule 3 ("preserve the exact accepted/source-provided values"); gate **M-0.4** | **Fix in P0** — adopt the owner's precision. This is discovery of the platform representation, not a new rounding policy. |
| **F-4** | `Order.OrderVersion` increments once per emitted domain event, including payment- and document-only transitions (`Order.Payment.cs`, `Order.Issue.cs`) and remark edits. | 16 `IncrementVersion()` call sites in `Domain/OrderAggregate/Order.*.cs` | D-019/D-031; `01` §21; `04` §9A | **Correct in P0** — see §6.D. |
| **F-5** | `OrderStatus` is the exact global workflow enum `00` D-008 rejects (`Created → Confirmed → Paying → Paid → Ticketing → Ticketed`), gated by `OrderStateMachine`, and is the Order's only status. | `Contracts/.../Ordering/Enums/OrderStatus.cs`; `Domain/OrderAggregate/OrderStateMachine.cs`; `Order.cs:104` | D-008; `01` §17 | **Not P0.** Replacing it means introducing `CommercialSummary` plus per-operation eligibility policies (`07`) and the OrderItem/OrderService status model — that is P1/P3 domain work. Recorded here so it is not lost. |
| **F-6** | `Domain` project references `Contracts/AeroTech.Messages`; domain types use wire enums directly. | `AeroTech.Ordering.Domain.csproj`; `Order.cs:4` | `04` §9 ("Domain must not directly depend on message DTOs") | **Not P0.** Reversing it is coupled to F-5 (the enums that would move are the ones being replaced). Deferred to the slice that replaces the status model. |
| **F-7** | `IntegrationEventOptions.TenantId` defaults to `1` in code. | `Persistence/Outbox/IntegrationEventOptions.cs:5` | `01` §22 ("not hardcoded to 1 in new messages"); repo rule "no hardcoded parametric config" | **Not P0 by itself** — it is bound to `OwnerAirlineId`/trusted-context introduction, which is **BD-1**. |
| **F-8** | Outbox has no `StreamKey`/`StreamSequence`; inbox has no `SourceSystem`; envelope has no `CommercialVersion`/`EventOrdinal`/`FinancialSequence`/`StreamId`. | `Persistence/Outbox/OutboxMessage.cs`, `Persistence/Inbox/InboxMessage.cs`, `Contracts/…/BaseIntegrationEvent.cs` | `08` §9; `01` §18 INV-X01 | **Partly P0** — `EventOrdinal`/`CommercialVersion` land with §6.D. Stream sequencing and `FinancialSequence` require `PriceChangeSet`, which does not exist until P2/P3; adding a sequence with no stream to sequence would be speculative. Deferred with reason. |
| **F-9** | `IPaymentProvider` resolves to `MockPaymentProvider` in all environments. | `Providers/DependencyInjection.cs:41` | `13` §9; `04` §13.1 | **Not P0.** Real payment is JP-002. Recorded so no slice mistakes the mock for capability. |

---

## 6. P0 work that is safe to implement now

Each item below is either (a) an explicit binding instruction in the pack with no representation choice left open, or (b) adoption of a representation already proven in the platform.

**A. SV-017 — inbox marker joins the consumer's transaction.**
`IInboxStore` gains `EnlistProcessed` + `PersistProcessedAsync` (additive; `MarkProcessedAsync` keeps its effect). `InboxStore` implements them on `OrderingDbContext`; `HasProcessedAsync` gains `AsNoTracking()`. `InboxConsumeFilter` enlists before `next.Send` and quietly discards a losing concurrent delivery after re-checking `HasProcessed` inside `catch (DbUpdateException)`. Mirrors the three siblings verbatim.

**B. Atomic local commit (M-0.1 / D-029 / `04` §5.2, §12).**
`OrderingUnitOfWork` opens one `DbConnection`, begins one `DbTransaction`, enlists both `OrderingDbContext` and `OrderQueryDbContext` on it, saves both, commits once, and rolls back on failure. Source rows, outbox rows, inbox marker and read-model rows become one atomic unit.

**C. Rate-of-exchange precision (F-3 / M-0.4).**
`RateOfExchange` mapped `HasPrecision(28, 12)` on both owning configurations (`OrderPricingLine`, `OrderPricingLineAllocation`) with a widening migration. Amounts stay `(18,2)`; only the rate adopts the owner's precision.

**D. Version-purpose correction (D-019 / D-031 / `01` §21 / `04` §9A).**
`Order.OrderVersion` → `Order.CommercialVersion`, `Create` = 1, incremented exactly once per committed commercial mutation. Increments are removed from payment-, document- and projection-only transitions per `01` §21. Emitted events carry `CommercialVersion` and an `EventOrdinal` within the mutation. `RowVersion` remains the sole concurrency token. `FinancialSequence` is **not** added yet (see F-8).

**E. Durable operation and claim primitives (`07` §5, `08` §5, `04` §10.1).**
New `Operations` persistence area: `CommandReceipt` (unique `OwnerAirlineId, CallerScope, OperationName, IdempotencyKey`, `RequestHash`, `PayloadRef`, allocated ids, `Status`), `ServicingOperation` (`OperationId`, `ReceiptId`, `OrderId`, `Kind`, `Status`, `ExpectedCommercialVersion`, `ClaimGeneration`), `OperationOrderClaim` (**filtered unique index on `OrderId` where `IsBlocking = 1`**, `Generation` fencing token, `RecoveryLeaseUntil` that never auto-releases an unresolved claim). Storage shapes and invariants are fully specified by the pack. The claim service is implementable end-to-end. **Receipt issuance from a request is blocked — see BD-1**, so the receipt table is created and unit-testable but not yet wired to a controller pipeline.

**F. Tests as release gates (`04` §15; supersedes the repository's "tests paused" rule).**
`tests/AeroTech.Ordering.Domain.Tests` and `tests/AeroTech.Ordering.Persistence.Tests` (xunit), covering: version increments once per mutation and not at all for payment/document-only transitions; claim uniqueness under concurrency; claim generation fencing; inbox marker atomicity (effect + marker commit or roll back together, duplicate discarded quietly, throwing consumer leaves no marker); command+query atomicity under injected failure; rate precision round-trip.

### Explicitly NOT done in P0

No P1 sale/issue/refund behaviour. No `CommercialSummary`/eligibility-policy replacement of `OrderStatus` (F-5). No Domain→Contracts inversion fix (F-6). No `PriceChangeSet`/`FinancialSequence`/stream sequencing (F-8). No new HTTP endpoints. No compatibility layer for current consumers. No changes in sibling repositories.

---

## 7. BLOCKED_DECISIONs

### BD-1 — What is the trusted `OwnerAirlineId` and `CallerScope` for a command receipt?

- **Affected slice / use case:** P0 §6.E receipt issuance; every P1+ mutation envelope (`08` §2), `01` §22 owner scope, and F-7 (`TenantId = 1`).
- **Exact question:** For an authenticated Ordering request, which claim or configuration value is the trusted `OwnerAirlineId`, and what identifies `CallerScope` — the Aegis authorization context (`travel_agency_id` for `otapanel`/`api`), the airline office, the Identity subject, or a composite?
- **Why blocking:** `CallerScope` is part of the receipt's uniqueness key. Choosing it wrongly either lets one caller replay another caller's key (a security hole) or splits one caller's retries into several receipts (a duplicate-money hole). `13` §5 lists security/authorization context as a mandatory gate.
- **Evidence inspected:** `Framework/.../ServiceContracts/IIdentityService.cs` (exposes `CurrentUserId`, `CurrentCustomerId`, raw `Claims`, `CheckAccess(scopeType, scopeId)` — no airline or agency accessor); `Presentation/AspNetCore/Services/IdentityService.cs`; `handoffs/STATE.md` FINAL items 3, 5, 6 (surfaces `backoffice|otapanel|ibe|api|service|account`; implicit scope: `otapanel`/`api` take `travel_agency_id` **from the token, never input**; token claims are frozen in `WORKORDER-SHARED.md` §5, which is not present in this repository); `handoffs/README.md` (Gateway/PEP **not built**, H8).
- **Known:** the platform has a frozen token claim contract and a fixed surface list; a single owning airline per deployment (`01` §22); `TenantId` is transported from trusted context, not hardcoded.
- **Unknown:** the claim names, and whether `OwnerAirlineId` comes from a claim or from Ordering deployment configuration.
- **Safe options:** (i) `CallerScope` = `{surface}:{authorization-context-scope-key}` from the token, `OwnerAirlineId` from fail-fast Ordering configuration; (ii) both from token claims once `WORKORDER-SHARED.md` §5 is supplied.
- **Recommended:** none — the claim contract has not been seen from this repository.
- **Code intentionally NOT written:** the request→receipt pipeline (envelope binding, `CallerScope`/`OwnerAirlineId` resolution, `RequestHash` normalization over authenticated identity, replay authorization, `409` on same-key/different-payload) and the removal of the hardcoded `IntegrationEventOptions.TenantId = 1`. The receipt table, its unique constraint and its status model are created; nothing writes to it yet.

### BD-2 — What is `RoundingFactor`, and which type is authoritative?

- **Affected slice / use case:** any Ordering code that rounds or reconciles a converted amount — P2 pricing, P3 refund/exchange reconciliation; the `ExchangeRate` snapshot written today.
- **Exact question:** Is `RoundingFactor` a number of units to round to, a divisor, or a scale exponent — and is its authoritative type `double` (AirInfo `CurrencyDto`/`CurrencyReadModel`) or `int` (Ordering `ExchangeRate`)?
- **Why blocking:** it is a rounding rule applied to customer money. `13` §7 rule 7 leaves rounding representation to the platform and forbids a local default; a wrong reading changes every converted total.
- **Evidence inspected:** `ReferenceData/AirInfo/Wire/AirInfoDtos.cs` (`double RoundingFactor`), `ReferenceData/ReadModels/CurrencyReadModel.cs` (`double`), `Domain/OrderAggregate/ValueObjects/ExchangeRate.cs` (`int`), `Domain/OrderAggregate/Arguments/ExchangeRateArgs.cs`; AirPrice stores rates at `(28,12)` but its rounding semantics were not asserted from a contract document.
- **Known:** currency metadata is AirInfo-owned and already synchronized; `DecimalPlaces` is a separate field; the two representations disagree in type across the same platform.
- **Unknown:** the semantic and the authoritative type.
- **Safe options:** none — a guess here silently changes money.
- **Code intentionally NOT written:** no rounding/quantization helper, no reconciliation check that consumes `RoundingFactor`, and **no change to the existing `int` field** (leaving the current shape untouched is the only non-destructive choice until the question is answered). Rate *storage precision* (§6.C) is fixed independently, because it is a discovered owner representation rather than a rounding rule.

### BD-3 — Is the Ordering→JetPay payment contract the one in JP-002?

- **Affected slice / use case:** P1 payment coverage and issue eligibility.
- **Exact question:** may Ordering implement against `JP-002-ordering-payment-integration.md` as written (payable-snapshot pull, guarantee/capture/cancel/refund/supersede, consume JetPay events), and does JetPay expose outcome-lookup by idempotency key for UNKNOWN recovery?
- **Why blocking:** `13` §9 requires stable operation identity, accepted/pending/confirmed/rejected/unknown semantics, a read-back method and the irreversible boundary before any real-money call.
- **Evidence inspected:** `handoffs/JP-002` (**Requested**, not agreed), `JP-007` (Requested), `JP-010` (Requested; **Ledger credit half blocking** — "no lookup, unresolved credit leg strands the payment"), `SV-011`, `SV-012`; `Providers/DependencyInjection.cs` binds `IPaymentProvider` to `MockPaymentProvider`.
- **Known:** JetPay contracts exist in `AeroTech.Messages/JetPay`; Ordering has no real payment adapter.
- **Unknown:** whether JP-002 is accepted, and the UNKNOWN-recovery read-back.
- **Code intentionally NOT written:** all real payment execution. No P0 code depends on it.

---

## 8. Framework changes proposed (smallest compatible)

| Change | Why it is a generic gap, not a domain concern | Compatibility |
|---|---|---|
| `IInboxStore` gains `EnlistProcessed` + `PersistProcessedAsync` | idempotent-messaging primitive; the same signature was already added to the StoredValue, Ledger and JetPay copies of this Framework under SV-017 | additive; `MarkProcessedAsync` kept with unchanged effect |
| `OrderingUnitOfWork` shares one connection/transaction | atomic command+query commit; both siblings have the defect, so no existing pattern covers it | contained in Ordering's own `Synchronizer` project; `IUnitOfWork` signature unchanged |

No other Framework file is modified.

---

## 9. Exit criteria for P0 — result

| Criterion | Result |
|---|---|
| This document complete and evidence-backed | **done** |
| §6 A–F implemented; §7 items left unwritten and named | **done** |
| `dotnet build AeroTech.Ordering.sln` clean | **0 errors, 2 warnings** |
| Domain, persistence, concurrency and fault tests green | **23 passed, 0 failed** (7 domain + 16 persistence) — see `P0-TEST-RUN.txt` |
| No duplicate platform subsystem introduced; no business or cross-service semantic invented | **held** — the only Framework change is the additive `IInboxStore` pair already agreed under SV-017 |

### What landed

| Item | Files |
|---|---|
| A — SV-017 inbox atomicity | `Framework/…/ServiceContracts/IInboxStore.cs`, `Persistence/Inbox/InboxStore.cs`, `Consumers/Inbox/InboxConsumeFilter.cs` |
| B — atomic command+query commit | `Synchronizer/OrderAggregate/OrderingUnitOfWork.cs` |
| C — rate precision `(28,12)` | `Persistence/OrderAggregate/OrderPricingLine{,Allocation}Configuration.cs` + migration |
| D — `CommercialVersion` semantics | `Domain/OrderAggregate/Order*.cs` (9 non-commercial increments removed), 5 domain events, 5 integration events, read model, snapshot, DTO, synchronizer + 2 migrations |
| E — durable operation primitives | `Contracts/…/Ordering/Enums/{ServicingOperationKind,ServicingOperationStatus,CommandReceiptStatus}.cs`, `Domain/_Shared/Operations/Contracts/*`, `Persistence/Operations/*`, `ExceptionFactory` 2700–2703 |
| F — test gate | `tests/AeroTech.Ordering.Domain.Tests`, `tests/AeroTech.Ordering.Persistence.Tests` |

Migrations applied to the local dev database: `20260907202858_P0OperationsAndRatePrecision` (command), `20260907…_P0CommercialVersion` (query). Both are non-destructive — a column rename plus a widening of `decimal(18,2) → decimal(28,12)`.

### Version-purpose classification applied (`01` §21)

| Transition | Advances `CommercialVersion` | Basis |
|---|---|---|
| `Create` | establishes 1 | §21 |
| `Cancel`, `Expire`, `SplitOff`, remark add/modify/delete | yes, once | commercial mutation |
| `MarkDocumentVoided` | yes, once | it appends reversal pricing lines — a material change to accepted money, not merely a document event |
| `MarkPaid`, `MarkPaymentFailed`, `MarkPaymentUnconfirmed` | no | §21 "payment … updates do not" |
| `CompleteIssue`, `FailIssue`, `MarkTicketingUnconfirmed` | no | §21 "document … updates do not" |
| `CompleteReserve`, `FailReservation`, `MarkReservationUnconfirmed` | no | reservation is fulfilment evidence (D-010), read as §21 "flight" |

The reservation row is the only judgement call in this table; if the owner reads reserve confirmation as a commercial mutation, it is a one-line change plus one test.

### Deliberately not added

`EventOrdinal`, `FinancialSequence`, `StreamId`/`StreamSequence` and `SourceSystem` on the inbox. `08` §9 places them on the shared `IntegrationEnvelope`, but `BaseIntegrationEvent` is a **platform-wide** contract used by Aegis, JetPay, LedgerFlow and others — changing it is a shared-Framework-contract decision under `13` §5, not an Ordering call. Independently, every current Ordering mutation emits exactly one event and there is no `PriceChangeSet` yet, so both fields would be constants. Revisit when P2/P3 introduce `PriceChangeSet` and multi-event mutations.

`UpdateTimeToLive` still does not advance the version. It changes an accepted time limit, which §21 could read as a term change, but it emits no event and has no projection path today; changing it is new behaviour rather than a correction, so it is left for the slice that owns time limits (`01` §9).

---

## 10. Identity and authenticated-context audit (added 2026-09-08)

### 10.1 The authoritative current contract

Issuer: `E:\Projects\DotAir\IdentityServer`, `src/AeroTech.Identity.Application/TokenIssuance/IdentityTokenClaims.cs`.

```
principal_type   authz_surface    roles                    customer_id      context_type
airline_user_id  airline_office_id                         travel_agency_user_id
travel_agency_id travel_agency_office_id                   individual_id
partner_api_access_profile_id                              service_code     session_id
```

Consumer side, verified in two sibling services that already implement it:

| Service | Files | Enum source |
|---|---|---|
| Aegis | `src/AeroTech.Aegis.ServiceHost/CallerContext/ClaimsCallerContext.cs` + `Domain/_Shared/Contracts/ICallerContext.cs` | **shared** `AeroTech.Messages.Aegis.Enums` / `AeroTech.Messages.Shared.Enums` |
| Core | `src/AeroTech.Core.ServiceHost/CallerContext/ClaimsCallerContext.cs` + `Domain/_Shared/Contracts/ICallerContext.cs` | **local** `CallerContextType` / `CallerPrincipalType` enums |

Aegis is the authorization owner and binds to the shared contract enums, so **Aegis's shape is canonical**. Core's local enums are duplication on the Core side; sibling repositories were not touched.

Live confirmation from the platform oracle (`E:\Projects\DotAir\FlowTests`): `GatewayServesLogin.cs:85-86` asserts `authz_surface == "otapanel"` (lowercase string) and a populated `travel_agency_id`. Aegis parses these enums with `ignoreCase: true`; the Ordering implementation matches.

### 10.2 Finding I-1 — Ordering's Framework identity accessor read claims that no longer exist

`Framework/AeroTech.Framework.Presentation/AspNetCore/Services/IdentityService.cs` read `UserId`, `CustomerId`, `DeviceId` and `FullScope`. **None of these appear in `IdentityTokenClaims`.** Behaviour on a current token, before the fix:

| Member | Behaviour | Blast radius |
|---|---|---|
| `CurrentUserId` | always `null` | `CommandDbContext.StampAudit` wrote `LastUpdatedBy = null` on **every** aggregate; `OutboxWriter` stamped `Actor = null` on **every** integration event |
| `CurrentCustomerId` | always `null` | no current call site |
| `RequiredCurrentUserId` | `NullReferenceException` | `AddOrderRemarkCommandHandler`, `CancelOrderCommandHandler`, `VoidTrafficDocumentCommandHandler` — three live servicing paths |
| `RequiredDeviceId` | throws (`Single` over a missing claim) | no call site |
| `CheckAccess` | always `ForbiddenException` (no `FullScope` claim is issued) | no call site |

This is a silent audit-trail and servicing defect, not a theoretical one: every order written through the current platform carried a null actor.

### 10.3 Call-site inventory

`IIdentityService` is consumed at 6 production sites: `CommandDbContext` (audit stamping), `OutboxWriter` (event `Actor`), three command handlers (`RequiredCurrentUserId`), and 4 controllers that inject but do not currently read it. **No handler or controller parses raw claims directly** — there was no duplication to remove, and none was introduced.

### 10.4 Change made — reuse first, smallest compatible improvement second

1. **Reused, not rebuilt.** `ICallerContext` replicated from Aegis's canonical shape into `src/AeroTech.Ordering.Domain/_Shared/Contracts/ICallerContext.cs`, implemented by `src/AeroTech.Ordering.ServiceHost/CallerContext/ClaimsCallerContext.cs` — the same two-file placement Aegis and Core both use. It binds to the **shared** contract enums (`BusinessContextType`, `PrincipalType`, `AuthorizationSurface`) that `AeroTech.Messages` already ships to this repository. No new security model, no new enum, no policy evaluation.
2. **Smallest compatible Framework improvement.** `IdentityService.CurrentUserId` now resolves the actor from `airline_user_id ?? travel_agency_user_id ?? individual_id ?? partner_api_access_profile_id` (Aegis's exact `ActorId` precedence) and `CurrentCustomerId` from `customer_id`. `SingleOrDefault` became `FirstOrDefault` because `travel_agency_office_id` is legitimately multi-valued. The `IIdentityService` signature is unchanged, so all 6 call sites keep working and the audit trail starts recording a real actor.
3. `ICallerContext` is registered in `ServiceHost/Program.cs`; `IHttpContextAccessor` was already registered by `AddPresentation`.

**Deliberately not changed:** `RequiredDeviceId` and `CheckAccess`. `DeviceId` and `FullScope` are no longer issued, but `CheckAccess` is authorization enforcement — Aegis owns that, and rewriting it here would recreate Aegis policy inside Ordering (`13` §3.2). Both are dead code in Ordering. Recorded as debt **I-2**: delete or re-home them when the Gateway/PEP lands (`handoffs/STATE.md` H8).

### 10.5 CallerScope — canonical encoding built from existing platform primitives

Per the owner's resolution, `CallerScope` = authenticated principal/credential + selected BusinessContext + AuthorizationSurface. Implemented in `src/AeroTech.Ordering.Domain/_Shared/Operations/CallerScope.cs` as:

```
{AuthorizationSurface}|{AuthorizationContextScopeKey}|{Subject:<sub> | Client:<client_id>}
```

The middle segment reuses **`AeroTech.Messages.Aegis.AuthorizationContextScopeKey`** — the platform's existing canonical context-scope encoding — instead of a new format: Airline to `AirlineOffice:{id}`, TravelAgency to `TravelAgency:{id}`, Individual to `Individual:{id}`, PartnerApi to `PartnerApiAccessProfile:{id}`. `Service` and `Global` carry no business identifier and use the context name.

Human principals key on `sub`; machine principals key on `client_id`. **A context type whose identifying claim is absent is rejected (`2705`, HTTP 403) rather than collapsed into a shared scope** — collapsing is precisely the failure mode that would let one caller replay another caller's idempotency key.

New reason codes: `2704 CallerContextUnavailable` (401), `2705 CallerContextIncomplete` (403).

### 10.6 Tests added

`tests/AeroTech.Ordering.Domain.Tests/_Shared/CallerScopeTests.cs` — 13 cases proving context isolation: two agencies; two subjects in one agency; one subject on two surfaces; an airline office and an agency sharing the same numeric id; and two API credentials for one agency all produce distinct scopes. The same caller is stable. Unauthenticated gives 2704; missing surface, context, subject or context identifier gives 2705; a `Service` context needs no business identifier.

`tests/AeroTech.Ordering.Persistence.Tests/Operations/CommandReceiptScopeTests.cs` — 8 cases proving idempotency-scope isolation at the database level: one caller reusing a key for one operation is rejected by the unique index; five different-scope pairs sharing a key all persist; two owner airlines sharing a scope and key both persist; one caller may reuse a key across different operations.

---

## 11. Verification of the earlier P0 changes (added 2026-09-08)

Re-reviewed and re-tested rather than accepted on compilation. Every failure found in this pass was a **test defect**; each is listed below for honesty.

| Change | How it was verified beyond compiling | Result |
|---|---|---|
| Inbox transaction fix (SV-017) | marker and effect commit together; a throwing consumer leaves no marker; two simultaneous contexts enlisting the same `MessageId` leave exactly one row; fast-path redelivery still short-circuits | 4 tests, pass |
| Atomic command/query transaction | both contexts prove to be on **one physical connection** (`Assert.Same`); no transaction left open on either context; repeated saves in one scope each commit; a caller-owned ambient transaction governs the commit and its rollback discards the work; a failing query projection rolls back the command write; a failing command write leaves no projected row; a rolled-back projection leaves the pre-existing read-model row untouched | 7 tests, pass |
| Rate-of-exchange precision | `INFORMATION_SCHEMA` asserts `decimal(28,12)` on both `OrderPricingLines` and `OrderPricingLineAllocations`; a 12-decimal rate round-trips through SQL Server unchanged | 3 tests, pass |
| CommercialVersion semantics | create = 1; cancel and expire advance exactly once; payment, document-issue and reservation outcomes do not advance; a **rejected** command does not advance; a **no-event** transition (`MarkCancelUnconfirmed`) does not advance; an unconfirmed-then-confirmed reservation retry stays at 1; the emitted `OrderCancelled` carries 2 and the emitted `OrderIssued` carries 1 | 12 tests, pass |
| CommandReceipt | unique index columns asserted directly against `sys.indexes` in exact key order (primary key excluded); collision and isolation behaviour per §10.6 | 9 tests, pass |
| ServicingOperation | table, indexes and enum-backed status/kind persist; exercised as the receipt/claim companion | covered by migration + constraint tests |
| OperationOrderClaim and generation fencing | a second operation is refused (2700); recovery by the same operation advances the generation; a stale generation is refused by both `EnsureCurrentGeneration` and `Resolve` (2702); a foreign operation cannot resolve another's claim (2701); an **expired lease does not release an unresolved claim**; an **unsaved resolve does not free the order**; a resolved claim is retained as history alongside the new blocking claim; 6 concurrent operations yield exactly one blocking claim | 11 tests, pass |
| Migrations | `sys.indexes` confirms the filtered unique index exists, is unique and filters on `IsBlocking`; both migrations applied to the local dev database | 2 tests, pass |

### Test defects found and fixed in this pass

1. `The_receipt_uniqueness_key_covers_owner_scope_operation_and_key` also matched the clustered primary key, because a PK is `is_unique = 1` too. Added `AND i.is_primary_key = 0`.
2. `A_rolled_back_projection_leaves_the_read_model_untouched` attached two `OrderReadModel` instances with the same key in one context. Rewritten to force the failure on the command side, which is also the more meaningful direction.
3. `FulfillmentFailureReason.ProviderTimeout` does not exist; the enum's unknown-outcome member is `UnknownOutcome`.

### Confirmed behaviour worth recording

`OperationClaimStore.ResolveAsync` deliberately does **not** call `SaveChanges`. It enlists the release in the caller's unit of work, so the claim is freed by the same transaction that commits the finalization. A crash between "resolve" and "commit" therefore leaves the claim held, which is the safe direction. This is now pinned by `An_unsaved_resolve_does_not_free_the_order`.

---

## 12. Owner decisions applied (2026-09-08)

| Decision | Effect on this repository |
|---|---|
| Reservation confirmation/release/rejection/expiry/unknown resolution is fulfillment evidence, not a commercial mutation | Confirms the classification already implemented and now pinned by 3 additional tests. The §9 note calling this "the only judgement call" is resolved; `CompleteReserve`, `FailReservation` and `MarkReservationUnconfirmed` correctly do not advance `CommercialVersion`. A later commercial action caused by a reservation outcome remains a separate mutation. |
| The `RoundingFactor` mismatch is deferred contract debt; Ordering owns no FX or rounding calculation | **BD-2 is downgraded from a blocker to recorded debt.** No rounding abstraction was created; the existing `int` field is untouched and unread. If a future boundary needs the exact value while the owner contract is still ambiguous, only that boundary blocks. |
| The unresolved JetPay contract is not a P0 blocker, but blocks real P1 payment-backed issuance/refund | **BD-3 is re-scoped to P1.** `MockPaymentProvider` remains bound and is explicitly **not** production evidence for any eligibility or release gate. |
| `OwnerAirlineId` is trusted operator/deployment context, not request body and not a normal Identity JWT claim; `CallerScope` is principal/credential + BusinessContext + AuthorizationSurface | **BD-1 is split.** The `CallerScope` half is now resolved and implemented (§10.5). The `OwnerAirlineId` half remains blocked as **BD-1a**. |

### BD-1a — the trusted operator-context source for `OwnerAirlineId`

> **CLOSED 2026-09-08 (P0.5).** Source: `Core.OperatorSettings.HomeAirlineId` where
> `ScopeKey = HOME_OPERATOR`, consumed through Core's existing `GET Service/v1/OperatorSettings`
> Service2Service contract and Ordering's existing reference-data pull-sync. See
> `P0.5-OWNER-AIRLINE-AUDIT.md`. The `IntegrationEventOptions.TenantId = 1` item below is **no longer
> gated by this decision** — the owner has ruled `TenantId` a separate platform concern (F-7 stays open
> on its own). The original entry is retained below for the record.

- **Affected:** `CommandReceipt.OwnerAirlineId`, `ServicingOperation.OwnerAirlineId`, and `IntegrationEventOptions.TenantId` (finding F-7, still hardcoded to `1`).
- **Exact question:** which existing platform component supplies the trusted owning-airline identity to a service at runtime — deployment configuration read by the service, a Core/Identity operator-context endpoint, or a platform-provided host abstraction?
- **Why blocking:** it is part of the receipt uniqueness key and stamps every published event. Ordering must not invent an operator-identity source.
- **Evidence inspected:** `IdentityTokenClaims` (no owning-airline claim — consistent with the owner's statement); `ICallerContext` in Aegis and Core (no owner-airline member); a search across `JetPay/src`, `StoredValue/src` and `Aegis/src` for `OwnerAirlineId`, `OwnerAirline`, `OperatingAirline` and `HomeAirline` returned **no match**; Ordering's `appsettings.json` has no such key.
- **What is known:** one owning airline per deployment (`01` §22); the value is trusted operator context; it is not user input.
- **What is unknown:** the source. **No trusted operator-context provider exists anywhere in the platform today** — this is a missing platform capability, not a missing lookup.
- **Code intentionally NOT written:** the receipt/operation write path that stamps `OwnerAirlineId`, and the removal of the hardcoded `TenantId = 1`. The columns, the unique key and the isolation tests exist; the tests supply the value explicitly.
