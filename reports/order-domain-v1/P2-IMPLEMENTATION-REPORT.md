# P2 — Pricing, Fare Construction, Ancillary Catalogue & EMD

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Started:** 2026-09-08
Entry gate: [`audit/p2/P2-ENTRY-AND-MAPPING.md`](audit/p2/P2-ENTRY-AND-MAPPING.md) · Evidence: [`audit/p2/P2-TEST-RUN.txt`](audit/p2/P2-TEST-RUN.txt)

| Sub-phase | Status |
|---|---|
| **P2-A** pricing foundation | **Complete** |
| P2-B accepted source normalization | Not started |
| P2-C AirFareConstruction | Not started |
| P2-D service / item model | Not started |
| P2-E initial sale + AddProduct | Not started |
| P2-F ElectronicMiscDocument | Not started |
| P2-G projections / APIs / events | Not started |
| P2-H verification | Not started |

---

## P2-A — Pricing foundation

**Complete.** The old *Credit-means-sale* polarity is gone from the active model, and every monetary fact
now enters the Order through one immutable, validated, change-set-scoped ledger.

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **92 passed**, 0 failed (69 baseline + 23 new P2-A) |
| `AeroTech.Ordering.Persistence.Tests` | **128 passed**, 0 failed (real SQL Server) |
| Total | **220 passed, 0 failed** — the full P0–P1.2 baseline is intact |

### 1. The polarity replacement

`Direction` is now the only source of sign: `Debit = +1`, `Credit = -1`, magnitudes always non-negative,
`CustomerTotal = Σ signedSale where Effect = CustomerBalance`. The create path that previously wrote every
sale as `Credit` now writes `Debit`.

The published `OrderIssued` / `OrderCancelled` / `OrderDocumentVoided` / `OrderSplit` contracts still carry
the old enum, so the inversion is **translated at the publish adapter** — `LegacyPricingLineTranslation` maps
the new `ComponentType`/`Direction` back to `OrderPricingLineCategory` and the legacy polarity — exactly as
`02` §3.3 requires ("mapped at its adapter, never reused without translation"). No consumer sees a silently
flipped sign, and the wire contract is re-versioned in P2-G, not here.

### 2. New model (all in the one Ordering domain — no `V2` types)

`OrderChange` (stable `ChangeId` per accepted commercial mutation) → `OrderPriceChangeSet` (immutable after
commit, carries `FinancialSequence`, reason, source, `ExpectedCommercialVersion`) → `OrderPricingLine`
(replaced in place) → `OrderPricingAllocationSet` → `OrderPricingAllocation`.

`OrderPricingLine` gained `ComponentType`, `Effect`, `LineRole`, `OriginalAmount`/`SaleAmount` with their own
currencies, `ApplicationLevel`/`Quantity`/`UnitOfMeasure`/`UnitPrice`, `BasisType`/`BasisReferenceId`,
`SourceLineRef`, `TransferGroupId`, `RelatedOperationId`, `CalculationSnapshot`, `TaxDetails`,
`SettlementPartyRef`/`SettlementCategory`. It lost `LineScope`, `LineSubCategory`, `LineReason` (reason moved
up to the change set) and `IsPercentage`.

Eleven enums were added under the existing convention in `Contracts/AeroTech.Messages/Ordering/Enums`, one
per file with `Display` names. No enum was moved.

### 3. Rules now enforced in the domain

`PricingComponentPolicy` implements the complete `02` §3.4 matrix: `Tax + SettlementOnly` is rejected
outright (2751), commission can never touch the customer balance (2752), a discount is `Credit` and a
fare/tax/fee/surcharge/markup/penalty is `Debit` as an original (2754), `Adjustment`/`Other` require a code
(2755), and a settlement-only line requires an explicit party and category (2756).

Reversals require the original line, the opposing direction, identical component/effect/currencies, and are
capped by the outstanding value in **both** currencies (2757–2760). A lineage link alone is not a reversal.

Allocation sets belong to exactly one line, purpose and version, and reconcile per `02` §4.2: `Complete`
must equal the parent exactly, `Partial` may not exceed it, `Unavailable` must be empty — no invented
shares (2761–2766). Allocations are never added to totals.

`SourceLineRef` is a deterministic composite of offer, traveller, bound, flight, code and occurrence, unique
per order in the database, so replaying the same accepted offer is rejected (2769) while two genuine
occurrences of the same tax code stay distinguishable.

### 4. Totals, commission and versions

`OrderAmount` is now a **derived cache** recomputed from the ledger in the same transaction as the mutation,
and it gained `AncillaryTotal` (ProductCharge). `Order.CustomerTotal` is the authoritative derived value.

`Commission` stopped calculating. It was `totalAmount * rate / 100` folded into the order total; it is now a
pure carrier whose amount comes from `ComponentType = Commission` ledger lines, which are `SettlementOnly` by
default and therefore do **not** reduce `CustomerTotal` (`02` §7.8).

`FinancialSequence` was added to `Order`, advancing exactly once per committed `PriceChangeSet`, independent
of `CommercialVersion`. `ObligationVersion` advances only when the committed change actually moves
`CustomerTotal` — a settlement-only line does not touch it. `OwnerAirlineId` was added to `Order` and is
supplied by `IHomeOperatorProvider` at the application boundary, **not** through `CreateOrderArgs`, because
it is trusted operator context and never client input.

### 5. Source classification change to confirm

`AirChargeKind.Surcharge` previously collapsed into `OrderPricingLineCategory.Fee`. It now maps to
`ComponentType.CarrierSurcharge` per `02` §3.2/§7.3 (YQ/YR must not be Fare or Tax). This is a deliberate
mapping correction, not a port of the old behaviour — flagged here for confirmation.

### 6. Persistence

New tables `OrderChanges`, `OrderPriceChangeSets`, `OrderPricingAllocationSets`, `OrderPricingAllocations`;
`OrderPricingLineAllocations` dropped. Unique indexes on `(OrderId, FinancialSequence)`,
`(OrderPricingLineId, Purpose, Version)` and filtered `(OrderId, SourceLineRef)`.

Migration `P2PricingFoundation` was **hand-corrected before it was applied**: EF scaffolded ten
`RenameColumn` operations that matched old and new columns *by CLR type rather than by meaning* — it would
have turned `LineCategory` into `Direction`, `EquivalentCurrencyId` into `ComponentType` and `CurrencyId`
into `BasisType`, silently reinterpreting existing rows as garbage. Each was rewritten as an explicit
`DropColumn` + `AddColumn` pair (and the `Down` section symmetrised) so no value is carried across a
semantic boundary. Applied to `DotAirOrderNew`.

### 7. Legacy paths kept compiling, not extended

`Order.Cancel`, `Order.Termination` and `Order.Split` were migrated onto the new ledger rather than left
behind: cancellation and void now build bounded, allocation-aware `Reversal` lines through
`CommitPriceChange`, which removes the old hand-rolled `IsLineReversed`/`IsAllocationReversed` scan.

**Known deviation carried into P3:** the legacy split still physically moves value between orders. It now
does so as `LineRole = Transfer` lines committed under a `SplitTransfer` change set on the child (which at
least preserves referential integrity and change-set lineage), but `02` §10.3 requires a *balanced pair* of
opposite-direction transfer lines on both orders under one `TransferGroupId`. Split is out of P2 scope; this
is recorded as P3 work.

### 8. Tests added

`tests/AeroTech.Ordering.Domain.Tests/P2/PricingFoundationTests.cs` — 23 cases pinning: sale-is-debit and
the signed customer total; the totals cache; discount polarity in both directions; settlement-only
commission; the tax and commission prohibitions; informational exclusion; settlement party requirement;
non-negative magnitudes; reversal lineage, opposition and outstanding-value capping; source-line duplicate
rejection; financial-sequence advance and change-set immutability; empty change set refusal; obligation
version movement; the three allocation completeness rules; sale-currency coherence; and that allocations
never inflate the customer total.

---

## Scope

P3 was not started. No sibling service was inspected or changed. Enum placement was not reopened. No
`Money`, `CurrencyCode`, ExchangeRate framework, currency service, ROE engine or rounding library was
created — the existing `ExchangeRate` value object at `decimal(28,12)` is reused unchanged. No parallel
`PricingV2` / `OrderV2` model exists. Refund, exchange, void, split, DCS, disruption, group booking, tax
engines and fare-rule engines remain unimplemented.
