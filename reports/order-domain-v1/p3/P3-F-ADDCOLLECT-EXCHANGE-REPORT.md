# P3-F Monetary Exchange — AddCollect Capability Bundle

Implementation report for add-collect exchange and reissue.

Companion documents:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog, now carrying `ICC-P3-EXCHANGE-FUNDING`.
* [P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md](P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md) — the partially-used even reissue bundle.
* [P3-F-FREEZE-GATE-CORRECTION-REPORT.md](P3-F-FREEZE-GATE-CORRECTION-REPORT.md) — the contract and recovery correction that preceded this bundle.

---

## 1. Implementation summary

`ChangeMonetaryOutcome.AddCollect` is now a supported exchange capability over both frozen exchange shapes,
fully unused and partially used. Nothing about the even reissue changed.

* AirPrice remains the sole pricing authority. The accepted result must carry an explicit
  `AcceptedAddCollect(Amount, CurrencyId)`; Ordering preserves it exactly and derives nothing.
* A new Ordering-owned funding port expresses guarantee, capture and release, each with its own read-back and
  its own stable operation key.
* The accepted exchange plan carries durable funding evidence, so every stage survives a crash.
* The document host is never dispatched without confirmed funding assurance.
* Once the document host confirms the successor, a later money problem is recorded as an economic exception
  requiring reconciliation. It never fakes a rollback of the reissue.
* Provider-neutral monetary and funding state is exposed through the existing servicing projection.

---

## 2. Selected funding semantic model and rationale

**Two-stage guarantee then capture**, with an independent release.

```text
guarantee the exact amount  ->  reissue the document  ->  capture
                            ->  release, if the exchange dies before the document is confirmed
```

Three reasons, in order of weight.

1. **The shared contracts already anticipate it.** `Contracts/AeroTech.Messages/JetPay/` carries
   `RequiredGuarantee.AuthorizedBeforeIssuance` and `PaidBeforeIssuance`, a `PaymentIntentStatus` running
   `Guaranteed → CommittedForIssuance → Capturing → Paid` with `PaidUnapplied` and `Exception` beside it,
   `InstructionType.Issuance` and `DocumentOutcome`, and the events `PaymentIntentGuaranteed` and
   `PaymentCompleted`. The staging is aligned with an existing design rather than invented for this bundle.
2. **A guarantee is reversible; a collection is not.** One-step collection would need a compensating refund,
   and refund is explicitly out of scope here. Choosing it would have meant either building a refund path
   this bundle was told not to build, or accepting a real overcollection risk.
3. **It keeps the exposure window to the document call itself.** The money is committed only across the one
   step that cannot be undone.

**Stage placement: the guarantee is dispatched after document eligibility and before the inventory mutation.**
The inventory mutation is the first step this bundle cannot reverse within its own scope, whereas the
guarantee is reversible by design. Guaranteeing first gives every definite later failure one defined release
path, and never mutates capacity for an exchange the customer cannot fund. Guaranteeing after inventory would
strand a confirmed capacity change whenever funding is refused, which is exactly the silent partial success
case F forbids.

A note on transport. The JetPay vocabulary is asynchronous, instructions out through the outbox and facts back
as integration events, while this port is synchronous like the frozen inventory and document rails. The two
reconcile because recovery is a read-back keyed by the operation identity Ordering supplies, which an adapter
can implement over either transport. Whether Payment exposes such a read-back is unverified and is recorded
as the first integration blocker rather than assumed.

---

## 3. Exact orchestration sequence

```text
1.  quote                                   side-effect free, no claim, no plan
2.  accept the AirPrice add-collect result
3.  validate the monetary shape             fails closed on malformed evidence
4.  require a funding method reference      fails closed if absent
5.  persist the accepted plan               amount, currency, funding method, every operation key
6.  document eligibility                    observational
7.  funding guarantee                       recover-first on resume
8.  inventory mutation                      plan-level, only Replaced services
9.  document exchange                       recover-first, never without funding assurance
10. persist the document confirmation       with raw successor evidence
11. funding capture                         recover-first on resume
12. single local finalization transaction
```

Release paths, each under its own key and recover-first:

| Trigger | Action |
| --- | --- |
| Inventory rejected | release the guarantee, then settle terminally |
| Document rejected | release the guarantee, then reconcile as the frozen rail already does |
| Guarantee rejected | nothing to release, settle terminally, no capacity touched |
| Capture unresolved or refused | no release, the money is owed, reconcile |

For an even reissue steps 4, 7, 11 and every release path are skipped and the sequence is byte-for-byte the
frozen one.

---

## 4. Domain and application changes

| Change | Location |
| --- | --- |
| `AcceptedAddCollect(Amount, CurrencyId)` | `Domain/OrderAggregate/AcceptedSource/Exchange/` |
| `AcceptedExchange` and `ExchangeQuote` carry an optional `AddCollect` | same folder |
| Outcome-aware `DeferralReason`, new `RequiresFunding`, new `EnsureMonetaryOutcomeIsWellFormed` | `Domain/OrderAggregate/Policies/ExchangePricingPolicy.cs` |
| `IExchangeFundingPort` plus its six operations and seven records | `Domain/Ports/ExchangeFunding/` |
| `ExchangeFundingState`, `ExchangeFundingReleaseReason` | `Contracts/AeroTech.Messages/Ordering/Enums/` |
| Durable funding evidence and ten derived readings on the plan | `Domain/Servicing/Plans/AcceptedExchangePlan.cs` |
| Three funding recorders on the plan store contract | `Domain/Servicing/Plans/Contracts/` |
| Funding guarantee, capture and release stages, plus the reorder | `Application/.../Exchange/ExchangeService.cs` |
| `FundingMethodRef` on the execution and the command | `Application/.../Exchange/`, `Application/.../Commands/AcceptExchange/` |

The even rules in `ExchangePricingPolicy` were moved into a private `EvenDeferralReason` unchanged, so penalty
and fee lines still defer an even outcome while being legitimate in an add-collect one.

Ordering validates the authoritative amount without deriving it: present, strictly positive, in the sale
currency, and equal to `NetCustomerBalance` of the accepted lines. A disagreement is malformed provider
evidence, not an input to a calculation.

Two side findings, both fixed. Exception code 2731 was used twice, once for `ServicingOperationNotFound` and
once in an inline `new BusinessException` in `RestApi/_Shared/IdempotencyKey.cs`; that inline throw also broke
the never-throw-inline rule and now goes through `ExceptionFactory.IdempotencyKeyRequired`. And on the user's
instruction the whole code block moved: all 292 codes were renumbered contiguously from **20001** to **20292**,
so nothing sits outside 20000–29999. `CLAUDE.md` records the new rule. Historical P0–P2 closure reports keep
their original numbers as a record of the past.

---

## 5. Persistence and migration changes

One migration, `20260911104204_P3FAddCollectExchangeFunding`, applied to the dev database. Nine nullable
columns on `Order.AcceptedExchangePlans`:

```text
FundingMethodRef            nvarchar(128)
FundingGuaranteeOutcome     int
FundingGuaranteeReference   nvarchar(128)
FundingGuaranteeDetail      nvarchar(512)
FundingCaptureOutcome       int
FundingCaptureReference     nvarchar(128)
FundingCaptureDetail        nvarchar(512)
FundingReleaseOutcome       int
FundingReleaseDetail        nvarchar(512)
```

No existing column changed, no enum was renumbered, and `Down` drops only the new columns, so the migration is
losslessly reversible and needs no downgrade guard.

The amount and currency are deliberately **not** duplicated as columns. They live in the immutable accepted
plan JSON, which already provides them unambiguously, so the two-condition persistence rule from the earlier
phases is not met. The funding-method reference is persisted because it is required to reproduce the guarantee
request on replay and exists nowhere else.

---

## 6. Port and simulator changes

`DeterministicExchangeFundingAdapter` records one operation per key and replays it for a repeated call, so a
second guarantee or capture under the same key never moves money twice. It distinguishes never-dispatched from
dispatched, keeps `Confirmed` and `Rejected` sticky across read-back while letting `Pending` and `Unknown`
resolve to a configured outcome, and offers throw-before-dispatch and throw-after-dispatch knobs for the crash
boundaries.

`UnconfiguredExchangeFundingProvider` fails closed on all six operations with code 20263 and HTTP 501, and is
what production resolves to today. The existing `IPaymentProvider` and `IFundingCoveragePort` were left
untouched; neither satisfies this contract and neither was bent to appear to.

---

## 7. ICC changes and their paths

All in [reports/order-domain-v1/p3/P3-integration-capability-catalog.md](P3-integration-capability-catalog.md).

* **`ICC-P3-EXCHANGE-FUNDING`** — new, all nineteen headings. Owner, chosen model and rationale, stage
  placement and its reason, request evidence per stage, outcome semantics, per-stage keys, the five dispatch
  states, recover-first, the `WasDispatched` requirement, irreversible-step ordering, the economic-uncertainty
  rule, simulator, contract tests, verification status, five integration blockers each with the required
  capability and why an adapter cannot invent it, known gaps and non-responsibilities.
* **`ICC-P3-EXCHANGE-AIRPRICE`** — add-collect authority rule, the explicit-amount integration blocker, the
  accepted outcome set, and money movement delegated to the funding entry.
* **`ICC-P3-EXCHANGE-INVENTORY`** — the guarantee now precedes the mutation, and a rejected mutation releases
  it.
* **`ICC-P3-EXCHANGE-DOCUMENT`** — the updated step ordering, the no-dispatch-without-assurance rule, and the
  no-release-after-confirmation rule.

No real service integration is claimed as verified anywhere.

---

## 8. API and query projection changes

No new API surface. `ExchangeOutcome`, which the existing `OrderChangeResponse` already returns from the
accept-exchange command, gained five provider-neutral fields: `AddCollectAmount`, `AddCollectCurrencyId`,
`FundingState`, `FundingProviderReference` and `RequiresReconciliation`. Together with the monetary outcome,
document outcome, predecessor and successor lineage and servicing status already present, a backoffice tool
can understand an add-collect exchange without knowing anything about Payment internals.

`ExchangeFundingState` is derived from the stored outcomes, so it is a reading rather than a second source of
truth. Request input gained one optional field, `FundingMethodRef`, on the command and the execution.

Nothing sensitive is exposed. The funding-method reference is an opaque caller-supplied reference and is never
echoed in the outcome; only the provider's own reference is, and case W asserts the funding method does not
appear in it.

---

## 9. Edge-case matrix results

All twenty-four cases pass, as thirty-two test cases in
`tests/AeroTech.Ordering.Persistence.Tests/P3/AddCollectExchangeFlowTests.cs`.

| Case | Result | Case | Result |
| --- | --- | --- | --- |
| A fully unused happy path | Pass | M document unresolved, both outcomes | Pass |
| B partially used happy path | Pass | N document confirmed, save crashes | Pass |
| C exact commercial authority | Pass | O capture unresolved, both outcomes | Pass |
| D malformed money, six shapes | Pass | P capture refused | Pass |
| D missing funding method | Pass | Q completed replay | Pass |
| E stale accepted context | Pass | R repeated A to B to C | Pass |
| F guarantee refused | Pass | S partially used lineage | Pass |
| G guarantee unresolved, both outcomes | Pass | T same funding key | Pass |
| H guarantee confirmed, save crashes | Pass | U different funding key | Pass |
| I inventory refused, guarantee released | Pass | V unconfigured adapter fails closed | Pass |
| J inventory unresolved, both outcomes | Pass | W query observability | Pass |
| K inventory confirmed, save crashes | Pass | X even reissue unchanged | Pass |

Cases T and U are proved in the reusable port contract, `One_operation_key_can_never_consume_another_operations_result`
and `Guaranteeing_the_same_operation_key_twice_never_moves_money_twice`, and again at flow level by the replay
assertions in G, H and Q.

Call counts worth stating: every crash-boundary case performs exactly one guarantee and at most one capture;
case Q performs zero of everything on replay; case I performs one guarantee, one release, zero captures and
zero document calls; cases O and P perform one capture and zero releases, because after a confirmed reissue
the money is owed.

Three obsolete tests were retargeted rather than deleted, since their premise was that a non-even outcome
defers. `ExchangeFlowTests.C4` and the domain deferral theory now use `Refund`, `Residual` and `Mixed`, which
are still deferred, and the domain theory gained positive add-collect coverage.

---

## 10. Focused and final regression counts

| Focused run during implementation | Result |
| --- | --- |
| `AddCollectExchangeFlowTests` | 32 / 32 |
| Funding port contract kit | 17 / 17 |
| All exchange persistence tests | 151 / 151 |

| Final freeze gate | Result |
| --- | --- |
| `dotnet build AeroTech.Ordering.sln` | Succeeded |
| Domain tests | 502 / 502 |
| Persistence tests | 724 / 724 |

No other test project was modified.

---

## 11. BLOCKED_DEVELOPMENT

```text
BLOCKED_DEVELOPMENT:
None.
```

Every Ordering business semantic this bundle needed was resolvable from the brief, the frozen P3 semantics and
the existing code. No business rule was invented.

---

## 12. BLOCKED_INTEGRATION

Five funding blockers, each with the required capability, what is missing or unverified, why an adapter cannot
safely invent it, and what real integration must settle.

**1. Read-back keyed by the caller's operation id.**
Required: given the key Ordering generated, Payment answers whether it ever saw that operation and what the
authoritative outcome is. Missing or unverified: the JetPay contracts describe an asynchronous
instruction-and-fact flow, with no evidence of a query keyed by a caller-supplied operation id. An adapter
cannot invent it, because a client-side record of what was sent is lost exactly when the process crashes,
which is the only moment the answer matters. To verify or add later: a read-back endpoint or a fact stream
that can be replayed by caller key.

**2. Two-stage guarantee then capture.**
Required: protect an exact amount, capture it later against the successor document, release it if the exchange
dies first. Unverified: `RequiredGuarantee.AuthorizedBeforeIssuance` and the
`Guaranteed → CommittedForIssuance → Capturing → Paid` progression strongly suggest this exists in the JetPay
design, but no Ordering-facing operation was verified. An adapter must not simulate a guarantee by capturing
immediately; that silently converts a reversible step into an irreversible one and breaks the release paths.
To verify or add later: the three operations, and whether a guarantee expires.

**3. Same-key idempotency on money operations.**
Required: re-sending a guarantee or capture under an already-used key never moves money twice. Unverified. An
adapter cannot add this on the client side for the same reason as blocker 1. To verify or add later:
server-side idempotency on the caller key, with the original authoritative result returned on a repeat.

**4. Release semantics.**
Required: an independently recoverable release, safe to call once, twice, or after an unknown outcome.
Unverified. An adapter cannot fabricate a release from a capture-only API. To verify or add later: the
operation and its read-back.

**5. Completion after authorization.**
Required: a reliable path to capture a previously guaranteed amount. If Payment cannot guarantee completion
after authorization, then the reconciliation state this bundle persists is the correct terminal representation
and an operator must resolve it. To verify or add later: the completion guarantee, or an explicit operational
procedure for the reconciliation queue.

The four blockers carried forward from the previous bundles are unchanged: AirPrice, inventory and document
host real contracts remain unverified, and nothing in Ordering yet writes
`TicketCouponFinancialStatus.Used` or `TicketCouponControlStatus`. One AirPrice blocker was added, that the
provider must return an explicit authoritative add-collect amount rather than leaving the caller to total the
pricing lines; an adapter that summed the lines would silently make Ordering the pricing authority.

A passing deterministic simulator is not evidence of production readiness for Payment, AirPrice, inventory or
the document host.

---

## 13. Scope confirmation

Not started, and no code exists for any of them:

```text
Refund monetary exchange      not started
Residual value                not started
Mixed monetary outcomes       not started
StoredValue / wallet          not started
EMD exchange                  not started
Ancillary exchange            not started
Involuntary change            not started
DCS ingestion                 not started
Production Payment adapter    not started
```

`Refund`, `Residual` and `Mixed` remain deferred by `ExchangePricingPolicy.DeferralReason` and are asserted as
such. No payment authorization or capture call exists outside the Ordering-owned funding port and its
deterministic simulator. No penalty settlement, refund or reversal instruction was implemented.
