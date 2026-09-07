# CLAUDE.md — Ordering

AeroTech.Ordering is a **greenfield PSS (airline Passenger Service System) Order Service**: it owns the
order lifecycle from offer → reservation (inventory hold) → payment → ticket issuance, plus post-sale
servicing (cancel, void, split/divide-booking, remarks, expiry). Single-carrier scope — interline / GDS /
NDC distribution is deliberately out of scope.

**Stack:** .NET 10 (`net10.0`), EF Core 10 (SQL Server), MediatR 12, MassTransit 8 over RabbitMQ, IdGen
(snowflake IDs), RedLock.net (distributed lock). DDD + CQRS + Clean Architecture. The contracts project
`Contracts/AeroTech.Messages` targets `net9.0` on purpose (shared wire contracts, broader sibling reach).

## Reference projects — replicate, don't cross-reference

Two sibling repos are the pattern source: **AirPrice** (`E:\Projects\DotAir\AirPrice`) and **FlightFlow**
(`E:\Projects\DotAir\FlightFlow`). When aligning with a sibling, **copy the pattern in** — never add a
cross-repo project reference. Before hand-rolling infrastructure (outbox, inbox, idempotence, query
pagination), check the siblings' `Framework/` for an existing implementation first. The **query side
follows AirPrice**; **domain / consumers / providers conventions follow FlightFlow**.

## Solution layout & dependency direction

```
Framework/         Core ← Infrastructure ← Presentation   (shared, sibling-aligned; not Ordering-specific)
Contracts/         AeroTech.Messages          — wire contracts + enums (leaf; net9.0)
src/
  Domain           aggregates, domain events, invariants   (→ Framework.Core, Contracts)
  Application      MediatR handlers, app services, ports    (→ Domain)
  Persistence      EF command side, outbox, inbox           (→ Domain, Framework.Infrastructure)
  Query            read models + queries (CQRS read side)   (→ Application, Providers, ReferenceData)
  Synchronizer     projects command state → read models     (→ Persistence, Providers, Query)
  Consumers        MassTransit consumers, outbox publisher, pollers (→ Contracts, Persistence, Synchronizer)
  Providers        ACLs to FlightFlow / payment / offer      (→ Contracts, Domain)
  ReferenceData    isolated pull-sync module (own schema/context; zero domain coupling)
  RestApi          controllers                              (→ Application, Synchronizer)
  ServiceHost      composition root / host
```

**Known layering inversion:** `Domain` references `Contracts/AeroTech.Messages` for its enums — so a wire
enum change is a domain-breaking change. Documented + deferred in `docs/DomainAuditRemediationPlan.md` (P3).

## Placement & naming (enforced)

Put every type in its **most relevant, most related folder** — a folder name is a promise about its
contents. Ask "what *is* this type?", not "who uses it". Per-aggregate folders throughout. Naming and
folder structure are treated as first-class; folder = namespace, mirroring the sibling layout. Solution
folder conventions are AirPrice-faithful (see `docs/` and the memory notes if present).

## Domain model essentials

- **Aggregates:** `Order` (root), `TrafficDocument` (ticket/EMD + coupons), `Payment`, `FulfillmentTask`,
  `ProviderInteraction`. Reference other aggregates **by identity only** (Vernon) — never hold a foreign
  root object. Cross-aggregate orchestration lives in Application services within one transaction
  (`OrderingDbContext` is the `IUnitOfWork`).
- **State machine:** `OrderStateMachine` gates every `Order.TransitionTo`. Don't bypass it.
- **`OrderVersion` is a business version**, not a concurrency token. It is incremented **inside the domain
  transition, immediately before `Causes(...)`** — once per emitted event — so the event payload, the
  read-model snapshot, and the write row all agree. `Order.Create` establishes v1 and does not bump.
  No-event transitions (e.g. `MarkCancelUnconfirmed`) do **not** bump. **`RowVersion`** (byte[], on
  `AggregateRoot`) is the separate optimistic-concurrency token.
- **Pricing:** line-grain `OrderPricingLine` ledger with reversal lines (`OriginalPricingLineId` links a
  reversal to what it reverses). Invariant: `GrandTotal == signed Σ lines` on every event.

## Messaging & contracts

- **Flow:** domain event → `DomainEventNotification<T>` (MediatR) → publish handler writes an integration
  event to the **transactional outbox** → `OutboxPublisher` publishes to RabbitMQ → consumers dedup via
  the **inbox** on `(MessageId, Consumer)`. `MessageId` is derived from the event's snowflake `EventId`
  (stable across republish; never the outbox row id).
- **Integration events** live in `Contracts/AeroTech.Messages/Ordering/IntegrationEvents/V1/`, namespace
  `...IntegrationEvents.V1` (versioned, matching `AsyncCommands/V1` and `Acknowledgments/V1`). All extend
  **`BaseIntegrationEvent`** (envelope: EventId, AggregateId, TimeOfOccurrence, TenantId=1, SchemaVersion,
  Correlation/Causation/Actor, SourceSystem). The envelope is stamped centrally in `OutboxWriter`.
- Renaming or re-versioning a published event changes the MassTransit type/exchange name — a **wire
  contract change**; coordinate cross-service via the handoff ledger.

## Idempotency & concurrency

Servicing operations (reserve, issue, cancel, split, void) take **no client idempotency key**. Each runs
under a **distributed lock** (`IDistributedLock` on `{op}:{orderId}`) with a **server-derived deterministic
key** (`{op}:{orderId}`, or `void:{documentId}`). Order *creation* is the exception — a future
passenger-duplicate-on-same-flight guard covers it (a lock can't, there's no id yet). Provider-facing keys
(`reserve-hold:{taskId}` etc.) are derived internally and must stay.

## API — controllers & channels

Per-aggregate `Controllers/` folder, **one controller per channel**, route `{Channel}/v{version}/{Resource}`,
`[Tags("{Channel}")]`:

| Channel | Audience |
|---|---|
| `Backoffice` | airline's internal admin panel — agents: management, servicing, reports |
| `OTA` | public API for travel agencies (resource: `Bookings`) |
| `Internal` | debug / test / maintenance of the service itself — NOT a production sell path (binds commands raw) |
| `Service` | service-to-service sync HTTP (future) |

Real servicing operations belong on `Backoffice`; `Internal` mirrors them as a maintenance escape hatch.

## Error handling

Throw via **`ExceptionFactory`** (Domain `_Shared/Resources/`) — never `new BusinessException("...")` inline.
Codes start at **2000** (Ordering's block); messages live in `ExceptionMessages`. Each factory method sets a
REST-accurate `HttpStatus` (404 not-found, 409 state-conflict, 422 rule-violation, 500 system, 502 upstream
provider). `ExceptionHandlingMiddleware` surfaces both `Code` and `HttpStatus` in the response body. This
mirrors StoredValue's Framework pattern.

## Code style

- **No code comments** unless explicitly requested (memory/docs are exempt).
- **No hardcoded parametric config** — retries, timeouts, batch sizes, thresholds, endpoints, credentials
  must be configurable and fail-fast when unset (both siblings fail-fast; do not add `?? default` fallbacks).
- When porting from legacy, **review every check — don't copy verbatim**; flag concerns. Ask before
  changing an entity's fields or a field mapping.

## Build & dev workflow

- The running `ServiceHost` **locks `bin/`**. To build while it runs, output elsewhere:
  `dotnet build AeroTech.Ordering.sln -o "$TEMP/ordbuild"`. `dotnet ef` can't redirect output — stop the app
  for migrations.
- **Database:** `DotAirOrderNew` on `localhost\SQLEXPRESS`, one DB with `Order` / `ReadModel` /
  `ReferenceData` schemas. Migration history tables: `dbo.__CommandsMigrationHistory`,
  `dbo.__QueriesMigrationHistory`, `dbo.__ReferenceDataMigrationHistory`.
- "**Update database**" = apply pending EF migrations to the local dev DB (no confirmation needed).
- **Infra:** RabbitMQ + Redis on `localhost` (dev: `guest/guest`).
- **Tests are paused** by standing decision — do not add or run tests unless asked.
- **Design docs** live in `docs/` (`Domain-Model.md`, `P1–P4` phase designs, `Solution-Analysis-*.md`,
  `DomainAuditRemediationPlan.md`) — keep the "known gaps" sections current as behavior lands.

## Cross-service handoffs

Cross-service coordination uses the shared ledger at `E:\Projects\DotAir\handoffs\` (its README has the
full protocol). That folder is neutral ground — read and write handoff files there freely, but never
write code in another service's repo.

- **As owner:** on session start, scan the ledger's open-items index for items whose **Owner** is
  Ordering. Implement them in this repo, note it in the item's Log, set Status: Done.
- **As requester:** to need a change from another service, copy `TEMPLATE.md` to a new
  `OR-NNN-short-slug.md`, fill in an implementation-ready spec + verification checklist, add a row to
  the index, set Direction `Ordering → <Owner>` and Status: Requested.
- **To verify:** once an owner reports Done, read their repo read-only against the checklist and set
  Status: Verified (or Blocked with the gap).
