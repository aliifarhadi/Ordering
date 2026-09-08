# P2-A.1 — Pricing Foundation Correctness Closure

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
**Scope:** only the pricing-foundation correctness gaps found by the independent review of the pushed P2-A code.
P0–P1.2 frozen. P2-B not started. No sibling service inspected or changed.

Evidence: [`P2-A.1-TEST-RUN.txt`](P2-A.1-TEST-RUN.txt) · Entry gate: [`P2-ENTRY-AND-MAPPING.md`](P2-ENTRY-AND-MAPPING.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **116 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **141 passed**, 0 failed (real SQL Server) |
| Total | **257 passed, 0 failed** (baseline 220 → +37) |

Every P2-A decision listed as preserved in the brief is intact: sign convention, non-negative magnitudes,
`CustomerTotal` from `CustomerBalance`, the `OrderChange → OrderPriceChangeSet → OrderPricingLine` chain,
versioned allocation sets, `OwnerAirlineId`, `FinancialSequence`, source-owned commission amount,
`LegacyPricingLineTranslation`, `Surcharge → CarrierSurcharge`, and the drop+add migration strategy.

---

## 2. Defect 1 — `CommitPriceChange` was not exception-atomic

**Was:** the method appended `OrderChange`, incremented `FinancialSequence` and appended the
`OrderPriceChangeSet` *before* validating any line. A rejection on line 2 of 3 left the aggregate carrying a
phantom change, a burned financial sequence and line 1.

**Now:** the commit is split into a pure staging pass and an attach pass.

`StagePriceChange` constructs the `OrderChange`, the `OrderPriceChangeSet` (at `FinancialSequence + 1`, not
assigned to the Order) and every `OrderPricingLine` with its allocation sets, running all validation as it
goes. It mutates nothing on the aggregate. `AttachPriceChange` then adds the change, the change set and all
lines, assigns `FinancialSequence` from the staged set, commits the set, recomputes the caches and advances
`ObligationVersion` if `CustomerTotal` moved.

A rejected mutation therefore leaves `Changes`, `PriceChangeSets`, `PricingLines`, `FinancialSequence`,
`ObligationVersion`, the `OrderAmount` cache and the `Commission` cache byte-identical. No second unit of
work or transaction framework was introduced; ids consumed by failed staging are simply discarded.

Multi-line candidates validate against staged siblings: two reversals of the same original in one candidate
are capped jointly, and a duplicated source occurrence inside one candidate is rejected.

## 3. Defect 2 — reversal provenance was under-specified

`EnsureReversalIsBounded` became `EnsureReversalIsWellFormed` and now additionally requires that a reversal:

- does not target another `Reversal` line (**2772**) — undoing a reversal is an explicit later
  Correction/Adjustment, not a recursive chain;
- carries the original's accepted `ExchangeRate` provenance unchanged when the original had one (**2773**) —
  today's rate cannot be substituted;
- supplies a non-zero `OriginalAmount` when outstanding original value remains (**2774**) — a reversal can no
  longer slip through the sale-currency cap by declaring `OriginalAmount = 0`;
- closes the outstanding original exactly when it closes the outstanding sale value (**2775**) — a full
  reversal copies the remaining magnitudes rather than inventing them.

Both caps (sale and original) are still enforced, now counting reversals staged in the same candidate.
Ordering never derives a partial original-currency amount itself; the accepted decision must supply it.

The legacy cancel/void path in `Order.Termination` was updated accordingly: it uses source-supplied
allocation original values when every allocation has one, and otherwise falls back to the sale magnitude
**only** when the line has no conversion at all (`ExchangeRate is null` and original currency == sale
currency), which is an identity, not an FX calculation. When a converted line lacks a defensible original
breakdown the reversal is now correctly refused instead of silently posting zero.

## 4. Defect 3 — source occurrence identity was conflated

`SourceLineRef` previously carried the occurrence ordinal inside one synthetic string, and persistence
enforced `UNIQUE(OrderId, SourceLineRef)` — which wrongly forbade a later authoritative pricing decision from
referencing the same source line again.

`OccurrenceKey` is now a separate field on `OrderPricingLine`. The AirPrice create path builds
`SourceLineRef` as the stable identity (`offer:traveller:bound:flight:code`) and `OccurrenceKey` as the
occurrence ordinal within the accepted source. Textual tax/fee `Code` is not the occurrence identity.

Uniqueness moved to `(PriceChangeSetId, SourceLineRef, OccurrenceKey)`, filtered on
`SourceLineRef IS NOT NULL`, in both the domain check and a SQL unique index. The same source occurrence
cannot appear twice inside one accepted `PriceChangeSet`; the same reference may legitimately appear in a
later distinct change set. This is a deliberately thin split for the current adapter — P2-B replaces the
Domain-side offer interpretation with the normalized accepted-source boundary.

## 5. Defect 4 — `OrderChange` history did not load

`OrderConfiguration` mapped `Order.Changes` but `OrderRepository.AggregateQuery()` never included it, so a
reloaded Order had an empty commercial change history. `Changes` is now included alongside `PriceChangeSets`.
Pinned by a real SQL test that creates an Order, reloads it, appends a second accepted price change and
reloads again, asserting the `Create`/`AddProduct` changes, the `OriginalSale` change set, contiguous
`FinancialSequence` 1→2 and the recomputed customer total.

## 6. Defect 5 — commission demotion was incomplete

`Order.Create` still wrote `new Commission(args.CommissionRate, 0m)`, letting an arbitrary caller establish
an authoritative agency commission percentage. That line is removed: creation no longer touches commission.

`RecomputeAmountCache` now derives both parts from accepted facts only — the amount is the signed sum of
`ComponentType = Commission` lines, and the rate is taken from the most recent commission line's
source-supplied `UnitPrice`, or stays `0` when no authoritative source supplied one. No rate is ever
back-calculated from amounts. `CommissionRate` remains on `CreateOrderArgs` for old API compatibility but is
no longer trusted pricing truth, and commission still never moves `CustomerTotal`.

## 7. Defect 6 — allocation completeness was incomplete

`OrderPricingAllocationSet` now persists an explicit residual: `ResidualSaleAmount` +
`ResidualSaleCurrencyId` (Complete → 0, Partial → parent − allocated, Unavailable → the whole parent value),
plus `IsFullyAttributed`. No allocation target is invented for the residual and no `Money` type was created.

Original-currency reconciliation was added: when allocations supply original values they must all supply
them (**2777** otherwise — a half-supplied breakdown cannot be completed locally), must use the parent's
original currency, must reconcile exactly when `Complete` and must not exceed the parent when `Partial`.
`ResidualOriginalAmount`/`ResidualOriginalCurrencyId` stay `null` when the source supplied no original
breakdown — nothing is fabricated. Sale-currency allocation remains mandatory.

## 8. Persistence

The already-applied `P2PricingFoundation` migration was **not** touched. A new additive migration
`P2A1PricingProvenance` adds `OccurrenceKey` to `OrderPricingLines`, replaces the old
`(OrderId, SourceLineRef)` unique index with the correctly scoped
`(PriceChangeSetId, SourceLineRef, OccurrenceKey)` unique filtered index (restoring a plain `OrderId`
index), and adds the four residual columns to `OrderPricingAllocationSets`. It contains no renames and
reinterprets no existing column value. Applied to `DotAirOrderNew`.

## 9. Legacy wire translation

Unchanged in approach and still applied only at the publish boundary: the domain keeps `Debit = sale`, and
`LegacyPricingLineTranslation` inverts direction and maps `ComponentType` to the old
`OrderPricingLineCategory` for the legacy contracts. Producer contracts are not re-versioned here; that
remains P2-G. Now pinned by explicit tests.

## 10. Two P2-A tests were rewritten, not deleted

`The_same_source_line_cannot_be_accepted_twice` asserted the behaviour this session deliberately changes
(global rejection of a repeated source reference). It is replaced by `Create_records_source_identity_and
_occurrence_separately` plus three occurrence-scoping tests. The `ReversalOf` helper in
`PricingFoundationTests` passed `OriginalAmount = 0`, which rule 2774 now correctly refuses; it supplies the
defensible identity amount instead. Neither change weakened an assertion.

## 11. Tests added

`tests/AeroTech.Ordering.Domain.Tests/P2/PricingCorrectnessTests.cs` (24), extensions to
`PricingFoundationTests.cs`, `tests/AeroTech.Ordering.Persistence.Tests/P2/PricingPersistenceTests.cs` (4)
and `P2/LegacyWireTranslationTests.cs` (9) cover all 28 required cases: atomicity across first-line,
later-line and reversal rejections including the caches and financial sequence; full/partial reversal
magnitudes, provenance preservation, today's-rate refusal, zero-original refusal, both caps, joint capping
and reversal-of-reversal; occurrence identity within and across change sets plus SQL index enforcement;
`Changes`/`PriceChangeSets` reload with correct `FinancialSequence`; commission authority; the four
allocation residual/original-reconciliation cases; and legacy direction and category translation.

---

## 12. Exit gate

| Gate | Status |
|---|---|
| a rejected monetary mutation cannot alter aggregate state | **Met** |
| `FinancialSequence` represents committed `PriceChangeSet`s only | **Met** |
| reversals preserve original monetary/conversion provenance | **Met** |
| reversal-of-reversal is rejected | **Met** |
| source occurrence identity is explicit and correctly scoped | **Met** |
| `OrderChange` history reloads with the aggregate | **Met** |
| caller `CommissionRate` is not authoritative pricing truth | **Met** |
| Partial allocation has explicit residual semantics | **Met** |
| original allocation values reconcile when supplied | **Met** |
| legacy wire compatibility remains translated only at the boundary | **Met** |
| all previous tests remain green | **Met** (257 passed, 0 failed) |

**No remaining P2-A blocker.** P2-A is safe to freeze.

Carried, and out of scope here: the legacy split still moves value as `Transfer` lines on the child order
rather than the design's balanced opposite-direction pair under one `TransferGroupId` (P3); the AirPrice
kind→component interpretation still lives in Domain create code (P2-B moves it into the normalizer and
retains the source kind/code/reference there).
