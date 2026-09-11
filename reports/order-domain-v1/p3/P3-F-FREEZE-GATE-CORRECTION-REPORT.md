# P3-F Partially-Used Exchange — Freeze-Gate Contract and Recovery Correction

Correction report. Scope was the reusable integration-contract kit and the two missing partial-use recovery
proofs. The production Exchange flow was not redesigned.

Companion documents:

* [P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md](P3-F-PARTIALLY-USED-EXCHANGE-REPORT.md) — the capability bundle report.
* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog.

---

## 1. Reusable AirPrice Contract

Four semantic defects in `ExchangeQuotePortContract` are fixed. Every one of them was my test being stronger
than the frozen production invariant, so each would have failed a legitimate real adapter.

### Independent quote semantics

Removed the requirement that two independent `QuoteAsync` calls return the same quote id and the same result.
A real adapter may legitimately answer with a different quote id, a different expiry, refreshed source
pricing, or a changed source-authoritative result.

The reusable obligation is now `A_repeated_independent_quote_still_answers_the_request_it_was_asked`: each
answer stays bound to the order, commercial version, predecessor document and requested scope, and never
reports `PricingSource.OrderingDerived`.

Deterministic repeatability moved to `DeterministicExchangeQuotePortTests.The_simulator_repeats_one_deterministic_answer_for_the_same_request`,
where it belongs as a simulator property.

Side-effect freedom moved to the Ordering application boundary, which is what the contract was really trying
to express. `PartiallyUsedExchangeFlowTests.A_partially_used_quote_leaves_no_ordering_visible_trace_however_often_it_is_asked`
quotes a partially-used document three times and proves no servicing operation is created, no
`AcceptedExchangePlan` row exists, no `OrderChange` and no `PriceChangeSet` with `PriceChangeReason.Exchange`
appear, no inventory apply or recovery happens, no document eligibility, exchange or recovery call happens,
no successor ticket appears, the predecessor stays `PartiallyUsed` at the same document version with its
`Used` coupon `Used` and its `Open` coupons `Open`, and commercial version, financial sequence, obligation
version, customer total and service count are all untouched. AirPrice stays free to persist its own quote.

One measurement correction inside that test: the servicing-operation baseline is taken **before the first
quote**, not assumed to be zero. Reserve and issue legitimately own servicing operations on the same order,
so the order already had two before any exchange quote.

### Collection and set comparison changes

| Comparison | Before | After |
| --- | --- | --- |
| `ChangedOrderServiceIds` echoed by the quote | ordered sequence equality | `SetEquals` |
| `ChangedOrderServiceIds` on the accepted plan | ordered sequence equality | `SetEquals` |
| Priced coupon scope | `.Order()` sequence equality | set of `PredecessorCouponNumber` |
| Accepted coupon scope | `.Order()` sequence equality | set of `PredecessorCouponNumber` |
| Replay equivalence of coupons | ordered sequence of ticket-coupon ids | set of `PredecessorCouponNumber` |
| Replay equivalence of pricing lines | ordered sequence of `SourceLineRef` | set of `SourceLineRef` |

No production DTO changed. Production already normalizes the changed scope with `.Order()`, so ordering is
deterministic in practice; it simply is not an obligation a provider contract may impose.

### Historical Used evidence rule

The old assertion banned any pricing line whose `PredecessorCorrelationRef` belonged to a `Used` coupon, and
proved the successor side with string containment on `SourceLineRef`. Both were wrong: the first forbids
legitimate source-authoritative output, the second is not a semantic proof.

The frozen rule is `historical pricing context != successor transferred value`, and it is now expressed as
two obligations:

1. `A_coupon_that_is_only_history_never_becomes_part_of_the_reissue` — no historical coupon appears in
   `quote.Coupons` by coupon number, ticket-coupon id or order-service id, and none carries a `Replaced` or
   `Continued` disposition, so none can receive a successor coupon.
2. `Historical_value_is_never_mechanically_attributed_to_a_successor_coupon` — every
   `SuccessorDocumentPriceLink.SourceLineRef` is resolved to its `AcceptedExchangePricingLine`. That
   resolution is itself asserted, mirroring the frozen production rule behind
   `ExceptionFactory.ExchangeSuccessorAttributionUnresolved`. If the resolved line is a `PricingLineRole.Transfer`
   carrying a `PredecessorCorrelationRef`, that correlation must belong to the actual `Open` exchange scope.
   Unattributed source-explanatory lines may reference historical evidence freely.

The canonical fixture request deliberately carries predecessor pricing evidence for the `Used` coupon too, so
the rule is genuinely exercised rather than vacuously true.

The simulator's simpler behaviour, building transfer lines only from open-scope evidence, is now asserted as a
simulator property in `DeterministicExchangeQuotePortTests.The_simulator_builds_transfer_lines_only_from_open_scope_evidence`
and is no longer a universal AirPrice requirement.

### Even balance rule

Replaced the gross credit-versus-debit equality over all pricing lines with the existing production
definition. The contract now calls `ExchangePricingPolicy.NetCustomerBalance(quote.PricingLines)` directly and
requires zero when the outcome is `Even`. No second balance algorithm was introduced, and non-customer-balance
informational, accounting and source-explanatory lines are no longer required to gross-balance.

Also dropped the assertion that every pricing line is in the sale currency, which is not a frozen invariant.
The frozen currency rule, that successor attribution is in the sale currency, is asserted instead.

---

## 2. Partial-Use Recovery Proof

### Case N — unresolved inventory, replayed

`N_an_unresolved_reservation_change_is_read_back_on_replay_and_never_applied_again`, on a three-bound
predecessor with one `Used`, one `Replaced` and one `Continued` coupon.

| Measure | Result |
| --- | --- |
| Inventory `ApplyAsync` calls | 1 |
| Inventory `RecoverAsync` calls | at least 1, every key equal to the apply's `OperationKey` |
| Second inventory apply | none |
| Document eligibility / exchange / recovery calls | 0 exchange, 0 recovery |
| Operation id across replay | identical |
| Final operation status | `AwaitingExternal` on both attempts |
| Plan reservation outcome | `Pending` or `Unknown` |
| Claim | retained, a second operation returns 20070 |

Coupon states after the replay: coupon 1 `Used`, coupons 2 and 3 `Open`, predecessor `PartiallyUsed` with an
empty exchange record. No successor ticket exists, no ticket has a predecessor link, commercial version is
unchanged, and no exchange `OrderChange` was written. The generic `F9` recovery theory remains green.

### Case O — durable document confirmation across a process boundary

`O_a_durable_document_confirmation_finalizes_in_a_fresh_process_with_no_provider_call`. The existing
crash-boundary technique from `ExchangeCrashBoundaryTests.I1` was reused. No production-only crash switch was
added.

State persisted before the simulated crash:

* `AcceptedExchangePlan` exists with coupons `[2, 3]`, the `Used` coupon 1 absent;
* `IsEligibilityEstablished` true;
* `IsReservationConfirmed` true;
* document exchange outcome `Confirmed` with provider reference `EXCH-PARTIAL-DURABLE`;
* exact raw `SuccessorDocumentIdentity` evidence stored, mapping predecessor 2→1 and 3→2;
* local finalization not yet done, proven by an empty exchange record, a `PartiallyUsed` predecessor and an
  unchanged commercial version.

The first harness is then disposed and a fresh harness with the same caller replays the same operation.

| Fresh-process measure | Result |
| --- | --- |
| Inventory applies | 0 |
| Inventory recoveries | 0 |
| Document exchanges | 0 |
| Document recoveries | 0 |
| Document eligibility calls | 0 |
| AirPrice acceptances | 0 |

Local finalization result: operation `Completed` under the original operation id with provider reference
`EXCH-PARTIAL-DURABLE`, exactly one exchange `OrderChange`, exactly one exchange `PriceChangeSet` carrying
the new financial sequence, exactly one successor ticket linked to the predecessor, three tickets in total,
the predecessor retained and `Exchanged`, coupon 1 still `Used` with no successor lineage and absent from the
exchange record, coupons 2 and 3 `Exchanged`, the successor holding exactly the successors of coupons 2 and 3,
one new order service with no duplicate ids, customer total unchanged, obligation version unchanged, and
commercial version and financial sequence each advanced exactly once.

Completed replay call counts: a further replay returns the same operation id with `IsReplay` true and
`Completed`, with zero inventory applies, zero inventory recoveries, zero document exchanges and zero document
recoveries, still one exchange `OrderChange`, still three tickets, and an unchanged commercial version.

One assertion of mine was wrong here and was corrected against the frozen F2 semantics: a completed even
exchange advances `FinancialSequence` by one. `ExchangeFlowTests` already fixes that rule, and the test now
also asserts the exchange price change set carries that same sequence.

### Case M — malformed host mapping, replayed

`M_a_host_mapping_that_names_the_used_coupon_needs_reconciliation` now replays the same operation after the
first `NeedsReconciliation`.

* Document exchange dispatches: exactly 1, no second dispatch on replay.
* Successor ticket: none, and the plan's pre-allocated successor id resolves to nothing.
* Coupon states: coupon 1 `Used`, coupons 2 and 3 `Open`, predecessor `PartiallyUsed`, empty exchange record.
* Operation status: `NeedsReconciliation` on both attempts, same operation id, `SuccessorElectronicTicketId`
  null both times.
* Raw evidence: the plan still carries the malformed successor mapping naming `Used` coupon 1, unrepaired and
  unnormalized, while the plan's own coupon set never contains coupon 1. That difference is exactly what
  explains the inconsistency.
* Claim: retained, a second operation returns 20070.
* Commercial version unchanged, no exchange `OrderChange`.

---

## 3. Production Impact

```text
Production behavior changes: None
```

No file under `src/` was modified by this correction. Nothing in section 10 of the brief was touched:
`ExchangePreconditions`, `ExchangeCapabilityPolicy`, `ElectronicTicket.MarkExchanged`, the accepted-plan
schema, the AirPrice request DTO, the fare-construction DTOs, the document host port, the inventory port, the
public Exchange API, the enums and the migration history are all unchanged. No new migration.

Files changed, all test or documentation:

| File | Change |
| --- | --- |
| `tests/.../Contracts/ExchangeQuote/ExchangeQuotePortContract.cs` | The four contract corrections. |
| `tests/.../Contracts/ExchangeQuote/DeterministicExchangeQuotePortTests.cs` | Two simulator-only properties. |
| `tests/.../P3/PartiallyUsedExchangeFlowTests.cs` | Boundary side-effect proof, Case N replay, Case O durable confirmation, Case M replay, resumable caller overload, two row counters. |
| `reports/order-domain-v1/p3/P3-integration-capability-catalog.md` | Corrected ICC semantics, moved into the reports folder. |

The three new flow tests found two defects, both in my own assertions rather than in production: the
servicing-operation baseline and the financial-sequence expectation. Neither indicated a production fault.

---

## 4. Catalog

`ICC-P3-EXCHANGE-AIRPRICE` now states three things explicitly under **Outcome Semantics**:

1. Even is defined by the customer balance that `ExchangePricingPolicy.NetCustomerBalance` computes, not by a
   gross ledger balance, and informational or accounting lines need not gross-balance.
2. Quote side-effect freedom is not the same as two independent quotes being identical. Independent quotes may
   differ in id, expiry and refreshed pricing; only the binding to order, version, predecessor and scope is
   invariant. Identity equality is a simulator property.
3. Historical evidence may influence and appear in authoritative pricing evidence, but must not be
   mechanically attributed as transferred successor value. The rule is stated as the
   `SourceLineRef` → `AcceptedExchangePricingLine` → `Transfer` + `PredecessorCorrelationRef` resolution.

The **Deterministic Simulator** section now names the simulator a deliberately simple Even fixture and lists
its two non-obligations explicitly: filtering evidence to the open scope, and answering identically every
time.

The **Consumer Contract Tests** section records that collections are compared by domain identity rather than
enumeration order, that the fixture deliberately carries `Used` pricing evidence, and that Ordering-visible
side-effect freedom is proved at the application boundary.

`ICC-P3-EXCHANGE-INVENTORY` and `ICC-P3-EXCHANGE-DOCUMENT` now reference the completed partial-use replay
proofs with their exact call counts and state assertions.

No real-service integration is claimed as verified. Every `BLOCKED_INTEGRATION` item is unchanged, because no
repository evidence emerged to close any of them.

---

## 5. Regression

One freeze-gate run at the end.

| Gate | Result |
| --- | --- |
| `dotnet build AeroTech.Ordering.sln` | Succeeded |
| Domain tests | 493 / 493 passed |
| Persistence tests | 675 / 675 passed |

Focused runs during the work:

| Suite | Result |
| --- | --- |
| `PartiallyUsedExchangeFlowTests` | 19 / 19 passed |
| Port contract kit | 26 / 26 passed |
| `ExchangeCrashBoundaryTests`, `DocumentExchangeIdentityTests`, contracts | 66 / 66 passed |

No other test project was modified.

---

## 6. Blockers

```text
BLOCKED_DEVELOPMENT:
None.
```

```text
BLOCKED_INTEGRATION:
1. No ingestion path writes TicketCouponFinancialStatus.Used. A usage ingestion
   (integration event, consumer, and a domain transition on ElectronicTicket) is
   required before real partial-use servicing can occur.
2. Consumed-operational-segment evidence is not stored. Issue-time and current-bound
   segments are both reported where they differ; neither is the flown segment.
3. AirPrice: unverified whether it accepts the open-scope / historical-used split, the
   stored fare-construction snapshot, a caller-supplied replay-safe operation key, or
   returns a partially-used Even outcome at all.
4. Inventory: unverified whether FlightFlow exposes a plan-level replace with a stable
   caller key and a WasDispatched read-back.
5. Document host: unverified whether it accepts a coupon-subset reissue of a partially
   used document, correlates on document number plus predecessor coupon number without
   Ordering-local ids, and exposes a WasDispatched read-back.
6. TicketCouponControlStatus is persisted but has no writer, so coupon control is not yet
   part of the exchange decision.
```

The AddCollect capability has not been started.
