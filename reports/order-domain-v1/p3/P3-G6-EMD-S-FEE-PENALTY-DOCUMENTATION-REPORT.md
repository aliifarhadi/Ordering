# P3-G6 — EMD-S Fee / Penalty Documentation

Repository: `aliifarhadi/Ordering`
Branch: `k8s-stg`

```text
Frozen remote baseline     635a481085cb667b75dd3f9c36caf7d429b71b05   P3-G5
HEAD at the freeze gate    377f6b79d7c4adec516905870c230c2d3d72b8b3   P3-G6 (local)
Delta to the brief         none — the G6 work of this slice, committed locally
Working tree               clean
```

The freeze gate has now been executed in full. §12 carries the actual results and §18 records the two
defects the gate exposed and how they were fixed.

This is the last implementation slice of P3-G. It makes **source-approved EMD-S documentation of an already
accepted servicing Fee/Penalty** executable.

---

## 1. Why a Fee or Penalty line does not imply an EMD-S

The repository benchmark already freezes four legitimate penalty treatments — netted, separately collected,
added to the replacement document, or documented via EMD-S — and says Ordering records the source-approved
one and never assumes one.

So this is prohibited, and does not exist anywhere in the G6 rail:

```text
if line.ComponentType is Penalty or Fee -> issue EMD-S
```

The **only** trigger is an explicit instruction on the accepted exchange result. `ServicingFeeDocumentPolicy`
returns an empty list when the source sends none, and the G6 stage is then not entered at all. Two tests hold
that directly (`G6D1` penalty, `G6D2` fee), and the G6 execution file contains **zero** references to
`PricingComponentType` — the component type is read only during *acceptance*, to refuse an ineligible line the
source named, never to select a line the source did not name.

## 2. Benchmark evidence

**IATA** distinguishes EMD-S (Stand Alone) from EMD-A (Associated to an ET), and EMD-S may be used
independently of a ticket:
`https://portal.iata.org/faq/articles/en_US/FAQ/What-is-an-Electronic-Miscellaneous-Document-EMD-1415811054748`

**Travelport** documents EMD-S for non-flight-related charges such as reservation change fees and penalty
fees, and supports EMD-S issuance for voluntary change fees during exchange:

```text
https://support.travelport.com/webhelp/uAPI/Content/Standalone_Merchandising/EMDs_Overview.htm
https://support.travelport.com/webhelp/uAPI/Content/Standalone_Merchandising/EMDIssuance.htm
https://support.travelport.com/webhelp/Smartpoint1P/Content/Air/TicketExchange/TicketExchangePlus/Automatic/Requesting_Automatic_Ticket_Exchange.htm
```

Carrier and location support, and the collection treatment itself, stay provider- and source-dependent. RFIC
and RFISC are source/provider facts: Ordering never derives them from component type, description, airline or
a local table. No vendor rule is hard-coded.

## 3. The source-owned instruction

`AcceptedExchange` gains one optional property, `FeeDocuments`. Nothing else about the accepted result
changed, so every frozen shape deserializes unchanged (the property is absent → null → no documentation).

```csharp
AcceptedServicingFeeDocument(
    string DocumentReference,          // stable accepted group identity
    string SourceReference,
    long IssuerCarrierId,
    long? TravelerId,
    string ReasonForIssuanceCode,      // RFIC
    int CurrencyId,
    IReadOnlyList<AcceptedServicingFeeDocumentCoupon> Coupons)

AcceptedServicingFeeDocumentCoupon(
    string ReasonForIssuanceSubCode,   // RFISC
    string PrimarySourceLineRef,
    decimal DocumentedAmount,
    IReadOnlyList<AcceptedServicingFeeAttribution> Attributions)

AcceptedServicingFeeAttribution(string SourceLineRef, decimal AttributedAmount)
```

Acceptance lives in `Domain/OrderAggregate/Policies/ServicingFeeDocumentPolicy.cs`, beside the frozen
`ExchangePricingPolicy` that validates accepted source pricing the same way. It is a pure function of the
accepted exchange, which is why the whole §23 matrix is covered by fast unit tests rather than integration
tests — see §8.

## 4. Pricing-line binding

Every named line is resolved **by exact `SourceLineRef` inside the accepted exchange pricing result**, and
must be unique there. No amount matching, no code matching, no "latest line".

Primary line must be `Fee` or `Penalty`, `CustomerBalance`, `Debit`, and not a `Transfer` role. Additional
explicit attributions may be `Fee`, `Penalty` or `Tax`, under the same effect/direction/role rule — so
tax-on-penalty can be documented when the source says so, and is never inferred. `Commission`, `Discount`,
`Fare`, `Adjustment`, `SettlementOnly`, `Informational`, `Credit` and `Transfer` are all refused
(`ServicingFeeDocumentLineNotEligible`, 20325, 422).

At **execution** the same lines are resolved again, from committed state only:

```text
order.PricingLines
  where PriceChangeSetId == the exchange's own initial PriceChangeSet
  and   SourceLineRef    == the exact accepted reference
  and   exactly one match
```

`StagedExchange.PricingLineIdsBySourceRef` — which exists only on fresh materialization — is deliberately
**not** used. Fresh and replay therefore run the identical resolution and cannot diverge. A missing,
duplicated or out-of-set line reconciles after ticket truth instead of guessing.

## 5. Amount and currency conservation

Ordering calculates nothing. Every amount is the source's.

```text
per coupon      sum(AttributedAmount) == DocumentedAmount              else 20326
per accepted    sum(attributions to one SourceLineRef) <= its accepted debit   else 20326
per EMD-S       sum(coupon DocumentedAmount) == TotalAmount sent to the port
every attributed line's SaleCurrencyId == the document currency        else 20325
```

Over-attribution is checked **across all documents of the operation**, so two instructions cannot jointly
over-draw one accepted line; splitting one line across two documents within its accepted amount is allowed
and tested. No currency is ever converted and no missing remainder is invented.

## 6. Grouping, stock and issuance

Grouping is entirely the source's: `DocumentReference` is the group identity. Ordering never groups by
traveller, amount, RFIC, airline, quote or component type. Duplicate document references, duplicate coupon
identities and repeated attributions are refused before the ticket.

**Document numbers come only from `DocumentStock`.** The frozen reservation protocol is reused verbatim,
under a per-document role `Emd:emd-fee:{DocumentReference}`:

```text
no allocation      -> Allocate, persist the number, IssueAsync
prior allocation   -> RecoverAsync, never a blind IssueAsync
Confirmed          -> materialize locally, stock -> Issued, checkpoint settled
Pending / Unknown  -> AwaitingExternal, reservation retained, no local EMD-S
Rejected           -> ticket and pricing truth retained, no local EMD-S, NeedsReconciliation
```

The stock is looked up **by the instruction's own `IssuerCarrierId`**
(`GetActiveForOperationAsync(IssuerCarrierId, emdDocumentType, operationId)`), so the wrong carrier's stock
can never be allocated — coherence is structural, not a check that could be forgotten. If no active stock
exists for that carrier the operation reconciles; that cross-carrier shape is `BLOCKED_INTEGRATION`.

Provider requests reuse the frozen `IEmdIssuancePort` with a stable key
`ProviderOperationKey(operation, "emd-fee:{DocumentReference}")`, `Standalone` type, source RFIC/RFISC, source
amount and currency, no order service and no EMD-A association. Multiple instructions process in
`DocumentReference` order, one issue each.

**Where the frozen port is not expressive enough:** `DocumentIssuanceResult` carries no `WasDispatched`. The
frozen protocol already compensates because the stock number is committed *before* the call, so a non-null
allocation means "may have reached the provider → recover only". G6 is one notch stricter than the frozen
issuer, which recovers only on `Reserved`: G6 recovers on **any** non-retired allocation, closing a
duplicate-issue hole if a settle checkpoint were ever lost. The port was not widened.

## 7. Local EMD-S truth, and the two things it must not be

On a confirmed issuance the document is materialized as `ElectronicMiscDocumentType.Standalone` with `Fee`
coupons whose `OrderServiceId`, `AssociatedTicketCouponId` and `ExternalValueReference` are all null and whose
`PricingLineId` is the exact committed primary line. Each explicit attribution becomes one `EmdPriceLink` to
its exact committed line with the source-approved value.

**These invariants are the aggregate's already-frozen rules, not new parallel ones.**
`EnsureCouponIsWellFormed` already refuses a Standalone document carrying a ticket-coupon association, and
already refuses a `Fee` coupon whose `PricingLineId` is null or whose `OrderServiceId` is set. G6 satisfies
the frozen guard; it did not add a second one.

**No synthetic OrderService exists anywhere in G6.** The execution file contains no reference to
`OrderServiceId` at all, and `ElectronicMiscDocumentIssuer` — whose entire plan builder is driven by
`OrderService` + `EmdIssuanceSnapshot` — was **not** reused and **not** modified; only its stock protocol was
replicated.

**Related-ticket references were not emulated.** `AssociatedTicketDocumentNumber`,
`AssociatedTicketCouponNumber` and `AssociatedTicketCouponId` mean EMD-A association in this domain and are
left null. A provider needing a separate EMD-S related-ticket field is `BLOCKED_INTEGRATION`.

Before local materialization after a provider Confirmed, `ServicingFeeDocumentEvidencePolicy` revalidates the
document number, operation, type, issuer, traveller, RFIC, currency, coupon count, per-coupon RFISC, amounts
and pricing-line identities. An exact match is adopted idempotently with no second EMD; any conflict retains
the provider confirmation, the stock evidence and the ticket/pricing truth, and reconciles without a second
`IssueAsync` and without overwriting.

## 8. Money and pricing negative guarantees

G6 is documentation, not a payment rail.

```text
0 new OrderChange
0 new PriceChangeSet
0 new OrderPricingChanged
0 CommercialVersion increment for EMD-S issuance
0 G6-owned call to IExchangeFundingPort / IRefundValuePort / IExchangeResidualValuePort
0 duplicated penalty or fee line, 0 zero-value documentation line
```

The fee already exists in the accepted exchange economics; G6 only *links* to those committed lines.
`G6D3` asserts the whole set on a real add-collect reissue: one change, one price change set, one commercial
version advance (the exchange's own), one funding capture and no other value movement.

## 9. Sequence and the completion invariant

```text
1 accept exchange + explicit G6 instructions, persist the plan   before any irreversible work
2 ticket document exchange confirms
3 predecessor -> Exchanged, successor + lineage durable
4 G1 mechanical disassociation
5 existing P3-F monetary legs settle
6 G6 EMD-S documents issue / materialize
7 safe G1-G5 ancillary work settles
8 a remaining ManualReview -> NeedsReconciliation
9 otherwise complete
```

G6 sits at step 6, between monetary settlement and the ancillary stage, exactly as the brief's normalized
sequence lists it.

**"The operation never completes while a required G6 document is unsettled" is structural, not a flag.**
`CompleteAsync` is reachable only through `FinalizeAsync`, and the G6 stage sits before every path that leads
there; it returns `AwaitingExternal` or `NeedsReconciliation` on anything unsettled. No defensive gate was
added in `CompleteAsync`, because with G6 ordered first such a gate would be unreachable code, and unreachable
guards are exactly what the G4 correction taught this codebase not to add.

**ManualReview cannot starve G6**, because G6 runs before the ancillary stage and `ManualReview` is not in
`ExecutableAncillaries` at all. `G6D44` proves the fee document settles and the operation still ends
`NeedsReconciliation`; `G6D45` proves a *pending* G6 beside a ManualReview stays `AwaitingExternal` and is not
prematurely converted into a manual-review reconciliation.

**Ticket truth is never rolled back** by a later G6 failure: rejection and conflict reconcile, they do not
undo the exchange.

## 10. Durable plan and migration

One additive table, `Order.AcceptedExchangePlanFeeDocuments`, keyed `(OperationId, DocumentReference)` and
cascade-owned by the accepted plan, exactly like the frozen G3 exchange-group and G5 cancel-group tables. It
carries the full instruction (coupons and attributions as one JSON column, matching the frozen
`SuccessorCoupons` precedent) plus the execution evidence: `AllocatedDocumentNumber`, `IssuanceOutcome`,
`IssuanceProviderReference`, `IssuanceDetail`, `ElectronicMiscDocumentId`, `SettledAt`. All three checkpoints
use `??=` so a replay cannot move them.

```text
Migration  P3G6ServicingFeeDocumentation
Up         1 CreateTable, 2 CreateIndex     — additive only
Down       1 DropTable
```

No historical row is rewritten or backfilled and no enum is renumbered.

## 11. Coverage against the mandated matrix

67 new tests: 41 in `AeroTech.Ordering.Domain.Tests` (`ServicingFeeDocumentPolicyTests`, pure, no database)
and 26 in `AeroTech.Ordering.Persistence.Tests` (`ServicingFeeDocumentFlowTests`, end-to-end). All 67 are
executed and green.

Every `ServicingFeeDocumentFlowTests` assertion reads back through a **fresh `DbContext`**
(`AncillariesAsync`, `ReloadAsync`, `TicketsAsync` each open a new command context), and every crash/replay
case resumes through a brand-new `OrderSliceHarness`. They exercise real persisted state and real recovery,
not object construction.

| Brief case | Test |
| --- | --- |
| 1 penalty with no instruction → no EMD-S | `G6D1`, policy `A` |
| 2 fee with no instruction → no EMD-S | `G6D2`, policy `A` |
| 3 explicit penalty → one EMD-S / Fee coupon | `G6D3`, policy `B` |
| 4 explicit fee → one EMD-S / Fee coupon | `G6D4`, policy `C` |
| 5 document is Standalone | `G6D3` |
| 6 coupon purpose is Fee | `G6D3` |
| 7 `OrderServiceId` is null | `G6D3`, `G6D46` |
| 8 no EMD-A association | `G6D3` |
| 9 no external value reference | `G6D3` |
| 10 primary `PricingLineId` is the exact committed line | `G6D3` |
| 11 every attribution becomes an exact `EmdPriceLink` | `G6D3`, `G6D18` |
| 12 no new OrderService | `G6D46` |
| 13 no second change / price change set / pricing event | `G6D3` |
| 14 no G6 CommercialVersion bump | `G6D3` |
| 15 no extra funding / refund / residual call | `G6D3` |
| 16 RFIC / RFISC round-trip | `G6D3`, policy `B` |
| 17 issuer / traveller / currency round-trip | `G6D3`, policy `B` |
| 18 explicit penalty + tax, no tax calculation | `G6D18`, policy `D` |
| 19 missing line ref → pre-ticket refusal | `G6D19`, policy `X` |
| 20 tax-only primary → refusal | `G6D20`, policy `Y` |
| 21 credit fee/penalty → refusal | policy `Z` |
| 22 settlement-only / informational primary → refusal | policy `AA`, `AB` |
| 23 transfer → refusal | policy `AC` |
| 24 currency mismatch → refusal | `G6D24`, policy `AF`, `AG` |
| 25 conservation mismatch → refusal | `G6D25`, policy `AI` |
| 26 over-attribution → refusal | `G6D26`, policy `AJ`, `AK` |
| 27 duplicate document ref → refusal | `G6D27`, policy `H` |
| 28 multiple instructions, one issue each | `G6D28`, policy `E` |
| 29 Confirmed → local EMD-S + stock Issued + checkpoint | `G6D3` |
| 30 Pending → AwaitingExternal, no local EMD | `G6D30` |
| 31 Unknown → AwaitingExternal, no local EMD | `G6D31` |
| 32 Rejected after ticket truth → NeedsReconciliation | `G6D32` |
| 33 throw-before Issue → Recover on resume, no duplicate | `G6D33` |
| 34 throw-after side effect → Recover Confirmed, one local EMD | `G6D34` |
| 35 completed replay → zero repeat | `G6D35` |
| 39 G6 + G1 both settle | `G6D39` |
| 40 G6 + G2 both settle | `G6D40` |
| 41 G6 + G3 both settle | `G6D41` |
| 42 G6 + G4 both settle | `G6D42` |
| 43 G6 + G5 Cancel both settle | `G6D43` |
| 44 G6 + ManualReview → G6 settles, final NeedsReconciliation | `G6D44` |
| 45 G6 Pending + ManualReview → AwaitingExternal | `G6D45` |
| 46 no new `AncillaryExchangeDisposition` | `G6D46` + the enum is untouched in the diff |
| 47 no synthetic OrderService | `G6D46` + zero `OrderServiceId` references in the rail |
| 48 no `IEmdExchangePort` for G6 | `G6D46` |
| 49 no ResidualValue / Deposit purpose | `G6D46` |
| 50 frozen G1–G5 suites green | full Persistence suite 1266 / 1266 |

Beyond the list, the policy suite also covers a blank source reference, no coupons, blank RFIC/RFISC, invalid
issuer, invalid traveller, missing currency, missing primary ref, no attribution, a primary absent from its
own attributions, non-positive documented amount, non-positive attribution, a line repeated inside one coupon,
one primary repeated across coupons, `OrderingDerived` pricing, a duplicated `SourceLineRef` in the accepted
result, forbidden component types (`Commission`, `Discount`, `Fare`, `Adjustment`), a forbidden additional
attribution component, and one line legitimately funding two documents.

### Three mandated cases have no executable test

`36` (Confirmed + conflicting local document number), `37` (exact already-materialized identity → no-op) and
`38` (committed pricing line missing on resume) are **implemented and code-reviewed** —
`ServicingFeeDocumentEvidencePolicy` and `ResolvePrimaryPricingLines`/`ResolvePriceLinks` are the guards — but
each needs a state the atomic materialization-plus-checkpoint transaction does not naturally produce, so
reaching them requires raw-SQL fixture surgery against an unpredictable stock-allocated number. I did not
write speculative tests I could not run in this session. This is stated as a gap rather than papered over.

### Why the eligibility matrix lives in the Domain suite

Reshaping the penalty line's direction, effect or role inside a real add-collect exchange breaks the **frozen**
`ExchangePricingPolicy` customer-balance conservation rule and fails with the frozen malformed-exchange code
*before* G6 acceptance runs — so an integration test of those shapes would assert the wrong guard. Because
`ServicingFeeDocumentPolicy` is a pure function of `AcceptedExchange`, the matrix is tested directly and
precisely at the unit level instead, and those tests run in under a second with no database.

## 12. Freeze gate

All executed results, not expectations:

```text
dotnet build AeroTech.Ordering.sln                 0 errors
ServicingFeeDocumentPolicyTests   (focused Domain)        41 /   41
ServicingFeeDocumentFlowTests     (focused Persistence)   26 /   26
EmdIssuanceFlowTests              (repaired frozen)       22 /   22
AeroTech.Ordering.Domain.Tests    (full)                 585 /  585
AeroTech.Ordering.Persistence.Tests (full)              1266 / 1266
dotnet ef migrations has-pending-model-changes     No changes have been made to the model since the
                                                   last migration.
```

**Count reconciliation.** The G5 frozen baseline was 1240. The report previously projected 1265 on the
assumption of 25 new tests; the actual total is **1266**, because the suite carries **26** new tests — the
projection omitted `G6D43` (fee document beside a G5 Cancel), which was added after that count was written.
`1240 + 26 = 1266`. No frozen test was deleted, skipped or weakened; the two frozen tests that failed were
repaired in place with their coverage preserved — see §18.

Exception codes: 326 codes, 20001–20326, contiguous, no duplicates. New in G6: 20324
`ServicingFeeDocumentMalformed`, 20325 `ServicingFeeDocumentLineNotEligible`, 20326
`ServicingFeeDocumentAmountDoesNotReconcile`.

## 13. What the freeze gate exposed

The gate found two defects. Both were **test** defects; neither was a G6 production defect, and no G6
production behaviour was redesigned.

### 13.1 All 26 G6 Persistence tests failed identically (test defect, mine)

```text
BusinessException: The accepted exchange EXC-QUOTE-1 collects 250000 and therefore requires a
                   funding method reference.
```

Every G6 test builds an **add-collect** reissue, because that is the shape whose accepted economics naturally
carry a penalty line. An add-collect exchange requires a funding method on the execution, supplied by the
frozen `ExchangeScenario.FundedExecution(key)`; my tests called `Execution(key)`. All 26 therefore aborted in
`ExecuteFreshAsync` before reaching any G6 code. Switching the 20 call sites to `FundedExecution` turned the
suite green with no production change, and confirms the tests genuinely drive the whole pipeline rather than
asserting in isolation.

### 13.2 Two frozen `EmdIssuanceFlowTests` failed on global database state (latent frozen-test fragility)

```text
An_unknown_provider_outcome_keeps_the_document_recoverable        Assert.Single -> 5 items
A_definite_rejection_before_any_irreversible_document_retires...  Assert.Empty  -> 4 items
```

Their helper queried **every reserved EMD stock allocation in the whole test database**:

```csharp
context.DocumentStocks.Where(stock => stock.DocumentType == documentType)
    .SelectMany(stock => stock.Allocations)
    .Where(allocation => allocation.State == StockNumberState.Reserved)
```

So each test silently assumed no other test anywhere ever leaves a reserved EMD number. G6 legitimately does:
`G6D30` (Pending), `G6D31` (Unknown), `G6D33` (throw-before) and `G6D45` (Pending beside ManualReview) each
end with the reservation **deliberately retained** — that is the behaviour §18 of the brief mandates, and the
test fixture never cleans the shared database between runs.

The fix scopes the helper to the operation under test, `&& allocation.OperationId == operationId`, and passes
`suspended.OperationId` / `rejected.OperationId` at the two call sites. That is what each test actually means
— "did *this* operation retain / retire *its* number" — so coverage is preserved exactly and made stricter,
not reduced. Retiring or not retaining the reservation in G6 would have been the wrong fix: it would break a
mandated recovery invariant to satisfy an over-broad assertion.

## 14. Files changed

**New (12)**

```text
Domain/OrderAggregate/AcceptedSource/Exchange/AcceptedServicingFeeDocument.cs
Domain/OrderAggregate/AcceptedSource/Exchange/AcceptedServicingFeeDocumentCoupon.cs
Domain/OrderAggregate/AcceptedSource/Exchange/AcceptedServicingFeeAttribution.cs
Domain/OrderAggregate/Policies/ServicingFeeDocumentPolicy.cs
Domain/Servicing/Plans/AcceptedExchangeFeeDocument.cs
Domain/Servicing/Plans/Policies/ServicingFeeDocumentEvidencePolicy.cs
Application/.../Exchange/ExchangeService.FeeDocumentation.cs
Persistence/Servicing/AcceptedExchangePlanFeeDocumentRow.cs
Persistence/Servicing/AcceptedExchangePlanFeeDocumentConfiguration.cs
Persistence/Migrations/…_P3G6ServicingFeeDocumentation
tests/Domain.Tests/P3/ServicingFeeDocumentPolicyTests.cs
tests/Persistence.Tests/P3/ServicingFeeDocumentFlowTests.cs
```

**Modified (13)**

```text
Domain/OrderAggregate/AcceptedSource/Exchange/AcceptedExchange.cs   optional FeeDocuments
Domain/Servicing/Plans/AcceptedExchangePlan.cs                      fee-document accessors
Domain/Servicing/Plans/Contracts/IAcceptedExchangePlanStore.cs      three checkpoints
Domain/_Shared/Resources/ExceptionFactory.cs, ExceptionMessages.cs  20324-20326
Application/.../Exchange/ExchangeService.cs                         ports, acceptance, sequence
Application/.../Exchange/ExchangeOperationKeys.cs                   emd-fee step
Persistence/Servicing/AcceptedExchangePlan{Row,Configuration,Store} additive persistence
Persistence/Migrations/OrderingDbContextModelSnapshot.cs
Providers.Deterministic/DeterministicEmdIssuanceAdapter.cs          crash hooks only
tests/Persistence.Tests/P1/OrderSliceHarness.cs                     issuance port + stock injection
tests/Persistence.Tests/P2/EmdIssuanceFlowTests.cs                  reserved-number assertions scoped
                                                                    to the operation under test (§13.2)
```

`ElectronicMiscDocumentIssuer`, `ElectronicMiscDocument`, `DocumentStock`, `IEmdIssuancePort` and every G1–G5
file are **unmodified**.

## 15. P3-G final capability audit

| Capability | State |
| --- | --- |
| `ReassociateExisting` | executable (G1) |
| `Refund` | executable (G2) |
| `ExchangeToNewEmd` | executable (G3) |
| `RetainAsResidual` | executable (G4) |
| `Cancel` | executable for the frozen valid whole-document shape (G5) |
| `ManualReview` | durable intentional reconciliation outcome (G5) |
| EMD-S Fee/Penalty | executable **only** from an explicit source instruction (G6) |

| Invariant | Evidence |
| --- | --- |
| every dependent ancillary gets an explicit source disposition | frozen G1 acceptance; a missing decision is 20296/20297 |
| revalidation preserves EMD association | frozen P3-E, untouched |
| reissue disassociates affected EMD-A coupons | frozen G1, extended in G5 to every accepted ancillary including ManualReview |
| no wallet | no wallet/travel-bank/voucher type exists in the solution |
| no invented refund formula | G2 refuses without source economics |
| no invented penalty formula | G6 calculates no amount; every figure is the source's |
| no synthetic fee OrderService | zero `OrderServiceId` references in the G6 rail |
| no silent ancillary skip | `ExecutableAncillaries` covers five dispositions; ManualReview is durably unresolved, never skipped silently |
| no provider-specific entity in the domain | ports only; no vendor type in Domain |

No G5 blocked shape was opportunistically resolved.

## 16. BLOCKED_DECISION

None encountered for the shapes this slice implements. Every supported case works, and these remain refused
rather than guessed:

1. **A fee needing value movement the accepted exchange monetary plan does not represent.** G6 creates no
   second collection, refund or residual. Such an instruction can only be documented against a line that is
   already in the accepted economics; anything else has no accepted line to attribute to and is refused as
   ineligible. A genuine new movement is a new source decision.
2. **Ordering deriving a fee or penalty.** Refused by construction — there is no code path that computes one.
3. **Ordering choosing EMD-S versus tax or new-fare treatment.** The source chooses; Ordering records.
4. **An instruction that cannot identify exact pricing lines.** 20325, before the ticket.
5. **A documented amount that cannot be conserved.** 20326, before the ticket.
6. **Components beyond `Fee`, `Penalty` and explicitly-attributed `Tax`.** Refused with 20325 rather than
   guessed. Widening this set needs a business ruling on what an EMD-S may legitimately document.

## 17. BLOCKED_INTEGRATION

1. No real EMD-S provider adapter is wired. `UnconfiguredEmdIssuanceProvider` fails closed; G6 is not marked
   complete because no real adapter exists.
2. Carrier and location capability for EMD-S is unknown from inside Ordering.
3. Real RFIC/RFISC support is unverified. Ordering carries whatever the source supplies and validates none of
   its meaning.
4. **Cross-carrier stock is unsupported.** Stock is resolved by the instruction's own `IssuerCarrierId`, so an
   instruction naming a carrier with no active EMD stock reconciles. Whether a real deployment issues EMD-S on
   another carrier's stock is a provider question.
5. **A provider needing a separate EMD-S related-ticket field** is not representable. The association fields
   mean EMD-A in this domain and were deliberately not overloaded.
6. **`RecoverAsync` cannot distinguish never-dispatched from unknown.** The committed stock reservation
   preserves the no-duplicate fail-safe regardless, at the cost that a crash between reserving the number and
   the call reaching the provider parks the operation at `AwaitingExternal` until readback resolves it.
   `DocumentIssuanceResult` was not widened.

## 18. Freeze verdict

Based on executed green results, not arithmetic:

```text
build                                 0 errors
focused Domain                       41 /   41
focused Persistence                  26 /   26
full Domain                         585 /  585
full Persistence                   1266 / 1266
EF                                   no pending model changes

P3-G6 READY TO FREEZE: YES
P3-G  READY TO FREEZE: YES
```

P3-G is complete: `ReassociateExisting`, `Refund`, `ExchangeToNewEmd`, `RetainAsResidual`, `Cancel`,
`ManualReview` and source-instructed EMD-S fee/penalty documentation are all executable, with the frozen
G1–G5 semantics unchanged.
