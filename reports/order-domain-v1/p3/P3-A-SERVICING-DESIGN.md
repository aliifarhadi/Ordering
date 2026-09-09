# P3-A — Servicing Design Baseline (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg`
**Entry commit (P2 freeze):** `fb090eb768e2a079352b7d0e7ee59cab87003f6d` — verified HEAD, clean, == `origin/k8s-stg`.
**Date:** 2026-09-09 · **Status:** binding design baseline. Later P3 phases follow this document.
Benchmark evidence: [`P3-A-SERVICING-BENCHMARK.md`](P3-A-SERVICING-BENCHMARK.md) · Sequence:
[`P3-PHASE-PLAN.md`](P3-PHASE-PLAN.md).

Documentation only. **No production code was modified in P3-A.**

Every concept below carries a provenance marker: `IATA` · `ATPCO` · `Amadeus` · `Sabre` · `SITA` · `Navitaire` ·
`Common PSS practice` · `AeroTech technical abstraction`. Unmarked business concepts are not permitted.

---

## 1. The six-layer separation (binding)

Servicing is **not** one generic engine and **not** an extension of `OrderChange`. Six concerns stay separate and
correlate only through a durable operation identity.

```
 1  COMMERCIAL TRUTH                 owned by Ordering
    what obligation is added, cancelled, retained, replaced or moved
    -> Order, OrderItem, OrderService, OrderChange, PriceChangeSet, PricingLine

 2  PRICING / REFUND / RESHOP        owned by the authoritative pricing source
    what the accepted monetary outcome is and why
    -> AirPrice / pricing owner. Ordering implements NO ATPCO formula.

 3  SERVICING ORCHESTRATION          owned by Ordering (technical)
    durable operation identity, idempotency, claim fencing, reconciliation
    -> ServicingOperation, CommandReceipt, OperationOrderClaim, provider operation key

 4  ACCOUNTABLE DOCUMENT LIFECYCLE   owned by Ordering
    ETKT and EMD existence, coupon state, control, lineage
    -> ElectronicTicket / TicketCoupon, ElectronicMiscDocument / EmdCoupon

 5  PROVIDER / HOST EXECUTION        owned by external providers
    inventory, document issuance/void/refund/exchange, eligibility answers
    -> Ordering-owned ports only

 6  PAYMENT / REFUND / VALUE         owned by the payment/value owner (JetPay, StoredValue)
    authorization, capture, refund, release, voucher / credit-shell execution

 7  LEDGER / ACCOUNTING              owned by Ledger
    Ordering publishes commercial facts. NO runtime Ledger dependency.
```

**Provenance:** the 1/2/4/6 split is `IATA` — ONE Order defines order servicing as "re-shopping for an Offer,
through to applying any changes to the Order, processing further payments or refunds, and modifying accountable
documents" (S15). It is independently corroborated by `SITA`, whose `Horizon Ticketing` issues documents while
`Airfare Price` "automates ticket repricing and refunding" (S11) — two different modules. Layer 3 is
`AeroTech technical abstraction` (the frozen P0 foundation). Layer 7's non-dependency is a frozen P2 invariant.

### 1.1 Correlation, not shared state

A successful document change does not fabricate a successful refund. A confirmed refund does not prove the
commercial service was cancelled. A cancelled service does not prove money moved. Each layer is separately
observable and separately reconcilable. *(Provenance: `Common PSS practice`; explicit in Amadeus and Sabre where
document void and payment reversal are distinct operations.)*

---

## 2. Operation taxonomy (binding)

`Cancel ≠ Void ≠ Refund ≠ Exchange/Reissue ≠ Revalidation`, even when one UI button triggers them.
*(Provenance: `Amadeus`, `Sabre`, `SITA`, supplementary `Travelport`.)*

| Operation | Precondition | Document effect | Money effect | Provenance |
|---|---|---|---|---|
| **Pre-ticket Cancel / Service Removal** | no accountable document for the scope | none | possible cancellation fee; release of unused authorization | `IATA` + `Common PSS practice` |
| **Void** | document exists, within issuer void eligibility, coupons unused, control local | whole document voided | release **or** refund depending on actual payment state | `Amadeus`, `Sabre` |
| **Refund** | document exists, coupons refundable, source approves | affected coupons → Refunded | approved credit to an approved disposition | `ATPCO` Cat33 + `Amadeus` Refund Record |
| **Revalidation** | accepted change + issuer permits | **same** document, coupon rebound, `DocumentVersion++` | usually none; penalty possible | `Amadeus` (S3) |
| **Exchange / Reissue** | accepted change + eligible unused coupons | successor document + lineage; old coupons → Exchanged | add-collect / refund / residual / penalty | `Amadeus` ATC, `Sabre` |
| **EMD servicing** | EMD exists | void / refund / exchange / reassociate / disassociate | per source | `Amadeus` (S1), `Sabre` (S10) |

### 2.1 Contract extension required

`ServicingOperationKind` at P2 freeze is `CreateOrder, Reserve, RequestPayment, Issue, Cancel, VoidDocument,
Split, Expire, AddService`. P3 requires, as a **single append-only** extension (new numeric values only; nothing
renumbered):

```
Refund          -- ATPCO Cat33 / Amadeus refund      [ATPCO, Amadeus]
Exchange        -- reissue producing a successor doc [Amadeus ATC, Sabre]
Revalidate      -- rebind without a new document     [Amadeus]
VoidRefund      -- corrective reversal of a refund   [Sabre S9, Amadeus]
ServiceRemoval  -- scoped pre-ticket cancel          [IATA OrderItem cancel]
```

This is a **wire-contract change** to `Contracts/AeroTech.Messages` (frozen P2 rule: renaming or re-versioning a
published event changes the MassTransit type name). It must happen **once**, in P3-B, coordinated through the
handoff ledger — not incrementally across phases.

`OrderChangeType`, `PriceChangeReason`, `PricingComponentType.Penalty`, `PricingLineRole.Transfer`,
`PricingSource.Manual`, `ElectronicTicketStatus.{Exchanged,Refunded,PartiallyUsed,Suspended}`,
`TicketCouponFinancialStatus.{Exchanged,Refunded,Suspended}` and `EligibilityOutcome.PendingEvidence`
**already exist** and require no change. `ElectronicMiscDocumentStatus` and `EmdCouponStatus` currently hold a
single value each and must be extended in P3-C/P3-G.

---

## 3. Quote vs accepted decision semantics (binding)

*(Provenance: `IATA` OrderReshop, `ATPCO` Cat31/33, `Amadeus` ATC, `Sabre`; mechanism is
`AeroTech technical abstraction` reusing the frozen P2-E.1 pattern.)*

```
QUOTE   side-effect free. No OrderChange, no PriceChangeSet, no PricingLine,
        no CommandReceipt, no ServicingOperation, no claim, no outbox message,
        no document mutation, no provider economic action.

ACCEPT  the caller accepts an EXACT quote identity + version.
        Only acceptance starts durable execution.
```

The P2 `IOrderChangeQuoteProvider.AcceptSelectedQuotedOfferAsync(AcceptedQuotedOfferSelection)` shape is the
model to replicate — **not to overload**. P3 introduces sibling ports per operation family (§7), because a refund
quote, a reshop quote and a document-eligibility answer are different questions with different authorities.

Binding rules:

1. A quote carries its own identity, version and expiry. **Ordering never silently recalculates a stale quote.**
2. Before any irreversible step, Ordering rechecks `ExpectedCommercialVersion` **and** the external evidence
   relevant to that operation (coupon state, control status, provider eligibility).
3. Acceptance is idempotent under the frozen P0 receipt/claim/fingerprint mechanism. Replay returns the committed
   outcome; it never re-executes an economic or document action.
4. A quote is **not** an entitlement. A quote that has expired, or whose order version moved, is rejected — never
   downgraded, never partially applied.

---

## 4. Document consequences (binding)

### 4.1 Preconditions before any document action

*(Provenance: `Sabre` S8 coupon-status list, `Amadeus`, `SITA`.)*

```
coupon financial status   must permit the action
coupon control status     must be Local (not External / ReleasePending / Unknown)
issuer/provider           must affirmatively answer that the action is available
```

`TicketCoupon.ControlStatus` already exists but is currently read by nothing (finding **B-6**). P3-C makes it a
hard precondition. Ordering keeps its own `TicketCouponFinancialStatus` as truth and retains the raw provider code
in the existing `ProviderCouponStatusCode` field. **No vendor status list is imported into the domain.**

### 4.2 Per-operation document effects

| Operation | ETKT | TicketCoupon | EMD | EmdCoupon |
|---|---|---|---|---|
| Pre-ticket cancel | — | — | — | — |
| Void | `Voided`, `DocumentVersion++` | all → `Void` | own void operation | → voided |
| Refund (full) | `Refunded` | affected → `Refunded` | own refund operation | → refunded, **disassociated** |
| Refund (partial) | `PartiallyUsed` → stays; unused coupons → `Refunded` | scoped | per source | scoped |
| Revalidation | same document, `DocumentVersion++` | `CurrentOrderServiceId` rebound | **unchanged, stays associated** | **unchanged** |
| Reissue | old → `Exchanged`; successor issued with lineage | old → `Exchanged`; new coupons issued | **disassociated** from old ticket coupon | refund / exchange / residual / cancel / manual — source decides |

The revalidation-vs-reissue EMD rule is `Amadeus` (S1), adopted verbatim as behaviour and normalized in
expression. It is stated at **coupon** level, which the frozen P2 `EmdCoupon.AssociatedTicketCouponId` already
supports exactly.

### 4.3 Lineage

`ElectronicTicket` has **no** successor/predecessor link at P2 freeze (finding **B-5**). P3-F must add explicit,
append-only lineage — never overwrite or delete an issued document. *(Provenance: `Amadeus`, `Sabre`, `IATA`.)*

The seams that already exist and must be used rather than duplicated:

| Existing P2 field | P3 role |
|---|---|
| `ElectronicTicket.OriginalOrderId` / `CurrentServicingOrderId` | document moving between orders (split, servicing) |
| `TicketCoupon.OrderServiceId` / `CurrentOrderServiceId` | **revalidation rebinding** |
| `EmdCoupon.AssociatedTicketCouponId` | coupon-level association / disassociation |
| `DocumentPriceLink` / `EmdPriceLink` | issue-time value attribution, immutable |
| `ElectronicTicket.VoidDeadline` | per-document void window |

---

## 5. Pricing and refund boundary (binding)

**Ordering computes no refund, no penalty, no reprice, no tax and no exchange difference.**
*(Provenance: `ATPCO` Cat31/33/16; `SITA` module split; `Amadeus` ATC; `Sabre`.)*

### 5.1 What Ordering does

- supplies the pricing owner with historical context: original sale lines, fare construction (P2-C), used/flown
  service history (P2-D), document attribution (`DocumentPriceLink`);
- records the accepted result as immutable `OrderPricingLine`s inside exactly one `OrderPriceChangeSet`;
- validates only conservation rules that are **unambiguous from the accepted payload** (frozen P2-A behaviour);
- refuses to fabricate a component the source did not supply.

### 5.2 Accepted refund/exchange result decomposition

Each component becomes its own pricing line using the **existing** frozen vocabulary. Nothing new is invented.

| Accepted component | Existing representation |
|---|---|
| refundable fare | `ComponentType.Fare`, `Role.Reversal`, `Effect.CustomerBalance` |
| refundable tax occurrence | `ComponentType.Tax`, `Role.Reversal`, per-occurrence identity preserved |
| retained / non-refundable tax | no reversal line is emitted for it |
| penalty | `ComponentType.Penalty`, `Role.Original`, **debit** — never a reversal |
| servicing fee | `ComponentType.Fee`, `Role.Original` |
| refund/reissue commission | `ComponentType.Commission`, `Effect.SettlementOnly` — never moves customer balance |
| old→new value carried into a reissue | `Role.Transfer` |
| manual adjustment | `Role.Adjustment` + `PricingSource.Manual` |

**Binding:** a penalty is a new applied charge, not a negative fare. A waived penalty is **not** a zero-valued
penalty — the waiver reference, authority and actor are preserved as provenance. *(Provenance: `ATPCO` Cat16,
`Amadeus` waiver codes, `IATA` Order Penalty Information.)*

### 5.3 Automatic vs manual, and override provenance

| Mode | `PricingSource` | Required provenance |
|---|---|---|
| Automatic (ATPCO-driven) | `PricingEngine` | source quote reference + version |
| Source-supplied but non-automated | `Supplier` | supplier reference |
| Manual / agency-priced servicing | **`Manual`** | `OrderChange.ActorId` + `ActorScope` + `ExternalReference` (authority) — all already on the P2 entity |

"**Guarantee / assurance provenance**" in the brief maps to this family — it is the industry **waiver / authority
evidence** concept (`Amadeus` waiver code, `ATPCO` Cat31/33 waiver, `IATA` authority). **No new AeroTech business
concept is introduced for it.**

### 5.4 The allocation rule, restated for P3

Frozen: `PricingAllocation` is **attribution only**; refund entitlement must never be inferred from it.

```
refund  !=  original total  −  allocations of delivered services
```

A partially used ticket is valued by the source, which may **reprice the flown portion** under historical rules.
*(Provenance: `ATPCO` Cat33; `Amadeus` Refund Record separates "fare used" from "fare refund", S6.)*

---

## 6. Payment / value boundary (binding)

*(Provenance: `IATA`; `Navitaire` for the ticketless/credit-shell case.)*

- Ordering **requests** value movement and **records** the approved disposition **reference**. It never executes,
  never holds a balance, and never creates a liability.
- **Frozen P2 invariant retained:** Ordering does not create wallet liability for residual/deposit value.
  `EmdCouponPurpose.Deposit` / `ResidualValue` reference an **external** instrument by
  `EmdCoupon.ExternalValueReference`.
- A document void does **not** prove a capture was reversed. An authorization may be released while a captured
  payment requires a refund. The payment owner is authoritative for that distinction.
- Refund destination may be original form of payment, voucher, residual value, or a **credit shell / travel bank**
  (`Navitaire` S14). Ordering records which; it never builds the instrument.
- Refund completion and commercial cancellation are **separately observable**.

---

## 7. Ports (Ordering-owned, provider-neutral)

*(Provenance: `AeroTech technical abstraction`, following the frozen P2 `Domain/Ports/<Area>/I<Name>Port.cs`
convention. **No provider-specific domain entity** — no `AmadeusRefundMask`, no Sabre coupon-status enum, no
vendor mask/screen model — may enter the domain.)*

| Port | Question it answers | Introduced in |
|---|---|---|
| `IRefundQuotePort` | what is the approved refund result for this scope? | P3-D |
| `IDocumentVoidPort` | is this document voidable now, and execute it | P3-C |
| `IDocumentRefundPort` | execute the document-side refund | P3-D |
| `IChangeQuotePort` (reshop) | what is the approved change result? | P3-E |
| `IDocumentChangeEligibilityPort` | revalidation, reissue, or denied? | P3-E |
| `IDocumentExchangePort` | issue the successor document | P3-F |
| `IEmdServicingPort` | EMD void / refund / exchange / reassociation | P3-G |

Existing frozen ports reused unchanged: `IReservationPort`, `IFundingCoveragePort`, `IDocumentIssuancePort`,
`IEmdIssuancePort`, `IOrderChangeQuoteProvider`.

Every new port follows the frozen P2 pattern: request/outcome records in the same file, a deterministic test
double behind `Providers:UseDeterministicTestAdapters`, and a fail-closed `Unconfigured*` production
implementation.

---

## 8. Voluntary / involuntary boundary (binding)

*(Provenance: `IATA`, `ATPCO` — Cat31/33 are voluntary by definition; `Sabre` Schedule Change; `Amadeus`
involuntary reissue.)*

| | Voluntary | Involuntary |
|---|---|---|
| Trigger | customer/agent request | authoritative airline/operational fact |
| Calculation basis | ATPCO Cat31/33 | carrier involuntary policy / recovery instruction |
| Existing vocabulary | `OrderChangeType.VoluntaryChange`, `PriceChangeReason.VoluntaryChange` | `OrderChangeType.InvoluntaryChange` / `Reaccommodation`, `PriceChangeReason.InvoluntaryChange` |
| Authority | actor + scope | **authority + reason must be preserved** |

**Binding:** "involuntary" never implies zero price or zero penalty. The authoritative recovery/pricing contract
states the treatment for the exact case. P3 models the **boundary and the authority provenance** only; disruption
recovery execution and DCS are out of scope (§11).

---

## 9. Reconciliation behaviour (binding)

*(Provenance: `AeroTech technical abstraction` — frozen P0 semantics; corroborated by `Amadeus`/`Sabre` practice
of reconciling before retry.)*

```
Confirmed -> apply
Rejected  -> reject; release claim; no commercial mutation
Pending   -> AwaitingExternal; same operation key; poll/callback
Unknown   -> NeedsReconciliation; query provider state BEFORE any further action
```

**Pending/Unknown is not failure** (frozen P2 invariant). An unknown outcome is never blind-retried as a new
economic or document action; the durable operation key and the allocated document number are preserved. The
existing `ServicingOperationStatus` already carries `AwaitingExternal`, `Compensating` and `NeedsReconciliation` —
no new status vocabulary is required.

Corrective operations (`VoidRefund`, reverse-void, reverse-exchange) **append** evidence under their own operation
identity and provider confirmation. They never delete history and never restore state by flipping a local status.
*(Provenance: `Sabre` same-day cancel-refund S9; `Amadeus` refund cancellation.)*

### 9.1 Conflict with the frozen P2 code — `ReverseServiceValue`

`Order.Termination.cs::ReverseServiceValue` derives scoped reversal amounts from
`line.CommercialAllocations()` filtered by service id, and stamps the result `PricingSource.PricingEngine`.

**Assessment.** For **pre-ticket cancel** and **void**, this is a reversal of *unconsumed sale value* on an
obligation that never became deliverable — a commercial value correction, which is defensible without a pricing
call. It is **not** a refund entitlement, and it must never become one.

**Binding consequences for P3:**

1. **P3-D Refund must not reuse `ReverseServiceValue`.** Refund lines come only from an accepted
   `IRefundQuotePort` result. This is the single most important boundary in P3.
2. `PricingSource` must be stamped honestly. Locally derived reversals are Ordering-derived, not
   `PricingEngine` output. P3-B should correct this provenance (a value-level correction, not a schema change).
3. Where a cancellation *fee* or *penalty* exists, it comes from the pricing owner — never from
   `ReverseServiceValue`.

---

## 10. Servicing record

*(Provenance: `Amadeus` Refund Record (S6) — redisplayable, non-editable after processing; `Common PSS practice`.)*

After finalization, Ordering must be able to redisplay what was accepted and completed **without recomputing
today's fares or rules**: the servicing operation, original and successor documents/coupons, affected services,
the accepted pricing decision and its immutable lines, penalty/waiver/authority evidence, value-movement
references, provider confirmations, and actor/office context.

**Implementation shape is not prescribed here and it is not a new aggregate.** The frozen P2 spine already
provides most of it: immutable `OrderPriceChangeSet` + `OrderPricingLine`, `OrderChange` with actor/operation
provenance, `ServicingOperation`, and the typed `OrderView` projection. P3 extends the projection; it does not
build a parallel audit subsystem.

---

## 11. Explicitly NOT modelled in P3

| Not modelled | Reason |
|---|---|
| ATPCO rule engine (Cat16/31/33 evaluation) | Owned by the pricing source. Frozen invariant |
| Fare, tax, FX, ROE or rounding computation | Frozen P2 invariant — no local Money/Currency/ROE subsystem |
| Wallet, voucher, credit-shell or travel-bank balances | Value owner's liability, never Ordering's |
| Ledger posting | No runtime Ledger dependency. Frozen |
| JetPay implementation | Payment execution is out of Ordering |
| DCS integration, disruption recovery execution, reaccommodation search | P4+ |
| **No-show** consequences | `NoShow` is a DCS observation. P3 guarantees only that **nothing** infers cancellation, forfeiture or penalty from delivery status. No no-show flow is built |
| **SSR** lifecycle | Out of scope; already asserted by the frozen `SsrBoundaryTests`. Servicing an ancillary never creates an SSR record |
| Name correction / traveller substitution | `OrderChangeType.NameCorrection` exists but is provider-policy-specific; deferred |
| Group booking servicing | Separate aggregate; out of P3 |
| Customer notices, receipts, PDF rendering | Derived presentation artifacts, owned elsewhere |
| Interline / GDS / NDC distribution servicing | Single-carrier scope (CLAUDE.md) |
| IATA external OrderVersion | Frozen: `CommercialVersion` is **not** automatically IATA OrderVersion |
| Provider-specific domain entities (`AmadeusRefundMask`, vendor coupon-status enums, vendor screens) | Explicitly forbidden |

---

## 12. Frozen P2 invariants — P3 compliance check

| Frozen invariant | P3-A position |
|---|---|
| PricingLine is monetary truth | Retained. All servicing money becomes pricing lines |
| PricingAllocation is attribution only | Retained and reinforced (§5.4) |
| Refund entitlement not inferred from allocations | Retained. **Conflict found in existing cancel/void code — recorded in §9.1, must not propagate** |
| Ordering implements no ATPCO formula | Retained (§5) |
| Tax remains source-owned | Retained; per-occurrence identity preserved |
| No local Money/Currency/ROE subsystem | Retained |
| ETKT and EMD are separate accountable-document aggregates | Retained; P3 activates the EMD lifecycle rather than merging it |
| EMD-A association is coupon-level | Retained — and independently confirmed by Amadeus (S1) |
| Ordering creates no wallet liability for residual/deposit value | Retained (§6) |
| Pending/Unknown is not failure | Retained (§9) |
| No runtime Ledger dependency | Retained |
| `CommercialVersion` ≠ IATA OrderVersion | Retained |
| One `OrderPricingChanged` per committed `PriceChangeSet` | Retained — every servicing money outcome is exactly one committed set |
| `CommercialVersion` +1 per accepted commercial mutation | Retained — each accepted servicing operation is one mutation |
| Documents do not advance `CommercialVersion` | Retained — void/refund/revalidation advance `DocumentVersion`; only the accepted commercial consequence advances `CommercialVersion` |

---

## 13. AeroTech-specific business concepts proposed

**None.**

Every business concept in this design traces to `IATA`, `ATPCO`, `Amadeus`, `Sabre`, `SITA`, `Navitaire` or
`Common PSS practice`. The only AeroTech-specific elements are technical, and all of them already exist and are
frozen: `ServicingOperation`, `CommandReceipt`, `OperationOrderClaim`, provider operation keys, ports,
`CommercialVersion` / `FinancialSequence` / `ObligationVersion` / `ProjectionRevision`, the transactional outbox,
and the typed `OrderView`.

Two candidates were examined and rejected as new concepts:

| Candidate | Resolution |
|---|---|
| "Guarantee / assurance provenance" | It is the industry **waiver / authority evidence** family (`ATPCO` Cat31/33 waiver, `Amadeus` waiver code, `IATA` authority). Adopted as such — §5.3 |
| "Servicing record" | It is the `Amadeus` **Refund Record** capability. Adopted as a projection over existing immutable P2 state, not a new aggregate — §10 |
