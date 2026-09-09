# P3 — Servicing Phase Plan

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg`
**Entry commit:** `fb090eb768e2a079352b7d0e7ee59cab87003f6d` (P2 freeze) · **Date:** 2026-09-09

Binding sequence for P3 implementation. Design authority:
[`P3-A-SERVICING-DESIGN.md`](P3-A-SERVICING-DESIGN.md) · Evidence:
[`P3-A-SERVICING-BENCHMARK.md`](P3-A-SERVICING-BENCHMARK.md).

Test baseline at entry: **811 passed / 0 failed** (451 Domain + 360 Persistence).

---

## Phase breakdown

The seven-phase structure proposed in the P3-A brief is **retained**. Benchmark evidence did not require
re-ordering it. Two constraints discovered in the pushed code shape *what lands where*:

- **Rail unification is a prerequisite, not a cleanup.** The live `{id}/Cancel` and
  `{id}/Documents/{documentId}/Void` endpoints run on the legacy `FulfillmentTask` + `TrafficDocument` rail, not
  the P0 `OrderOperationCoordinator` + P2 `ElectronicTicket` rail (finding **B-3**). Building Refund or Exchange
  on top of a split rail would make the split permanent. Unification therefore lands in **P3-B** (cancel) and
  **P3-C** (void), before any money-returning operation.
- **The contract extension happens once.** `ServicingOperationKind` needs five new members (§2.1 of the design).
  That is a wire-contract change; it is done **once, in P3-B**, append-only, and coordinated through the handoff
  ledger — never incrementally.

| Phase | Scope | Rationale for position |
|---|---|---|
| **P3-B** | Pre-ticket Cancel / Remove Service | No document, no refund, no provider money. Lowest risk place to unify the rail and land the contract extension |
| **P3-C** | ETKT / EMD Void | Activates the inert P2 document lifecycle (finding **B-4**) and makes coupon + control preconditions real (**B-6**). No pricing-source dependency |
| **P3-D** | Refund | First operation that needs an external calculation authority. Requires P3-C's document lifecycle |
| **P3-E** | Voluntary Change + Revalidation | Reuses P3-D's quote/accept machinery; revalidation is the document outcome that needs **no** successor document |
| **P3-F** | Exchange / Reissue | Needs document lineage (**B-5**) and everything P3-D/P3-E establish |
| **P3-G** | EMD / Ancillary Servicing | The Amadeus reissue rule (EMD disassociated → refund or exchange) can only be implemented once reissue exists |
| **P3-H** | Reconciliation, involuntary boundary, hardening | Cross-cutting; verifies the whole surface |

---

## P3-B — Pre-ticket Cancel / Remove Service

**Three distinct scopes — do not collapse them** (design §2.2):

| Scope | Business semantic | Internal operation kind | `OrderChangeType` |
|---|---|---|---|
| Whole Order | **Cancel** | existing `Cancel` | `Cancel` |
| Whole OrderItem | **Cancel** (cancellation family) | existing `Cancel`, item-scoped | `Cancel` |
| One or more Services, containing OrderItem survives | **OrderChange — Remove Service** | new `RemoveService = 14` | new **`RemoveService = 12`** |

**Deliver**
- Whole-Order cancel and whole-OrderItem cancel, both in the cancellation family, under the existing
  `ServicingOperationKind.Cancel`.
- Service removal while the containing OrderItem survives, as an **OrderChange**, under the new
  `ServicingOperationKind.RemoveService` — an `AeroTech technical abstraction` mirroring the existing
  `AddService`, **not** a public `ServiceRemoval` business operation.
- Unify the live Cancel path onto `OrderOperationCoordinator`; retire or fence the legacy
  `OrderCancelService` + `FulfillmentTask` cancel rail.
- The frozen append-only enum extensions (design §2.1). **No existing value is renumbered:**

  | Enum | Last existing | Appended |
  |---|---|---|
  | `ServicingOperationKind` | `AddService = 9` | `Refund = 10`, `Exchange = 11`, `Revalidate = 12`, `CancelRefund = 13`, `RemoveService = 14` |
  | `OrderChangeType` | `Close = 11` | `RemoveService = 12` |
  | `PricingSource` | `Manual = 4` | `OrderingDerived = 5` |

- Stamp locally derived reversals `PricingSource.OrderingDerived` (design §9.2 / §9.3, finding **B-2**). This is a
  **cutover, not a migration**: historical rows stamped `PricingEngine` are **never rewritten or backfilled**, and
  only qualifying reversals created after the change carry `OrderingDerived`.
- Cancellation fee accepted from the pricing owner as a `Fee`/`Penalty` line — never computed, never stamped
  `OrderingDerived`.

**Do not**
- Describe or expose `RemoveService` as an IATA cancellation operation.
- Classify service removal as `OrderChangeType.VoluntaryChange` — it is `OrderChangeType.RemoveService`.
- Rewrite, backfill or migrate historical `PricingSource.PricingEngine` rows.
- Let a derived `RollUpCancelledItems` outcome overwrite the caller's recorded intent.
- Touch documents. Cancel with an issued document for the scope must be refused and routed to Void or Refund.
- Extend `ReverseServiceValue` toward refund semantics, or stamp any calculated amount `OrderingDerived`.
- Renumber any existing enum value.

**Exit gate** — the three scopes are separately observable through `ServicingOperationKind` + `OrderChangeType`
(`Cancel` for the two cancellation scopes, `RemoveService` for service removal), and removing an item's last
service still records `RemoveService` intent even though roll-up cancels the item; historical `PricingEngine`
rows are provably unchanged; dependent-service scope respected; capacity released under a stable operation key; a scope with
an issued document is refused; one `PriceChangeSet` per accepted operation; locally derived reversals carry
`OrderingDerived` while no calculated amount does; replay is idempotent; legacy cancel rail no longer reachable
from a production channel; full regression green.

## P3-C — ETKT / EMD Void

**Deliver**
- Void on the P2 `ElectronicTicket` (activating the existing, currently uncalled `Void()`), replacing the legacy
  `TrafficDocument` void path.
- EMD void: extend `ElectronicMiscDocumentStatus` and `EmdCouponStatus` (currently one value each) and add the
  document operation.
- `IDocumentVoidPort` — issuer eligibility answer plus execution. **No hard-coded same-day rule**; per-document
  `VoidDeadline` plus the provider answer.
- Coupon financial status **and** `TicketCoupon.ControlStatus` as hard preconditions (finding **B-6**).
- Void-window-expired ⇒ refused and routed to Refund. Never forced into Void.

**Do not**
- Compute any refund. Void's money consequence is a request to the payment owner.
- Void an EMD implicitly as a side effect of voiding a ticket.

**Exit gate** — void succeeds only from a valid coupon/control state; expired window refused with the correct
routing; ETKT and EMD void are independent operations; Pending/Unknown leaves the operation reconcilable under the
same key with the document number preserved; legacy `TrafficDocument` void no longer reachable.

## P3-D — Refund

**Deliver**
- `IRefundQuotePort` — side-effect-free quote returning the decomposed accepted result (fare paid / fare used /
  refundable fare, per-occurrence refundable and retained taxes, penalty, fee, commission, disposition reference).
- Accept-exact-quote execution reusing the frozen receipt/claim/fingerprint mechanism.
- `IDocumentRefundPort`; coupons → `Refunded`; document → `Refunded` or `PartiallyUsed`.
- Full unused, partial-after-use, and tax-only-on-non-refundable-fare scenarios.
- Penalty as `ComponentType.Penalty` **debit**, never a reversal; waiver recorded with authority provenance.
- Manual refund path with `PricingSource.Manual` + actor + authority.
- `CancelRefund` corrective reversal as a **new** operation with its own provider evidence; after affirmative
  provider confirmation the coupon/document current state may move to the provider-confirmed state (design §9.1).
- Servicing-record redisplay through the existing projection.

**Do not**
- Reuse `ReverseServiceValue` for refund lines (design §9.2 — the critical boundary).
- Derive any refundable amount from `PricingAllocation`.
- Compute used/flown value, tax refundability, penalty, or commission.
- Create a voucher, wallet or credit-shell balance.

**Exit gate** — a refund with **no** accepted source result is impossible; `refund ≠ total − allocations` proven by
a partial-use scenario; non-refundable fare with refundable tax produces a non-zero refund; commission stays
`SettlementOnly` and never moves customer balance; **no refund amount is stamped `OrderingDerived`**; corrective
reversal appends history and never deletes it, while current state moves only on affirmative provider
confirmation; exactly one `OrderPricingChanged` per committed set.

## P3-E — Voluntary Change + Revalidation

**Deliver**
- `IChangeQuotePort` (reshop) and `IDocumentChangeEligibilityPort` returning revalidate / reissue / denied.
- Revalidation: same document, `DocumentVersion++`, coupon rebound via the existing
  `TicketCoupon.CurrentOrderServiceId`. **No successor document.**
- Accepted change plan states which services continue, which are replaced, which are created — a repriced pricing
  unit does not re-identify unchanged services.
- All monetary outcome shapes kept distinct: even, add-collect, refund, residual, mixed, penalty netted vs
  separately collected.

**Do not**
- Decide revalidation eligibility locally, or silently fall back to reissue when denied
  (`EligibilityOutcome.PendingEvidence`).
- Collapse outcomes into one signed difference.

**Exit gate** — revalidation issues no document and advances no `CommercialVersion` for the document act itself;
denial does not downgrade; each outcome shape is separately representable; **EMD stays associated across
revalidation** (Amadeus S1).

## P3-F — Exchange / Reissue

**Deliver**
- Explicit append-only document lineage on `ElectronicTicket` (finding **B-5**): predecessor/successor at document
  and coupon level.
- `IDocumentExchangePort`; successor issued; old document → `Exchanged`, old coupons → `Exchanged`.
- Old→new value carried as `PricingLineRole.Transfer`; add-collect, residual and penalty stay distinct lines.
- Partially used exchange: unflown scope only, with used history retained as pricing context.

**Do not**
- Overwrite, delete or mutate the original document.
- Recompute any component of the exchange.

**Exit gate** — old and new documents both retrievable with intact lineage; partially used exchange preserves used
history; reissue never fabricates a component the source did not supply.

## P3-G — EMD / Ancillary Servicing

**Deliver**
- The Amadeus rule (S1), normalized: reissue ⇒ EMD-A **disassociated**, then source-decided refund, exchange into a
  new EMD associated at issuance, retain-as-residual, cancel, or manual review.
- EMD refund and EMD exchange as their own document operations; refunded/exchanged EMD coupons **disassociated**
  from their e-ticket coupons.
- Explicit reassociation as a first-class outcome (`Amadeus` S2).
- Dependent-ancillary evaluation on flight change — every dependent service gets an explicit source-approved
  outcome.
- EMD-S fee/penalty documentation without inventing an `OrderService` (frozen P2 rule).

**Do not**
- Create wallet liability for `Deposit` / `ResidualValue` coupons — reference the external instrument only.
- Invent an ancillary refund formula.

**Exit gate** — association changes are coupon-level; revalidation preserves association while reissue breaks it;
EMD refund/exchange are independent of ticket refund/exchange; no dependent ancillary is left implicitly untouched.

## P3-H — Reconciliation, involuntary boundary, hardening

**Deliver**
- Reconciliation across every P3 operation: `Pending`/`Unknown` under the same operation key, provider state
  queried before any further action, document numbers preserved.
- `ControlStatus` reconciliation (external / release-pending) as a first-class blocker.
- Involuntary **boundary**: authority and reason preserved on `InvoluntaryChange` / `Reaccommodation`; no
  hard-coded "involuntary means free".
- No-show **negative guarantee**: tests proving nothing infers cancellation, forfeiture or penalty from delivery
  status.
- SSR negative guarantee retained.
- Full P3 architecture, benchmark-traceability and migration audit; P3 freeze.

**Do not**
- Build DCS integration, disruption recovery, or a no-show flow.
- Introduce an involuntary pricing policy.

**Exit gate** — every servicing operation is reconcilable; no operation blind-retries an economic or document
action; involuntary authority preserved; negative guarantees hold; full regression green; P3 frozen.

---

## Cross-phase invariants

Enforced in every phase, verified again in P3-H:

1. `Cancel ≠ Void ≠ Refund ≠ Exchange ≠ Revalidation`.
2. Quote is side-effect free; only acceptance mutates.
3. Ordering computes no fare, tax, penalty, refund or exchange amount.
4. Refund entitlement is never inferred from `PricingAllocation`.
5. One `PriceChangeSet` and exactly one `OrderPricingChanged` per accepted servicing money outcome.
6. Document acts advance `DocumentVersion`; only accepted commercial consequences advance `CommercialVersion`.
7. `Pending`/`Unknown` is not failure.
8. Corrective operations append immutable history and never delete it; current state moves **only** on
   affirmative provider confirmation, never on an optimistic local status flip.
9. No provider-specific entity enters the domain.
10. No wallet, ledger, ATPCO engine, or Money/ROE subsystem is created.
11. Enum extensions are append-only; no persisted numeric value is renumbered (note the existing intentional hole
    at `RefundabilityRule = 2`). The P3-B values are frozen: `ServicingOperationKind` `Refund = 10` /
    `Exchange = 11` / `Revalidate = 12` / `CancelRefund = 13` / `RemoveService = 14`;
    `OrderChangeType.RemoveService = 12`; `PricingSource.OrderingDerived = 5`.
12. Ports follow the frozen `Domain/Ports/<Area>` convention with a deterministic double and a fail-closed
    `Unconfigured*` production implementation.
13. `PricingSource.OrderingDerived` is used **only** for mechanical reversals of Ordering's own accepted truth —
    never for refund, FareUsed, refundable fare, tax refundability, penalty, cancellation fee, exchange/repricing,
    residual value, or any other source-calculated servicing amount (design §9.3). Historical provenance is never
    rewritten.
14. No design decision rests solely on weak or secondary vendor evidence. Where primary SITA Horizon or Navitaire
    New Skies operational documentation is unavailable, the gap is stated explicitly rather than presented as
    Tier-1 confirmed behaviour.
