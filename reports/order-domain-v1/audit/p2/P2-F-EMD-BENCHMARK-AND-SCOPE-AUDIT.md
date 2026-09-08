# P2-F — EMD Benchmark and Scope Audit (binding)

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08

Benchmark basis: IATA EMD Resolutions 725f/725g/725h, IATA RFIC/RFISC semantics, the IATA EMD-A / EMD-S
distinction, Amadeus EMD creation/issuance/association behaviour, the existing AeroTech Order / OrderService /
PricingLine truth, and the ONE Order direction that accountable documents are fulfilment artefacts rather than
the commercial source of truth. The standing **Airline Flow Benchmark Rule** (P2-E.1) applies to every
deviation below.

---

## 1. Concept-by-concept comparison

| Concept | IATA / Amadeus benchmark | AeroTech implementation | Deviation? | Reason |
|---|---|---|---|---|
| **EMD-A** | Associated document; each coupon is associated with an electronic ticket coupon | `ElectronicMiscDocumentType.Associated`; every coupon must carry `AssociatedTicketCouponId` (2873) | No | Coupon-level association is the standard shape |
| **EMD-S** | Standalone document; no ET coupon association | `ElectronicMiscDocumentType.Standalone`; a coupon carrying a ticket association is rejected (2874) | No | — |
| **A/S selection** | Determined by carrier/product definition and filing, not by service kind | Supplied by the accepted quote through `AcceptedEmdIssuanceProfile` and snapshotted; nothing infers it from `ServiceType` or `ProductType` (asserted) | No | — |
| **RFIC** | One reason-for-issuance code per document | `ElectronicMiscDocument.ReasonForIssuanceCode`, single-valued; two different codes in one group fail closed (2881) | No | Enforced structurally: the type carries exactly one RFIC property |
| **RFISC** | Sub-code per coupon | `EmdCoupon.ReasonForIssuanceSubCode`, required per coupon (2872); distinct sub-codes coexist under one RFIC | No | Treated as opaque validated codes; never derived from `ServiceCode` in the Domain |
| **Coupon association** | Resolved to a specific, current, non-void ET coupon | Resolved from the explicit `AssociatedAirOrderServiceId` relationship; exactly one non-void candidate required, otherwise fail closed (2883) | No | No sequence guessing, no newest/first pick, no document-number string matching |
| **Service before document** | The service/fee exists and is priced before EMD issuance (modern quote→OrderChange, classic SSR/SVC→TSM) | The EMD-required service is sold through P2-E.1 Order Change / Add Service with its accepted pricing; issuance creates no service, item, pricing line or price change set | No | Classic SSR/SVC origination is deliberately not implemented |
| **Document stock** | Controlled, range-based, accountable numbering | Existing `DocumentStock` aggregate reused with a configured `EmdDocumentType`; no `EmdStock` was created | No | Number allocated and persisted **before** the irreversible provider call; retry reuses number, role and provider operation key |
| **Document value attribution** | The document freezes the value it represents; it does not create value | `EmdPriceLink` freezes coupon → `PricingLine` (+ optional `PricingAllocation`); coupon value comes only from `ServiceValueAttributions`; no defensible attribution → fail closed (2878) | No | No equal split by coupon, segment, beneficiary or service count |
| **Combined / sequential issuance** | Ticket and EMD issued in one servicing action, EMD-A after the ET coupon it references | One `/Issue` operation discovers both families, issues/recovers ET first, then resolves EMD-A associations and issues EMDs; EMD-S has no ET dependency | No | No `/IssueEmd` lifecycle was invented |
| **SSR / SVC relationship** | Classic flows originate ancillaries from SSR/SVC elements and price them into a TSM | **Not implemented.** P2-F consumes the modern accepted-quote path only | Documented gap | SSR is a first-class concept deferred to its own benchmarked slice (P2-E.1 decision); no SSR code or status is invented here |
| **EMD refund** | Real EMD servicing operation | **Not implemented** | Deferred | P3; requires refund evidence model |
| **EMD exchange** | Real EMD servicing operation | **Not implemented** | Deferred | P3 |
| **EMD revalidation** | **Does not exist** — EMD does not use the ET revalidation lifecycle | Not implemented, and explicitly recorded so a later phase does not invent `RevalidateEmd` | No | See §4 |
| **ONE Order direction** | Accountable documents become fulfilment artefacts; the Order is the commercial source of truth | EMD issuance advances no `CommercialVersion`, `FinancialSequence` or `ObligationVersion`, creates no `OrderChange` and no `PriceChangeSet`; only `DocumentVersion` moves | No | The document records which already-accepted value it represents |

## 2. Field-level practicality

Every persisted field carries issuance, audit or servicing value:

| Entity | Kept | Rejected |
|---|---|---|
| `ElectronicMiscDocument` | original / current servicing order, traveller, operation, document number, type, RFIC, issuer carrier, issuing office, authority, issued-at, issued total, currency, provider reference, status summary, document version | the rest of the IATA EMD record schema; TSM number, Amadeus FA element, cryptic command, PNR line number, host coupon indicator |
| `EmdCoupon` | coupon number, purpose, RFISC, order service, pricing line, external value reference, associated ticket coupon, issuance value, currency, status | hotel / baggage / seat detail (owned by `OrderService`), delivery and location fields with no consumer |
| `EmdPriceLink` | document, coupon, pricing line, allocation, attributed value, currency | a polymorphic `DocumentType + DocumentId` table |
| `OrderServiceEmdIssuanceSnapshot` | order service, EMD type, RFIC, RFISC, associated air service, document group reference, source system, source reference, captured-at | any ancillary-catalogue DTO copy |

Coupon status uses a single Ordering-owned value, `OpenForUse`. `Refunded`, `Exchanged`, `Suspended`,
`AirportControl` and `CheckedIn` were **not** added because P2-F implements none of their lifecycles.

## 3. Grouping and provider capability

Multiple obligations share one EMD **only** when the authoritative source supplies the same
`DocumentGroupReference`; otherwise one obligation produces one document. Nothing groups by traveller, item,
service type or RFIC alone, and no automatic grouping engine exists. No universal maximum coupon count,
conjunction behaviour or association capability is hardcoded from any single host — provider capability will be
expressed through the semantic port when a real adapter exists.

## 4. Recorded servicing decision — EMD has no revalidation lifecycle

```
EMD does not use the electronic-ticket revalidation lifecycle.
Later EMD servicing uses exchange or the other applicable EMD operations.
```

This is recorded so P3 does not invent a `RevalidateEmd` operation by analogy with ET. Also deferred to P3, and
deliberately not partially implemented here: EMD disassociation after ticket exchange, reassociation, ticket
revalidation effects on an associated EMD, EMD exchange, EMD refund, EMD void, refund cancellation and exchange
cancellation.

## 5. Deposit and residual value

The domain and persistence support `EmdCouponPurpose.Deposit` and `ResidualValue` because they are genuine
industry EMD-S use cases, and both require an authoritative `ExternalValueReference` (2877). Ordering creates no
`StoredValueBalance`, `VoucherBalance` or `WalletBalance` (asserted by test), and no public API was manufactured
to exercise them — no current producer exists, so domain plus persistence capability is the correct P2-F scope.
The same applies to `Fee`-purpose EMD-S coupons, which are backed by a real `PricingLine` and deliberately do
**not** fabricate an `OrderService`.

## 6. Legacy placeholder

`TrafficDocumentAggregate.EmdDocument` / `EmdCoupon` and the `EmdType` enum are a pre-P1 structure-only
placeholder with no factory flow, persisted only as a TPH discriminator value on the legacy `TrafficDocuments`
table. They are **superseded** by the P2-F `ElectronicMiscDocumentAggregate`. Removing them is a legacy-cleanup
change to a frozen aggregate and is out of P2-F scope; the new aggregate deliberately does not inherit from
`ElectronicTicket` and no `TrafficDocumentV2` was created.

## 7. Not implemented in P2-F

SSR subsystem, SVC segment, Amadeus TSM or cryptic commands; EMD refund, exchange, void, reassociation or
disassociation; refund mask; service delivery/consumption or DCS; a real EMD host; JetPay, Ledger, SIS or
interline settlement, proration and partner capability matrices; receipt rendering or notification; offer,
pricing or inventory redesign. Interline issuer/owner identities are retained only as far as current document
semantics require.
