# P2-F — Benchmark-Aligned Electronic Miscellaneous Document (EMD)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline: P2-E.1 (`d24f895`), **671 passed / 0 failed**. P0 – P2-E.1 frozen. **P2-G not started.**

Evidence: [`audit/p2/P2-F-TEST-RUN.txt`](audit/p2/P2-F-TEST-RUN.txt) ·
Benchmark audit: [`audit/p2/P2-F-EMD-BENCHMARK-AND-SCOPE-AUDIT.md`](audit/p2/P2-F-EMD-BENCHMARK-AND-SCOPE-AUDIT.md)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `dotnet ef database update` (`DotAirOrderNew`) | `P2FElectronicMiscDocument` applied; no multiple-cascade-path error |
| `AeroTech.Ordering.Domain.Tests` | **436 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **309 passed**, 0 failed (real SQL Server) |
| Total | **745 passed, 0 failed** (baseline 671 → +74, zero regressions) |
| New P2-F tests | 37 domain + 36 persistence = **73** |

## 2. What an EMD is here

```
Order / OrderService / PricingLine        already accepted commercial truth
        ↓  document issuance
ElectronicMiscDocument
    ├── EmdCoupon[]        purpose · RFISC · order service or pricing line · ticket-coupon association
    └── EmdPriceLink[]     frozen coupon → PricingLine (+ optional allocation)
```

The EMD is an accountable **fulfilment artefact**. It never creates the obligation it documents: issuance
advances no `CommercialVersion`, `FinancialSequence` or `ObligationVersion`, creates no `OrderChange`, no
`PriceChangeSet` and no `PricingLine`, and adds no item, service or fare construction — all asserted.

`ElectronicMiscDocument` is a separate aggregate. It does not inherit `ElectronicTicket`, no
`TrafficDocumentV2` was created, and the two document families were not merged into a polymorphic hierarchy.

## 3. EMD-A and EMD-S

`ElectronicMiscDocumentType` is `Associated` (EMD-A) or `Standalone` (EMD-S). An associated document requires a
ticket-coupon association on **every** coupon (2873); a standalone document rejects one (2874). Association is
modelled at coupon level (`EmdCoupon.AssociatedTicketCouponId`), not merely at document level.

Nothing infers the type: the accepted quote supplies it. Neither `ServiceType` nor `ProductType` participates —
a baggage service issued as EMD-S is accepted exactly as readily as one issued as EMD-A (asserted).

## 4. RFIC and RFISC

`ReasonForIssuanceCode` lives once on the document; `ReasonForIssuanceSubCode` lives on each coupon. Both are
opaque validated codes supplied by the accepted quote — never derived from `ProductType`, `ServiceCode` or any
local taxonomy, and never defaulted. Blank values are rejected at sale (2871 / 2872) and at issuance. **One
document carries exactly one RFIC**: two obligations grouped together with different codes fail closed (2881),
while distinct sub-codes coexist correctly under one code.

## 5. Order-owned issuance evidence

`RequiresDocument` + `DocumentKind = ElectronicMiscDocument` is not enough to issue. The new immutable
`OrderServiceEmdIssuanceSnapshot` (EMD type, RFIC, RFISC, associated air service, document group reference,
source system and reference, captured-at) is accepted through the P2-E.1 quote provider and attached during
Add Service. Attaching it to a service that needs no EMD is rejected (2879); an associated profile without an
air service is rejected (2882); a service sold without a profile simply cannot be issued (2880).

The **public Order Change contract is unchanged**: the caller still selects only a quoted offer and its offer
item, and cannot send RFIC, RFISC, EMD type, ticket-coupon association or document number (asserted by contract
test).

## 6. Association resolution

At sale time an EMD-A references an **air `OrderService`**, because the ET coupon does not exist yet. At
issuance that reference resolves to the current non-void ticket coupon covering that service, and the resolved
`TicketCouponId` is persisted on the coupon. Exactly one candidate is required — zero or several fail closed
(2883). Nothing guesses by segment sequence, newest ticket, first ticket, void coupon or document-number
matching, mirroring the scope-aware rule already used for fare construction.

## 7. Value attribution

Coupon value comes only from `Order.ServiceValueAttributions` — direct pricing-line basis or a complete
existing allocation — and each coupon's evidence is frozen into `EmdPriceLink` rows pointing at the real
`PricingLine` (and allocation when present). A service whose only price is an item-level line with no
defensible split is **not** divided: issuance fails closed (2878) rather than producing an accountable document
with fabricated coupon values. The issued total is the sum of that defensible attribution.

## 8. Issuance flow

The existing `POST .../Orders/{id}/Issue` operation was extended rather than replaced — no `/IssueEmd`,
`/CreateEmd` or `/GenerateEmd` endpoint and no new `ServicingOperationKind`. `IssueOrderService` now coordinates
document families and delegates to two collaborators, `ElectronicTicketIssuer` (P1 logic moved intact) and
`ElectronicMiscDocumentIssuer`. No generic workflow framework was built.

```
discover outstanding ET + EMD obligations
  ↓ verify funding coverage against the current ObligationVersion
issue / recover ET where required
  ↓ resolve EMD-A associations against the now-issued ET coupons
issue / recover eligible EMDs      EMD-S needs no ET
```

An already-ticketed order issues only the outstanding EMD and calls the ticket provider zero further times; a
pending ET plus an EMD-A issues the ticket first and then associates; an EMD-S issues with no ET dependency.

## 9. Numbering, recovery and partial irreversibility

EMDs draw from the existing `DocumentStock` aggregate under a configured `EmdDocumentType` — no `EmdStock`. The
number is allocated and persisted **before** the irreversible provider call, under the deterministic document
role `Emd:{group}`, and a retry reuses the same number, the same role and the same provider operation key.

`Pending` and `Unknown` are not failures: the allocation stays reserved, the operation moves to
`AwaitingExternal`, and a retry with the same idempotency key recovers the same document number. A definite
rejection before anything irreversible retires the number and rejects cleanly; a rejection after part of the
operation is already irreversible moves to `NeedsReconciliation` with no compensating fake void. A ticket
confirmed alongside an unknown document leaves the ticket intact and the document recoverable, and the recovery
does not re-issue the ticket.

## 10. Document families stay independent

Marking a service documented goes through one canonical method, `Order.RecordIssuedMiscellaneousDocuments`,
which sets `ElectronicMiscDocumentId` / `EmdCouponId` on exactly the covered service. Nothing is marked before
provider confirmation. ET completeness, the legacy `OrderStatus.Ticketed` derivation and
`IssueEligibilityPolicy` remain ETKT-scoped, so an issued EMD never tickets an unticketed order and a pending
EMD never un-tickets a ticketed one. No global `FullyDocumented` state was introduced. An already-issued ET is
byte-for-byte unchanged by later EMD issuance (asserted). A `Fee`-purpose EMD-S is valid document evidence with
no `OrderService` at all — no service is fabricated to satisfy a lifecycle API.

## 11. Persistence

New relational tables `ElectronicMiscDocuments`, `EmdCoupons`, `EmdPriceLinks` and
`OrderServiceEmdIssuanceSnapshots` — no JSON for core document or coupon relationships and no polymorphic fake
FK. Real foreign keys with `NoAction` cover coupon → document, service coupon → `OrderService`, fee coupon →
`OrderPricingLine`, coupon → `TicketCoupon`, price link → coupon / pricing line / allocation, and snapshot →
associated air service; the Domain additionally proves same-Order membership and semantic type. Document
numbers are globally unique. `GetOrder` now exposes a `MiscellaneousDocuments` block with number, type, RFIC,
status, issued-at, total, currency, provider reference and per-coupon purpose, RFISC, service, association and
value — enough evidence for a future receipt renderer, which P2-F does not build.

## 12. Provider boundary

`IEmdIssuancePort` carries genuine EMD semantics (document number, type, RFIC, per-coupon purpose, RFISC,
attributed value, associated ticket document and coupon number) and contains no Amadeus TSM or SSR DTO. The
ET-shaped `IDocumentIssuancePort` was left untouched rather than widened into a nullable universal request. Two
implementations exist: a deterministic test double and a fail-closed `UnconfiguredEmdIssuanceProvider`
(2884 / HTTP 501). No host connectivity was implemented and no sibling service was touched.

## 13. Not implemented

SSR subsystem, SVC segment, Amadeus TSM or cryptic commands; EMD refund, exchange, void, reassociation or
disassociation; refund mask; service delivery/consumption, DCS; a real EMD host; JetPay, Ledger, SIS,
interline settlement, proration or partner capability matrices; receipt or notification rendering; offer,
pricing or inventory redesign. The audit records the durable decision that **EMD does not use the electronic
ticket revalidation lifecycle**, so a later phase does not invent `RevalidateEmd` by analogy.
