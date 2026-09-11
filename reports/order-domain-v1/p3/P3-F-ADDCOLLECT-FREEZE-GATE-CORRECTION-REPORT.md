# P3-F AddCollect — Consolidated Freeze-Gate Correction

Correction report. The AddCollect architecture was preserved; the funding state machines were made correct.

Companion documents:

* [P3-F-ADDCOLLECT-EXCHANGE-REPORT.md](P3-F-ADDCOLLECT-EXCHANGE-REPORT.md) — the AddCollect capability bundle.
* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog.

---

## 1. Production defects corrected

| Defect | Correction |
| --- | --- |
| **A. `FundingMethodRef` lost at the command boundary.** `AcceptExchangeCommandHandler` built `ExchangeExecution` without it, so the real command path could never execute an AddCollect. | The handler now passes it. The gap ran one layer deeper than reported: the REST `AcceptExchange` request had no such field either, so the controller could not supply one. Both are fixed, and the whole path is now proved by a test that starts at the handler. |
| **B. Funding confirmations were trusted unconditionally.** A `Confirmed` guarantee or capture was accepted whatever amount, currency or reference it carried. | `ExchangeFundingEvidencePolicy` validates every confirmation against the persisted obligation. A contradiction is persisted in the stage detail and the operation moves to `NeedsReconciliation`. |
| **C. Release was not a state machine.** An unresolved release was recorded and the parent operation was then settled terminally anyway, resolving the claim and losing the ability to read the release back. | Release is now a stage with its own outcome-driven settlement: confirmed settles the intended terminal state, refused reconciles, unresolved holds `AwaitingExternal` with the claim retained. |
| **D. An unresolved capture was treated as a reconciliation.** `Pending` and `Unknown` went straight to `NeedsReconciliation`, which ended recovery for an operation that was still recoverable. | An unresolved capture is now `AwaitingExternal` with the claim retained, and the next replay reads it back. Only a refusal or a contradiction reconciles. |
| **E. A release that crashed after dispatch was redispatched.** Found by the new test G. The plan held no release outcome yet, so the replay dispatched a second release. | Release follows the frozen dispatch discipline: it dispatches fresh only when the triggering rejection was recorded in this very attempt, and otherwise reads back first. |

Defect E was a genuine double-money-movement path that only the new crash test exposed. It is the reason
this correction was worth running.

---

## 2. Final AddCollect state transition table

`fresh` means the stage that triggers this one completed in this very attempt, so the provider cannot have
seen this operation. Any other entry reads back first and dispatches only on `WasDispatched = false`.

### Guarantee

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, fresh | `GuaranteeAsync` | `Confirmed` + valid evidence | persist, continue to reservation |
| none, fresh | `GuaranteeAsync` | `Confirmed` + contradictory evidence | persist contradiction, `NeedsReconciliation`, no reservation, no document |
| none, fresh | `GuaranteeAsync` | `Rejected` | persist, operation `Rejected`, nothing mutated |
| none, fresh | `GuaranteeAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| `Pending` / `Unknown` | `RecoverGuaranteeAsync` | `WasDispatched = false` | `GuaranteeAsync` under the same key |
| `Pending` / `Unknown` | `RecoverGuaranteeAsync` | `Confirmed` + valid | persist, continue |
| `Pending` / `Unknown` | `RecoverGuaranteeAsync` | `Rejected` | persist, operation `Rejected` |
| `Pending` / `Unknown` | `RecoverGuaranteeAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| `Confirmed` | none | — | continue to reservation |
| `Rejected` | none | — | operation `Rejected` |

### Reservation

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, fresh | `ApplyAsync` | `Confirmed` | persist, continue to document |
| none, fresh | `ApplyAsync` | `Rejected` | persist, enter release with reason `ReservationRejected` |
| none, fresh | `ApplyAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| `Pending` / `Unknown` | `RecoverAsync` | `WasDispatched = false` | `ApplyAsync` under the same key |
| `Pending` / `Unknown` | `RecoverAsync` | any resolved outcome | as the fresh rows above |
| `Confirmed` | none | — | continue to document |
| `Rejected` | none | — | enter release, then terminal |

### Release

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, fresh | `ReleaseAsync` | `Confirmed` | persist `Released`, settle the intended terminal state |
| none, fresh | `ReleaseAsync` | `Rejected` | persist, `NeedsReconciliation`, claim retained |
| none, fresh | `ReleaseAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| any, resumed | `RecoverReleaseAsync` | `WasDispatched = false` | `ReleaseAsync` under the same key |
| any, resumed | `RecoverReleaseAsync` | `Confirmed` | persist, settle the intended terminal state |
| any, resumed | `RecoverReleaseAsync` | `Rejected` | `NeedsReconciliation` |
| any, resumed | `RecoverReleaseAsync` | `Pending` / `Unknown` | `AwaitingExternal`, claim retained |
| `Released` | none | — | settle the intended terminal state |
| `Rejected` | none | — | `NeedsReconciliation`, never retried |

The intended terminal state is a terminal rejection for `ReservationRejected` and `DocumentDenied`, and
reconciliation for `DocumentRejected`.

### Document exchange

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, fresh, funding assured | `ExchangeAsync` | `Confirmed` | persist with raw evidence, enter capture |
| none, fresh, funding assured | `ExchangeAsync` | `Rejected` | persist, enter release with reason `DocumentRejected` |
| none, fresh, funding assured | `ExchangeAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| none, funding not assured | none | — | `NeedsReconciliation`, never dispatched |
| `Pending` / `Unknown` | `RecoverAsync` | `WasDispatched = false` | `ExchangeAsync` under the same key |
| `Pending` / `Unknown` | `RecoverAsync` | any resolved outcome | as the fresh rows above |
| `Confirmed` | none | — | enter capture |

### Capture

| Durable state | Action | Result | Next |
| --- | --- | --- | --- |
| none, document just confirmed | `CaptureAsync` | `Confirmed` + valid evidence | persist, local finalization, `Completed` |
| none, document just confirmed | `CaptureAsync` | `Confirmed` + contradictory evidence | persist contradiction, `NeedsReconciliation`, no local commit |
| none, document just confirmed | `CaptureAsync` | `Rejected` | persist, `NeedsReconciliation`, claim retained |
| none, document just confirmed | `CaptureAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, document stays `Confirmed`, claim retained |
| `Pending` / `Unknown` | `RecoverCaptureAsync` | `WasDispatched = false` | `CaptureAsync` under the same key |
| `Pending` / `Unknown` | `RecoverCaptureAsync` | `Confirmed` + valid | persist, local finalization only, no second document call |
| `Pending` / `Unknown` | `RecoverCaptureAsync` | `Rejected` | persist, `NeedsReconciliation` |
| `Pending` / `Unknown` | `RecoverCaptureAsync` | `Pending` / `Unknown` | persist, `AwaitingExternal`, claim retained |
| `Confirmed` | none | — | local finalization |
| `Rejected` | none | — | `NeedsReconciliation`, never retried |

Local finalization — `Order.CommitExchange`, predecessor `MarkExchanged`, successor issuance — runs exactly
once and only when the document is `Confirmed` and, for AddCollect, the capture is `Confirmed` with matching
evidence.

---

## 3. Public command-path proof

```text
AcceptExchange (REST body).FundingMethodRef
  -> AcceptExchangeCommand.FundingMethodRef            controller
  -> ExchangeExecution.FundingMethodRef                handler
  -> NewPlan(..., execution.FundingMethodRef)          service
  -> AcceptedExchangePlanRow.FundingMethodRef          persisted, survives replay
  -> ExchangeFundingGuaranteeRequest.FundingMethodRef  provider
```

`AddCollectFundingRecoveryTests.A_the_funding_method_survives_the_command_boundary_all_the_way_to_the_provider`
drives the real `AcceptExchangeCommandHandler` and asserts the value on both the persisted plan and the
guarantee the provider received. Its sibling proves that omitting it is refused with code 20276 before any
guarantee, inventory or document call.

---

## 4. Provider evidence validation

`ExchangeFundingEvidencePolicy` is the single decision point, used for both the guarantee and the capture.
For a `Confirmed` result it requires:

```text
plan.AddCollect                  exists
result.ProviderReference         present and non-blank
result.Amount                    present
result.CurrencyId                present
result.Amount     ==             plan.AddCollect.Amount
result.CurrencyId ==             plan.AddCollect.CurrencyId
```

Any failure returns a contradiction sentence, which is persisted in `FundingGuaranteeDetail` or
`FundingCaptureDetail` and drives the operation to `NeedsReconciliation`. A contradictory guarantee dispatches
no inventory and no document; a contradictory capture performs no local commit and no second document call.
A contradiction is never retried, because it is provider evidence disagreeing with the accepted obligation
rather than a transient failure.

---

## 5. Recovery and crash tests

All in `AddCollectFundingRecoveryTests` unless stated. Dispatch counts are totals for the whole scenario.

| Case | Scenario | Guarantees | Captures | Releases | Recoveries | Final |
| --- | --- | --- | --- | --- | --- | --- |
| C | guarantee unresolved, resolves on read-back | 1 | 1 | 0 | guarantee ≥ 1 | `Completed` |
| D | guarantee stays unresolved | 1 | 0 | 0 | guarantee ≥ 1 | `AwaitingExternal`, claim held |
| B | guarantee confirmed with wrong amount, wrong currency, or no reference | 1 | 0 | 0 | 0 | `NeedsReconciliation`, no inventory, no document |
| E | release unresolved, then confirmed on read-back | 1 | 0 | 1 | release ≥ 1 | `Rejected` only after the release settles |
| F | release stays unresolved | 1 | 0 | 1 | release ≥ 1 | `AwaitingExternal`, claim held |
| G | release confirmed, response lost | 1 | 0 | 1 | release 1 | `Rejected`, one release only |
| G′ | release refused | 1 | 0 | 1 | 0 | `NeedsReconciliation`, never retried |
| H, I | capture unresolved, then confirmed on read-back | 1 | 1 | 0 | capture ≥ 1 | `Completed`, one successor, one order change, one price change set |
| J | capture stays unresolved | 1 | 1 | 0 | capture ≥ 1 | `AwaitingExternal`, `RequiresReconciliation = false` |
| K | capture confirmed, response lost, process restarted | 1 | 1 | 0 | capture 1 | `Completed`, zero document and zero inventory calls in the new process |
| L | capture confirmed with wrong amount, wrong currency, or no reference | 1 | 1 | 0 | 0 | `NeedsReconciliation`, document stays `Confirmed`, no local commit |
| M | capture refused | 1 | 1 | 0 | 0 | `NeedsReconciliation`, claim held, never retried |
| O | partially used, capture unresolved then confirmed | 1 | 1 | 0 | capture ≥ 1 | `Completed`, `Used` coupon untouched, one inventory item |

Case K is a true cross-process test. The deterministic provider is now injectable into the harness, so the
first harness is disposed and a second one, with a fresh `DbContext` and the same caller, resumes against the
same provider instance. That is what makes "provider confirmed, local persistence lost, process restarted"
testable rather than simulated.

`AddCollectExchangeFlowTests` keeps the capability matrix, with cases O and W retargeted to the corrected
semantics: an unresolved capture is `AwaitingExternal` and not a reconciliation, and the projection is proved
to distinguish that from a refused capture.

---

## 6. Same-key conflict protection

The simulator now stores each operation's intent beside its outcome. A repeat with the same intent replays the
original result; a repeat with a different intent fails closed with an `InvalidOperationException`, matching
the convention the exchange quote adapter already uses.

The reusable contract proves it per stage:

| Stage | Conflicting fields exercised |
| --- | --- |
| Guarantee | amount, currency, funding method, order id, operation id, quoted exchange id, predecessor document number |
| Capture | amount, currency, successor document number, guarantee reference |
| Release | guarantee reference, release reason |

Idempotency is same identity with same intent, never same identity with a changed intent.

---

## 7. Persistence and schema changes

**None.** No migration, no new column, no changed column, no enum renumbering. The nine funding columns added
by the AddCollect bundle already carried everything this correction needed: the contradiction sentences go
into the existing `FundingGuaranteeDetail` and `FundingCaptureDetail`, and the release state into the existing
`FundingReleaseOutcome` and `FundingReleaseDetail`.

---

## 8. ICC changes

`ICC-P3-EXCHANGE-FUNDING` in
[P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) now states explicitly:

* confirmed funding evidence must match the accepted AirPrice obligation exactly, and a mismatch is
  contradictory provider evidence rather than a business rejection;
* `Pending` and `Unknown` are recoverable at all three stages;
* `NeedsReconciliation` is not a substitute for recover and read-back while a recoverable provider operation
  is unresolved, and reconciliation is reserved for terminal or contradictory evidence;
* an unresolved capture is `AwaitingExternal` with `RequiresReconciliation = false`, distinguishable from a
  refused capture;
* the same idempotency key with a conflicting semantic request must fail closed;
* recover-first applies to the release exactly as to the guarantee and the capture, and a read-back must
  preserve the operation's immutable amount, currency and provider reference.

The simulator and contract-test sections were rewritten to match, and the funding blocker list gained the
exact-obligation-evidence requirement, renumbered to six items. Real Payment remains
`BLOCKED_INTEGRATION` throughout.

---

## 9. Focused test results

| Suite | Result |
| --- | --- |
| `AddCollectExchangeFlowTests` and `AddCollectFundingRecoveryTests` | 54 / 54 |
| Funding port contract kit | 24 / 24, within 50 / 50 for all port contracts |
| All exchange plus contract tests | 195 / 195 |

---

## 10. Full freeze-gate regression results

| Gate | Result |
| --- | --- |
| `dotnet build AeroTech.Ordering.sln` | Succeeded |
| All exchange and port contract tests | 195 / 195 |
| Domain tests | 502 / 502 |
| Persistence tests | 753 / 753 |

No other test project was modified.

---

## 11. BLOCKED_DEVELOPMENT

```text
BLOCKED_DEVELOPMENT:
None.
```

Every semantic in this correction was specified by the brief or already frozen in the code. No business rule
was invented.

---

## 12. BLOCKED_INTEGRATION

Funding, all against real Payment:

1. **Read-back by caller key, for all three stages.** Payment must answer, for the key Ordering generated,
   whether it saw the operation and what the authoritative outcome is, preserving that operation's amount,
   currency and reference. An adapter cannot invent this, because a client-side record of what was sent is
   lost exactly when the process crashes.
2. **Exact obligation evidence on a confirmation.** A `Confirmed` guarantee or capture must state amount,
   currency and a provider reference. An adapter must not synthesize the amount from the request it sent, or
   the validation becomes tautological.
3. **Two-stage guarantee then capture.** Protect an amount, capture it later against the successor document,
   release it if the exchange dies first. An adapter must not simulate a guarantee by capturing immediately.
4. **Same-key idempotency on money operations**, including failing closed on a conflicting intent.
5. **Release semantics**, independently recoverable and safe to call more than once.
6. **Completion after authorization.** If Payment cannot guarantee it, the reconciliation state this bundle
   persists is the correct terminal representation.

Carried forward unchanged, and not addressed here:

1. no ingestion writer for `TicketCouponFinancialStatus.Used`;
2. consumed-operational-segment evidence not stored;
3. AirPrice partially-used and replay semantics unverified against the real service, including the explicit
   add-collect amount;
4. inventory plan-level and recovery contract unverified against the real service;
5. document subset-reissue and recovery contract unverified against the real host;
6. `TicketCouponControlStatus` has no production writer.

None of these block Ordering implementation. A passing deterministic simulator is not evidence of production
readiness for any provider.

---

## 13. Scope confirmation

```text
Residual Exchange      not started
Refund-due Exchange    not started
Mixed Exchange         not started
StoredValue            not started
EMD servicing          not started
```

`Refund`, `Residual` and `Mixed` remain deferred by `ExchangePricingPolicy.DeferralReason` and are asserted as
such. No refund, reversal or wallet instruction exists. No new servicing capability was begun.

Every mandatory recovery test in the brief passes, so the AddCollect bundle meets this freeze gate.
