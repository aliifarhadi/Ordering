# P0 — Discovery and Decisions

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-07
**Gate:** required by `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` §11 and `04-IMPLEMENTATION-BLUEPRINT.md` §14 before material domain coding.

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
