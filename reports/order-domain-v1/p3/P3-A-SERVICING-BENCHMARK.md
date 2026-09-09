# P3-A — Servicing Benchmark

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg`
**Entry commit (P2 freeze):** `fb090eb768e2a079352b7d0e7ee59cab87003f6d` — verified as HEAD, clean tree, identical to `origin/k8s-stg`.
**Date:** 2026-09-09 · **Status:** binding benchmark baseline for P3. Documentation only; no production code changed.

This document establishes *what the industry actually does*. The design consequences live in
[`P3-A-SERVICING-DESIGN.md`](P3-A-SERVICING-DESIGN.md); the implementation sequence lives in
[`P3-PHASE-PLAN.md`](P3-PHASE-PLAN.md).

---

## 1. Evidence hierarchy and how it was applied

| Layer | Role | Used for |
|---|---|---|
| **IATA** (Offers & Orders, ONE Order, AIDM, Servicing in NDC) | Conceptual backbone | What an Order servicing operation *is*; the separation of reshop → order change → payment/refund → accountable-document modification |
| **ATPCO** (Cat31, Cat33, Cat16, Optional Services) | Calculation semantics | Which party computes voluntary change/refund/penalty outcomes, and what dimensions those outcomes carry |
| **Amadeus Altéa / ATC** | Operational benchmark (primary) | Refund Record, ATC reissue vs revalidation, EMD association lifecycle, waiver handling |
| **SabreSonic / Sabre ticketing** | Operational benchmark (primary) | Coupon status vocabulary and the statuses that block exchange/refund; corrective cancel-refund window; Offers & Orders ancillary cancellation |
| **SITA Horizon** | Operational benchmark (primary) | Module decomposition — which component issues documents vs which computes repricing/refund |
| **Navitaire New Skies** | LCC/ticketless validation | Servicing where **no accountable document exists**; credit-shell value disposition |
| **Travelport** | Supplementary only | Cross-check that void / cancel / refund / exchange are four distinct ticket operations |

**Rule applied throughout:** no executable workflow was derived from IATA/ATPCO alone where Tier-1 operational
evidence existed. Where Tier-1 systems disagree, §3 records the disagreement and the AeroTech normalization.

### 1.1 Source register (this pass)

Retrieved and read in this pass:

| # | Source | Used for | Evidence strength |
|---|---|---|---|
| S1 | Amadeus Service Hub, *EMD: Frequently Asked Questions* — https://servicehub.amadeus.com/c/portal/view-solution/837116/en_US/emd-frequently-asked-questions | EMD association under revalidation vs reissue; disassociation on refund/exchange | **Strong** — explicit operational rules |
| S2 | Amadeus Service Hub, *How to re-associate an EMD to a ticket (Cryptic)* — https://servicehub.amadeus.com/c/portal/view-solution/866006/how-to-re-associate-an-emd-to-a-ticket-cryptic- | Reassociation is a first-class operator action | Strong |
| S3 | Amadeus Service Hub, *ATC Reissue: How to revalidate an e-ticket* — https://servicehub.amadeus.com/c/portal/view-solution/807866/amadeus-ticket-changer-atc-reissue-how-to-revalidate-an-e-ticket-cryptic- | Revalidation = change without new document | Strong |
| S4 | Amadeus Service Hub, *XX ETKT: REVALIDATION REQUEST DENIED* — https://live-travel.community.amadeus.com/c/portal/view-solution/836892/xx-etkt-revalidation-request-denied | Revalidation eligibility is provider-decided and can be refused | Strong |
| S5 | Amadeus Service Hub, *ATC Reissue: How to reissue an e-ticket* — https://servicehub.amadeus.com/c/portal/view-solution/839096/amadeus-ticket-changer-atc-reissue-how-to-reissue-an-e-ticket-cryptic- | Reissue computed from ATPCO Cat31 + original ticket data | Strong |
| S6 | Amadeus Service Hub, *How to refund a ticket* / *How to redisplay a refund record* — https://amadeusdev.service-now.com/csm/en/how-to-refund-a-ticket-cryptic?id=kb_article&sysparm_article=KB0017923 | Refund Record component breakdown; non-editable after processing | Strong |
| S7 | Amadeus Service Hub, *How to refund an EMD (Cryptic)* — https://servicehub.amadeus.com/c/portal/view-solution/830411/how-to-refund-an-emd-cryptic- | EMD refund is its own document operation | Strong |
| S8 | Sabre Developer, *EticketCouponLLSRQ / eTicket coupon status reference* — https://developer.sabre.com/soap-api/issue-air-ticket-soap/2.14.0/help-documentation/eticketcouponllsrq.html | Coupon status vocabulary; statuses blocking exchange/refund | Strong |
| S9 | Sabre, *Cancel Refund / Void Exchange* — https://www.scribd.com/document/752518473/Cancel-Refund-Void-Exchange-Sabre-Holdings | Corrective cancel-refund same-day window; coupon returns to OPEN; voided exchange → REAC | **Moderate** — secondary host of a Sabre document |
| S10 | Sabre, *Offers and Orders APIs user guide v1.6* — https://developer.sabre.com/sites/default/files/2024-04/Sabre%20Offers%20and%20Orders%20APIs%20user%20guide%20v1.6.pdf | Order-centric ancillary cancellation / fulfilled-EMD outcomes | Strong |
| S11 | AltexSoft, *Airline Reservation Systems and Passenger Service Systems* — https://www.altexsoft.com/blog/airline-reservation-systems-passenger-service-systems/ | SITA Horizon module decomposition; Navitaire New Skies scope | **Moderate** — reputable secondary survey |
| S12 | SITA, *Horizon — A New Era in Passenger Services* (brochure) — https://na.eventscloud.com/file_uploads/8bfbe87da088869f453591ab60b5ec0c_2145SITAHorizonBrochureLOWSingles.pdf | Horizon positioning (fetch returned 403 this pass; used via S11 summary) | **Weak** — not directly readable in this pass |
| S13 | Navitaire, *New Skies* product page — https://www.navitaire.com/p_new_skies.aspx | Ticketless model | Moderate |
| S14 | Frontier Airlines, *Navitaire Cutover: Agency FAQ* — https://www.flyfrontier.com/media/1328/frontier-ta-faq-aug2015.pdf | 24-hour refund window then Credit Shell; named-traveler, 1-year validity | **Moderate** — carrier policy on a Navitaire platform, not vendor doc (PDF not machine-readable this pass; used via search summary) |
| S15 | IATA, *Fulfilment with Orders (ONE Order)* — https://www.iata.org/en/programs/airline-distribution/retailing/one-order/ and IATA Developer Portal — https://developer.iata.org/en/one-order/ | Order servicing = reshop → order change → payments/refunds → modify accountable documents | Strong |
| S16 | IATA, *ONE Order Transition Study* — https://www.iata.org/contentassets/72cbd60393ff42b5975d90ce9e049a7d/one-order-transition-study.pdf | Gradual phase-out of PNR / ETKT / EMD toward a single order reference | Strong |
| S17 | Travelport, *Exchange, Refund, and Void Guide* — https://support.travelport.com/webhelp/JSONAPIs/Airv11/Content/Air11/General/ExchangeRefundGuide.htm | Supplementary confirmation that void/refund/exchange are distinct | Supplementary |

Carried forward from the existing repository design corpus (`docs/order-domain-design-v1/12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md`),
whose IATA/ATPCO citation set (IATA AIDM Order Item, Cancel/Change Restrictions, Reusable Indicator, Order Penalty
Information, Coupon; ATPCO Cat16/31/33 and Optional Services) is reused rather than re-derived. That document
predates the P2 code and does not cover SITA or Navitaire; this document supersedes it as the P3 benchmark of
record.

### 1.2 Verbatim findings that drive design decisions

> "In case of an e-ticket **revalidation**, the EMD **remains associated** to the e-ticket. In case of an e-ticket
> **reissue**, the EMD previously associated to the e-ticket **is disassociated**. Then this EMD has to be refunded
> or exchanged against a new EMD that will be associated at issuance time to the new e-ticket." — **Amadeus (S1)**

> "The refunded EMD coupons are disassociated from the associated e-ticket coupons." — **Amadeus (S1)**
> (association and disassociation are stated at **coupon** level, not document level)

> "Horizon **Ticketing** is responsible for issuing both e-tickets and EMDs." … "**Airfare Price** ensures proper
> application of fare rules … and automates **ticket repricing and refunding**." — **SITA (S11/S12)**
> (document issuance and refund calculation are *different modules*)

> Statuses that do not allow exchange or refund: **LFTD, USED, EXCH, RFND, VOID, NOGO, PTRX, FIM.** — **Sabre (S8)**

> A cancel-refund must occur **the same day** the refund was created, in the same PCC, and at least one coupon must
> show status **RFND**; the system then returns the coupon to **OPEN** (voided exchange → **REAC**). — **Sabre (S9)**

> Order servicing responsibilities include "handling re-shopping for an Offer, through to applying any changes to
> the Order, processing further payments or refunds, and **modifying accountable documents**." — **IATA (S15)**

> After the Navitaire conversion a full refund is available only within 24 hours of booking creation; afterwards
> "any amounts due back to the customer will be placed on a **Credit Shell** valid for one year … used by the named
> traveler." — **Navitaire-platform carrier policy (S14)**

---

## 2. What the pushed P2 code actually provides

Read directly from `fb090eb`. This is authoritative over every design document, including the one above.

| Element | Actual state at P2 freeze |
|---|---|
| `ServicingOperationKind` | `CreateOrder, Reserve, RequestPayment, Issue, Cancel, VoidDocument, Split, Expire, AddService` — **no Refund, Exchange, Reissue or Revalidate** |
| `ServicingOperationStatus` | `Prepared, Executing, AwaitingExternal, ReadyToFinalize, Committed, Completed, Rejected, Compensating, NeedsReconciliation` — already sufficient for servicing |
| `OrderChangeType` | `Create, AddProduct, Cancel, VoluntaryChange, Exchange, Reaccommodation, InvoluntaryChange, NameCorrection, Split, ManualAdjustment, Close` — servicing vocabulary already reserved |
| `PriceChangeReason` | `OriginalSale, AddProduct, Reprice, VoluntaryChange, Exchange, InvoluntaryChange, Cancellation, Refund, Void, ManualAdjustment, Correction, SplitTransfer` — already reserved |
| `PricingComponentType` | includes **`Penalty = 8`**, `Commission = 9`, `Adjustment = 10` |
| `PricingLineRole` | `Original, Reversal, Adjustment, **Transfer**` |
| `PricingSource` | `OfferProvider, PricingEngine, Supplier, **Manual**` — manual-override provenance already exists |
| `RefundabilityRule` | `FullIfAllUnused = 1, NonRefundable = 3, Refundable = 4, Conditional = 5` — **numeric 2 is a hole; must not be renumbered** |
| `ProviderOperationOutcome` | `Confirmed, Rejected, Pending, Unknown` |
| `EligibilityOutcome` | `Allowed, Denied, PendingEvidence` |
| `ElectronicTicket` | `OriginalOrderId` + `CurrentServicingOrderId`, `VoidDeadline`, `IssuedTotal`, `StatusSummary`, `DocumentVersion`, `PriceLinks`; **`Void(clock)` exists but has no caller anywhere in `src/`** |
| `ElectronicTicketStatus` | `Issued, PartiallyUsed, Used, Voided, Exchanged, Refunded, Suspended` — declared; only `Issued`/`Voided` reachable |
| `TicketCoupon` | `OrderServiceId` **and** `CurrentOrderServiceId`, `IssuedSegment` snapshot, `FareBasisSnapshot`, `IssuanceValue`, `FinancialStatus`, **`ControlStatus` (`Local/External/ReleasePending/Unknown`)**, `ProviderCouponStatusCode`; only `Void()` mutates |
| `ElectronicMiscDocument` | Issue + `RecordProviderConfirmation` only. **No void, refund, exchange or reassociation method** |
| `ElectronicMiscDocumentStatus` | **`Issued = 1` only** |
| `EmdCouponStatus` | **`OpenForUse = 1` only** |
| `EmdCoupon` | `AssociatedTicketCouponId` (**coupon-level**, matching Amadeus S1), `Purpose ∈ {Service, Fee, Deposit, ResidualValue}`, `ExternalValueReference`; status is immutable after issue |
| Legacy `TrafficDocument` | Separate aggregate with `Void`, `MarkVoidUnconfirmed`, `EnsureCanBeVoided`, `CouponStatus` (IATA-letter enum), `EmdDocument`/`EmdCoupon` children |
| Live Cancel / Void path | `OrderCancelService` and `TrafficDocumentVoidService` — run on `IDistributedLock` + `FulfillmentTask` planner/executor against **legacy `TrafficDocument`**, *not* the P0 `OrderOperationCoordinator` and *not* the P2 `ElectronicTicket` |
| Modern pre-ticket cancel | `WithdrawOrderService` → `OrderOperationCoordinator` + `IReservationPort` + `IFundingCoveragePort` + `WithdrawEligibilityPolicy` + `Order.WithdrawBeforeTicketing`, under `ServicingOperationKind.Cancel` |
| Commercial reversal | `Order.Termination.cs` — `ReverseOutstandingValue` (whole order) and `ReverseServiceValue` (scoped), both computing amounts **locally**, the scoped one **derived from `PricingAllocation` values**, and both stamped `PricingSource.PricingEngine` |

### 2.1 Findings that constrain P3

| # | Finding | Consequence |
|---|---|---|
| **B-1** | `Order.ReverseServiceValue` derives reversal amounts from `line.CommercialAllocations()` filtered by service id. | Directly collides with the frozen rule *"refund entitlement must not be inferred from allocations."* This path is a **pre-ticket / void-time commercial reversal of unconsumed sale value**, not a refund entitlement — but it must **not** be extended into P3-D Refund. See design §9.1. |
| **B-2** | Those locally computed reversals are stamped `PricingSource.PricingEngine`. | Provenance is inaccurate: Ordering computed them. P3 should stamp locally derived reversals honestly and reserve `PricingEngine` for results actually returned by the pricing owner. |
| **B-3** | Two parallel servicing rails exist (legacy `FulfillmentTask` + `TrafficDocument` vs P0 `OrderOperationCoordinator` + P2 `ElectronicTicket`). The **live** `{id}/Cancel` and `{id}/Documents/{documentId}/Void` endpoints run on the legacy rail. | P3-B and P3-C must unify onto the P0 rail before Refund/Exchange are built, otherwise servicing splits permanently. |
| **B-4** | `ElectronicTicket.Void()` has no caller; `ElectronicMiscDocument` has no lifecycle method at all. | The P2 accountable-document foundation is sound but **inert**. P3-C is the phase that activates it. |
| **B-5** | `ElectronicTicket` has no successor/predecessor document link. | P3-F must add explicit lineage (`ExchangedFrom` / `SupersededBy`) — required by Amadeus/Sabre/IATA reissue semantics. |
| **B-6** | `TicketCoupon.ControlStatus` exists but nothing reads it. | Amadeus/Sabre both gate document action on control; P3-C/P3-H must make it a precondition rather than decoration. |
| **B-7** | `Order.Cancel` reuses `VoidReason` for cancellation. | Semantic overload: cancel reasons and void reasons are different industry vocabularies. Flagged, not fixed in P3-A. |
| **B-8** | `ServicingOperationKind` lacks Refund/Exchange/Reissue/Revalidate. | Adding them is a **wire-contract change** (`Contracts/AeroTech.Messages`); it must be a single deliberate, append-only extension, not incremental drift. |

---

## 3. Where Tier-1 systems agree and disagree

### 3.1 Agreement (safe to normalize)

| Point | Amadeus | Sabre | SITA | Navitaire | Consequence |
|---|---|---|---|---|---|
| Void, refund, exchange and revalidation are **four distinct operations** | Yes | Yes | Yes | N/A (ticketless) | Never expose one generic "cancel" that silently means all four |
| Refund/change amounts are computed by a **fare/pricing** component, not the order/booking component | ATC + Cat31/33 | Cat31/33 | `Airfare Price` module | Fare Manager (ATPCO) | Ordering never computes a refund |
| Document issuance is a **different responsibility** from refund calculation | Ticketing vs ATC/pricing | Ticketing vs pricing | `Horizon Ticketing` vs `Airfare Price` | n/a | Separate document lifecycle from pricing decision |
| **Coupon status gates** whether a document action is legal | Yes | Explicit list (S8) | Yes | n/a | Coupon state is a hard precondition |
| **Control** (airport/external) must be resolved before unsafe document action | Yes | Yes | Yes | n/a | `TicketCoupon.ControlStatus` becomes a real precondition |
| Revalidation **does not create a replacement document** | Yes (S3) | Yes | Yes | n/a | Revalidation is a document outcome, not an exchange |
| Reissue creates a **successor document with lineage** | Yes (S5) | Yes | Yes | n/a | Never overwrite the original document |
| A processed refund record is **redisplayable and non-editable** | Refund Record (S6) | Yes | Yes | n/a | Immutable servicing record |
| Corrective reversal of a refund is a **new operation with its own provider evidence** | Yes | Same-day cancel-refund (S9) | Yes | n/a | Never delete servicing history |

### 3.2 Disagreement / variance (must stay configurable, never hard-coded)

| Point | Variance observed | AeroTech normalization |
|---|---|---|
| **Void window** | Amadeus/Sabre/Travelport all treat void as typically same-day but issuer- and market-dependent; Sabre's *cancel-refund* window is explicitly same-day (S9) | `ElectronicTicket.VoidDeadline` already exists per document. Eligibility is asked of the issuer/provider port; **no hard-coded "same day" rule** |
| **Revalidation eligibility** | Amadeus: depends on fare rules, routing, booking class, and can be refused outright (S3, S4). Sabre/SITA: issuer-capability driven | Never computed locally. `EligibilityOutcome.PendingEvidence` when the provider has not answered |
| **Penalty collection treatment** | Netted from refund, collected separately, added to the replacement document, or documented via EMD-S — all observed | Ordering records which treatment the source returned; it never assumes one |
| **Refund destination** | Original form of payment, voucher, residual value, credit shell / travel bank — all observed; Navitaire's default after 24h is a **Credit Shell**, not FOP (S14) | Ordering records the approved disposition **reference only**. It never creates the value instrument |
| **Accountable document existence** | Amadeus/Sabre/SITA are document-centric (ETKT + EMD). Navitaire is **ticketless** — refunds and credits exist with no document at all | Servicing flows must be document-*aware*, not document-*required*. The pre-ticket cancel path already proves this in P2 |
| **EMD on reissue** | Amadeus is explicit: disassociate, then refund or exchange into a new EMD (S1). Sabre Offers & Orders expresses the same outcome in order terms (S10) | Adopt the Amadeus rule as the normalized behaviour; express the *outcome*, not the cryptic command |
| **Coupon status vocabulary** | Sabre uses `LFTD/USED/EXCH/RFND/VOID/NOGO/PTRX/FIM`; IATA uses single letters (`O/F/E/R/V/…`); the repo already has both `CouponStatus` (IATA letters, legacy) and `TicketCouponFinancialStatus` (Ordering-owned) | Keep the Ordering-owned `TicketCouponFinancialStatus` as truth; retain the raw provider code in the existing `TicketCoupon.ProviderCouponStatusCode` field. **Do not** import a vendor status list into the domain |

---

## 4. Benchmark matrices

Legend for **AeroTech decision**: `[ADOPT]` direct industry semantic adoption · `[NORMALIZE]` provider-neutral
normalization of a common PSS behaviour · `[TECH]` explicitly documented AeroTech technical abstraction.

### 4.1 Pre-ticket cancel / service removal

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Nature of the operation | Order/OrderItem cancellation before fulfilment; no accountable document exists | Cancel booking / cancel TST before ticketing | Cancel segments / Offers & Orders order cancel | `Horizon Reservations` cancel | Cancel booking; ticketless throughout | `[ADOPT]` Commercial cancellation distinct from refund; already implemented as `Order.WithdrawBeforeTicketing` under `ServicingOperationKind.Cancel` | No |
| Partial scope | OrderItem/Service level cancel is standard | Yes | Yes (fulfilled vs unfulfilled ancillary, S10) | Yes | Yes | `[ADOPT]` Cancel takes an explicit service/item scope. P2 already passes `serviceIds` | No |
| Capacity release | Inventory release is a separate provider action | Yes | Yes | Yes | Yes | `[ADOPT]` `IReservationPort.ReleaseAsync` under a stable provider operation key — already implemented | No |
| Cancellation fee | ATPCO Cat16/Cat33 may levy a fee even pre-ticket | Yes | Yes | `Airfare Price` | Yes (LCC cancel fee is routine) | `[ADOPT]` Fee is a source-returned `PricingComponentType.Fee`/`Penalty` line; Ordering never computes it | No |
| Money already captured | Refund is a payment-owner action correlated to the cancelled obligation | Yes | Yes | Yes | Credit shell (S14) | `[ADOPT]` Payment/value execution is out of Ordering; Ordering records the approved disposition reference | No |
| Value reversal of unconsumed sale | Not an industry "refund"; an order-value correction | Yes | Yes | Yes | Yes | `[TECH]` `Order.ReverseOutstandingValue` — but see finding **B-1**/**B-2**: scoped reversal must stop deriving amounts from allocations | **Yes — documented, see design §9.1** |

### 4.2 Void (ETKT and EMD)

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Definition | Cancel an accountable document as if never issued | Void e-ticket / void EMD | Void ticket; void exchange → coupon `REAC` (S9) | `Horizon Ticketing` void | **N/A — no document** | `[ADOPT]` Void is a document operation, never a synonym for cancel or refund | No |
| Window | Issuer/market dependent, commonly same-day | Same-day typical, issuer dependent | Same-day for cancel-refund (S9) | Issuer dependent | n/a | `[NORMALIZE]` Per-document `VoidDeadline` (already on `ElectronicTicket`) **plus** issuer/provider eligibility answer. No hard-coded rule | No |
| Granularity | Whole document | Whole document | Whole document | Whole document | n/a | `[ADOPT]` `ElectronicTicket.Void()` already voids the whole document from `Issued` only. Correct — keep | No |
| Coupon precondition | Only unused/open coupons | Yes | `USED/LFTD/EXCH/RFND/VOID/NOGO/PTRX/FIM` block (S8) | Yes | n/a | `[NORMALIZE]` Precondition expressed over Ordering-owned `TicketCouponFinancialStatus` + `ControlStatus`; raw provider code retained separately | No |
| EMD void | EMD is independently voidable | Yes (S1, S7) | Yes (S10) | Yes | n/a | `[ADOPT]` EMD void is its **own** operation on `ElectronicMiscDocument`, not a side effect of ticket void | No — but **not implemented** (finding B-4) |
| Money consequence | Void may release an authorization or require a refund depending on payment state | Yes | Yes | Yes | n/a | `[ADOPT]` Ordering asks the payment owner; document void **never** proves a capture was reversed | No |
| Window expired | Must be handled as refund, not forced into void | Yes | Yes | Yes | n/a | `[ADOPT]` Never force a document into Void to simplify accounting | No |

### 4.3 Refund — quote / calculation

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Who calculates | ATPCO **Cat33** automates refund; Cat16 supplies penalty | ATC / refund pricing | Refund pricing | **`Airfare Price` "automates ticket repricing and refunding"** (S11) | Fare Manager (ATPCO) | `[ADOPT]` A dedicated pricing/refund owner calculates. **Ordering implements no ATPCO formula** | No |
| Quote is side-effect free | Reshop/quote does not mutate the Order | Yes | Yes | Yes | Yes | `[ADOPT]` Quote → accept → execute, mirroring the P2-E.1 `AcceptSelectedQuotedOffer` pattern already in the code | No |
| Result decomposition | Fare paid / fare used / fare refund / tax refund / penalty / fee / commission / FOP | **Refund Record** carries exactly these (S6) | Equivalent | Equivalent | Credit amount | `[ADOPT]` Persist each accepted component as its own `OrderPricingLine` with its existing `PricingComponentType`; never collapse to one signed delta | No |
| Redisplay | Processed record is redisplayable and non-editable | Yes (S6) | Yes | Yes | Yes | `[ADOPT]` Immutable servicing record; P2 `PriceChangeSet` immutability already provides the spine | No |
| Staleness | Quote expires | Yes | Yes | Yes | Yes | `[TECH]` Reuse the frozen `ExpectedCommercialVersion` + operation-fingerprint mechanism. Ordering never silently recalculates a stale quote | No |

### 4.4 Refund — full unused, partial after use, tax-only

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Full unused | All coupons open ⇒ refund per Cat33 | Yes | Requires all coupons refundable status | Yes | Full refund only inside 24h, then Credit Shell (S14) | `[ADOPT]` Eligibility from coupon state + source approval | No |
| Partially used | Cat33 may **reprice the flown portion** to derive unused value | Refund Record separates *fare used* from *fare refund* (S6) | Partial refund supported | `Airfare Price` repricing | n/a | `[ADOPT]` `refund ≠ original total − allocations of delivered services`. Ordering stores the source's used/unused valuation | No |
| Tax-only refund | Non-refundable fare can still carry refundable taxes | Yes | Yes | Yes | Government-tax refund | `[ADOPT]` Each tax occurrence keeps its own accepted treatment. P2 already preserves per-occurrence tax identity (`SourceLineRef` + `OccurrenceKey`) | No |
| Coupon consequence | Refunded coupons move to a refunded status | `RFND` | `RFND`; refund reversal returns coupon to `OPEN` (S9) | Yes | n/a | `[ADOPT]` `TicketCouponFinancialStatus.Refunded` (already declared) | No |
| Corrective reversal | Allowed under limited provider conditions | Refund cancellation | Same-day, same PCC, coupon in `RFND` (S9) | Yes | n/a | `[ADOPT]` A **new** corrective operation with its own provider evidence; never a status flip | No |

### 4.5 Penalty, commission, waiver, manual override

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Penalty source | ATPCO **Cat16/31/33**; IATA *Order Penalty Information* | ATC returns penalty | Returns penalty | `Airfare Price` | Fare Manager | `[ADOPT]` Penalty is a **source-returned applied result**, recorded as `PricingComponentType.Penalty` (already exists). No rule engine | No |
| Netted vs separately collected | IATA distinguishes both | Both observed | Both observed | Both | Both | `[ADOPT]` Treatment comes from the source; never assumed | No |
| Penalty is not a reversal | Penalty is a new charge, not negative fare | Yes | Yes | Yes | Yes | `[ADOPT]` `PricingLineRole.Original` debit with `ComponentType.Penalty`, never `Role.Reversal` | No |
| Tax on penalty | Market-dependent; source-calculated | Yes | Yes | Yes | Yes | `[ADOPT]` Keep source tax occurrence distinguishable; never compute tax-on-penalty | No |
| Refund/reissue commission | Differs from issue commission | Refund Record has its own commission (S6) | Yes | Yes | n/a | `[ADOPT]` Source-provided; stays `PricingEffect.SettlementOnly` and never moves customer balance (frozen P2 rule) | No |
| Waiver / authority | Cat31/33 waiver; carrier authority code | Waiver code on refund/reissue | Waiver code | Yes | Yes | `[ADOPT]` A **waived penalty is not a zero-valued penalty**. Record waiver reference + authority + actor as provenance | No |
| Automatic vs manual | Automated (ATC/Cat31-33) vs manual/agency-priced servicing both exist industry-wide | ATC automatic; manual refund possible | Both | Both | Both | `[NORMALIZE]` Both supported. Automatic ⇒ `PricingSource.PricingEngine`; manual ⇒ **`PricingSource.Manual`** (already exists) with mandatory actor + authority reference | No |
| Manual override provenance | Audit requirement | Yes | Yes | Yes | Yes | `[ADOPT]` `PricingSource.Manual` + `OrderChange.ActorId`/`ActorScope`/`ExternalReference` — all already on the P2 entity | No |
| "Guarantee/assurance" provenance | No distinct industry concept; this is the **waiver/authority evidence** family | Waiver code | Waiver code | Authority | Authority | `[ADOPT]` Modelled as waiver/authority evidence. **No new AeroTech business concept is introduced** | No |

### 4.6 Voluntary change and revalidation

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Change calculation | ATPCO **Cat31** voluntary change | ATC reissue uses Cat31 + original ticket data (S5) | Cat31 | `Airfare Price` | Fare Manager | `[ADOPT]` Source-owned. Ordering supplies historical fare construction + sale context (P2-C already persists it) | No |
| Reshop is side-effect free | IATA OrderReshop | Yes | Yes | Yes | Yes | `[ADOPT]` Quote/accept split | No |
| Revalidation definition | Rebind existing document without issuing a new one | "date or time … changed … without issuing a new ticket" (S3) | Yes | Yes | **N/A** | `[ADOPT]` Revalidation is **one possible document outcome** of an accepted change | No |
| Revalidation eligibility | Rule + issuer + coupon dependent | Fare must allow change; some booking classes force reissue; request can be **denied** (S3, S4) | Issuer dependent | Issuer dependent | n/a | `[ADOPT]` Asked of the provider. Never inferred from a local rule table. `EligibilityOutcome.PendingEvidence` when unresolved | No |
| Document identity | Original document remains accountable | Yes | Yes | Yes | n/a | `[ADOPT]` No replacement document is fabricated; `DocumentVersion` increments | No |
| Coupon rebinding | Coupon points at the new flight | Yes | Yes | Yes | n/a | `[ADOPT]` **`TicketCoupon.CurrentOrderServiceId` already exists for exactly this** | No |
| Fallback to reissue | Not automatic | Explicitly not automatic (S3) | Not automatic | Not automatic | n/a | `[ADOPT]` Only if the accepted change plan permits the alternative | No |
| Monetary outcome shapes | Even / add-collect / refund / residual / mixed, penalty netted or separate | All observed | All observed | All observed | Credit shell | `[ADOPT]` Never collapsed into one signed difference | No |

### 4.7 Exchange / reissue

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Successor document | Reissue creates a new document linked to the old | ATC reissue (S5) | Reissue; voided exchange → `REAC` (S9) | `Horizon Ticketing` | **N/A** | `[ADOPT]` New `ElectronicTicket` + explicit lineage. **Lineage fields do not exist yet — finding B-5** | No |
| Old document | Preserved, marked exchanged | `EXCH` coupon status | `EXCH` (S8) | Yes | n/a | `[ADOPT]` `ElectronicTicketStatus.Exchanged` + `TicketCouponFinancialStatus.Exchanged` — both already declared | No |
| Component breakdown | Old fare, new fare, difference, old/new tax, penalty, add-collect, residual, FOP, commission | All separately exposed by ATC | Equivalent | Equivalent | n/a | `[ADOPT]` Each as its own pricing line; `PricingLineRole.Transfer` (already exists) carries old→new value transfer | No |
| Partially used exchange | Unflown scope only; used history stays in pricing context | Yes | Yes | Yes | n/a | `[ADOPT]` Retain source fare construction and used-service history (P2-C/P2-D already do) | No |
| Never delete | Old document is never overwritten | Yes | Yes | Yes | n/a | `[ADOPT]` Append-only document history | No |

### 4.8 EMD-A / EMD-S servicing

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Association granularity | IATA AIDM: EMD coupon ↔ ticket coupon | "refunded EMD **coupons** are disassociated from the associated e-ticket **coupons**" (S1) | Coupon level | Coupon level | n/a | `[ADOPT]` Already frozen in P2: `EmdCoupon.AssociatedTicketCouponId` | No |
| **On ticket revalidation** | Not specified at this granularity | **EMD remains associated** (S1) | Consistent | Consistent | n/a | `[ADOPT]` Revalidation preserves EMD association | No |
| **On ticket reissue** | Not specified at this granularity | **EMD is disassociated**, then must be refunded or exchanged into a new EMD associated at issuance to the new ticket (S1) | Equivalent order-level outcome (S10) | Consistent | n/a | `[ADOPT]` Normalized as an explicit dependent-document consequence of reissue | No |
| Reassociation | Operator action | Distinct documented action (S2) | Yes | Yes | n/a | `[ADOPT]` A first-class servicing outcome, not a hidden side effect | No |
| EMD refund / void / exchange | ATPCO Optional Services refundability/reusability | Own operations (S1, S7) | Yes (S10) | Yes | n/a | `[ADOPT]` Own document operations on `ElectronicMiscDocument`. **None implemented — finding B-4** | No |
| EMD-S for fee/penalty | Standalone fee/penalty documentation | Yes | Yes | Yes | n/a | `[ADOPT]` Already frozen in P2: a `Fee` coupon requires a pricing line and **must not** invent an `OrderService` | No |
| Deposit / residual value | Reusable value instrument | Yes | Yes | Yes | Credit Shell (S14) | `[ADOPT]` `EmdCouponPurpose.Deposit`/`ResidualValue` reference an **external** value instrument. Frozen P2 rule: Ordering creates **no** wallet liability | No |
| Dependent-ancillary evaluation on flight change | ATPCO Optional Services + IATA servicing | Yes | Yes (S10) | Yes | n/a | `[ADOPT]` Every dependent ancillary is explicitly evaluated: keep / reassociate / reissue / refund / residual / cancel / manual review | No |

### 4.9 Voluntary vs involuntary, no-show, SSR, uncertainty

| Concern | IATA/ATPCO semantic | Amadeus | Sabre | SITA | Navitaire | AeroTech decision | Deviation? |
|---|---|---|---|---|---|---|---|
| Voluntary vs involuntary | Distinct authority; Cat31/33 are **voluntary** | Involuntary reissue is a distinct ATC flow | Schedule Change Web Service | Yes | Yes | `[ADOPT]` `OrderChangeType.InvoluntaryChange`/`Reaccommodation` already exist. Authority + reason preserved | No |
| Involuntary ≠ free | Even exchange and fee waiver are common but not universal | Yes | Yes | Yes | Yes | `[ADOPT]` Never hard-code "involuntary means zero" | No |
| No-show | Operational observation; Cat16/31/33 define failure-to-use penalties separately | Yes | `NOGO`/`LFTD` coupon states (S8) | Yes | Yes | `[ADOPT]` A DCS no-show observation **never** auto-cancels, auto-forfeits or auto-penalizes. Each consequence needs an explicit servicing decision | No |
| No-show boundary in P3 | — | — | — | — | — | `[TECH]` **Out of P3 scope**: DCS is not integrated. P3 only guarantees that nothing infers monetary loss from delivery status | No |
| SSR | IATA SSR is a reservation communication element, not a priced service | Yes | Yes | Yes | Yes | `[ADOPT]` SSR remains **out of scope**, as already asserted by the frozen `SsrBoundaryTests`. Servicing an ancillary never creates an SSR record | No |
| Provider/host uncertainty | Not specified by IATA/ATPCO | Reconcile before retry | Reconcile before retry | Reconcile | n/a | `[TECH]` Frozen P0 semantics: `ProviderOperationOutcome.Pending/Unknown` is **not** failure; reconcile under the same durable operation key. `ServicingOperationStatus.NeedsReconciliation` already exists | No |
| Document control conflict | Airport/external control blocks action | Yes | Yes | Yes | n/a | `[ADOPT]` `TicketCoupon.ControlStatus` becomes a hard precondition — finding B-6 | No |

---

## 5. Operational sequences

Each sequence is the **normalized** Tier-1 flow. `‖` marks a step that is out of Ordering's ownership.

### 5.1 Pre-ticket cancel / service removal
```
1. resolve explicit service/item scope + dependent services
2. evaluate eligibility (order state, document existence, coupon state)   [Ordering]
3. obtain cancellation treatment / fee                                  ‖ pricing owner
4. release external capacity under a stable provider operation key      ‖ inventory provider
5. release unused funding authorization                                 ‖ payment owner
6. commit commercial cancellation + accepted price change
7. project + publish
```

### 5.2 Void (ETKT or EMD)
```
1. resolve document + coupon state + control status                       [Ordering]
2. ask issuer/provider whether this document is voidable now            ‖ document provider
   -> not voidable  => STOP; re-evaluate as refund. Never force a void
3. persist void intent under a stable provider operation key
4. execute void                                                         ‖ document provider
   -> Pending/Unknown => NeedsReconciliation, same operation key, no retry as a new economic action
5. record confirmed document outcome (ETKT and/or EMD)
6. apply the accepted commercial reversal treatment
7. request the corresponding monetary action                            ‖ payment owner
```

### 5.3 Refund
```
1. resolve refundable scope from coupon/document state                    [Ordering]
2. request refund quote with original sale + fare construction + used history
                                                                        ‖ pricing/refund owner
3. present decomposed quote (fare paid / used / refundable, tax occurrences,
   penalty, fee, commission, disposition) — side-effect free
4. customer/agent accepts an exact quote version
5. recheck CommercialVersion + coupon/control state before irreversible work
6. execute document refund                                              ‖ document provider
7. record coupon -> Refunded, document -> Refunded
8. commit accepted refund pricing lines (one PriceChangeSet)
9. request value movement to the approved disposition                   ‖ payment / value owner
10. finalize immutable servicing record
```

### 5.4 Voluntary change → revalidation *or* reissue
```
1. reshop / change quote                                                ‖ pricing owner (Cat31)
2. accept exact quote version
3. protect replacement capacity                                         ‖ inventory provider
4. protect additional funding if add-collect                            ‖ payment owner
5. ask the document provider which document outcome is available:
     revalidation  -> same document, coupon rebound, DocumentVersion++
     reissue       -> successor document + lineage, old coupons -> Exchanged
     denied        -> PendingEvidence / blocked; never silently downgrade
6. execute the document plan                                            ‖ document provider
7. commit commercial replacement + accepted price delta (one PriceChangeSet)
8. evaluate every dependent ancillary/EMD explicitly (§5.6)
9. complete value movement                                              ‖ payment owner
10. finalize servicing record
```

### 5.5 Exchange / reissue lineage recorded
```
original document + coupon refs
successor document + coupon refs
affected order service refs
servicing operation / order change ref
accepted pricing decision ref
provider/issuer confirmation ref
voluntary or involuntary authority context
```

### 5.6 Dependent EMD consequence of a flight change *(Amadeus S1, normalized)*
```
change outcome = REVALIDATION
    -> EMD-A remains associated. No EMD action.

change outcome = REISSUE
    -> EMD-A is disassociated from the old ticket coupon
    -> for each affected EMD coupon, the source decides exactly one of:
         refund
         exchange into a new EMD, associated at issuance to the new ticket coupon
         retain as residual/reusable value        (external instrument)
         cancel without refund
         manual review
    -> a new EMD is associated at issuance time, never retro-fitted

EMD refunded or exchanged (any trigger)
    -> that EMD coupon is disassociated from its e-ticket coupon
```

### 5.7 Provider uncertainty reconciliation *(frozen P0 semantics)*
```
Confirmed -> apply outcome
Rejected  -> reject operation, release claim, no commercial mutation
Pending   -> AwaitingExternal; poll/callback under the SAME operation key
Unknown   -> NeedsReconciliation; query provider state before ANY further action
             never re-execute as a new economic or document action
```

---

## 6. Evidence gaps

Recorded honestly, per the P3-A brief.

| Gap | Impact | Mitigation |
|---|---|---|
| **SITA Horizon** internal servicing workflows are not publicly documented at the level Amadeus and Sabre are. Direct fetch of the SITA brochure returned HTTP 403 this pass; SITA evidence rests on a reputable secondary survey (S11) plus the brochure title. | Medium. SITA is used for **module-boundary** evidence (Ticketing vs Airfare Price), which is corroborated independently by Amadeus and Sabre. | No AeroTech decision rests on SITA **alone**. Every SITA-supported decision is also supported by Amadeus or Sabre. |
| **Navitaire New Skies** servicing evidence is mostly carrier policy on the platform (Frontier, Sun Country, IndiGo) rather than vendor documentation, and the Frontier PDF was not machine-readable this pass. | Low. Navitaire is used only to validate the *ticketless / no accountable document* and *credit-shell disposition* cases. | Treated as a **boundary check**, not a source of workflow. |
| Exact **void windows**, **waiver authority rules**, **EMD provider capabilities** and **refund destinations** are carrier-, market- and issuer-specific everywhere. | None — this is the expected industry state. | All are provider/configuration contracts, never hard-coded. |
| Sabre's cancel-refund detail (S9) comes from a secondary host of a Sabre document. | Low. | Used only to establish that corrective reversal is *windowed and provider-gated*, which Amadeus corroborates. |
| No Tier-1 evidence was sought for **DCS / disruption recovery** execution. | None for P3. | Explicitly out of scope; the no-show boundary is defined negatively (nothing is inferred). |
