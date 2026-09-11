# 05 - Scenario Validation Catalogue

## Purpose

This is a design-review and executable acceptance-test specification, NOT evidence that the AeroTech .NET service has passed these scenarios. Original scenario IDs S-001 through S-085 are preserved. Their former PASS labels are replaced to distinguish representable/reviewed design from actual runtime verification.

**DESIGN-REVIEWED** means a concrete representation and rule are specified. **DESIGN-REVIEWED / CONFIGURED POLICY** means the airline or certified provider supplies the business parameter/quote, with missing mandatory evidence blocking the operation. **DEFERRED ADAPTER** means the core identity/data can represent the case, but its specialized cross-carrier workflow is not claimed implemented.

All example prices, taxes and fees are synthetic test data. They are not real tariff, tax or refund advice. Current state, confirmed external facts, financial conservation, identity and required refusal behavior must be asserted in production tests, not just a successful HTTP response.

Reference-specification arithmetic and small deterministic rules were executed separately in this document revision; see `09-VALIDATION-REPORT.md`. They do not exercise EF, SQL, RabbitMQ, AeroTech production code or actual providers.

---

# A. Core sale and reservation

## S-001 - One passenger, direct one-way, card payment, ticket issuance

Accept trusted quote -> create current Order/item/service and optional Order-level bound fare context -> commit price lines once. Persist reservation/payment/issue intents before dispatch. Confirm capacity and money/guarantee evidence, reserve stock where local, then record confirmed document/coupon.

Assert Active commercial service, separate reservation/payment/document facets, stable ServiceId, one original price and one issued document. A dispatched HTTP request is not evidence of capture/issuance. Read-side completion is committed locally with the write and outbox.

**Assessment: DESIGN-REVIEWED**

---

## S-002 - Two passengers, direct one-way, one priced OfferItem

One OrderItem can contain:

```text
S1 P1/SEG1
S2 P2/SEG1
```

Pricing may contain one combined Item amount with source allocations per passenger, or separate line breakdown.

Do not force two OrderItems solely because there are two passengers.

**Assessment: DESIGN-REVIEWED**

---

## S-003 - Partial reservation success

P1 reservation confirmed; P2 provider outcome rejected/unknown.

Expected:

- commercial Services remain individually identifiable;
- FulfillmentReservation/links show mixed result;
- read model ReservationSummary = Partial/Unknown;
- Order does not transition globally to ReserveFailed.

Follow-up policy decides rollback vs retain partial hold.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-004 - Inventory timeout after provider may have committed

Expected:

```text
FulfillmentReservation = Unknown
ProviderInteraction = indeterminate
reconciliation required
```

Unsafe duplicate booking is blocked until idempotent provider semantics or reconciliation resolves the outcome.

**Assessment: DESIGN-REVIEWED**

---

# B. Fare construction

## S-005 - True round-trip fare

Route:

```text
IKA-IST / IST-IKA
```

Structure:

```text
PU1 RoundTrip
  FC1 outbound
  FC2 inbound
```

If inbound changes, Pricing may require repricing PU1 including the relationship to flown/unchanged outbound.

Ordering supplies stored construction but does not calculate the new fare.

**Assessment: DESIGN-REVIEWED**

---

## S-006 - Round trip built from two independent one-ways

Structure:

```text
PU1 OneWay -> outbound
PU2 OneWay -> inbound
```

Inbound change can be priced independently if Pricing rules allow.

The itinerary looks identical to S-005, but the stored pricing construction differs.

**Assessment: DESIGN-REVIEWED**

---

## S-007 - Source-defined combination of one-way fares

Given one source pricing result containing two OW fare components, preserve the EXACT returned PU grouping and combination metadata. Do not force `PU.Type=RoundTrip` merely because the itinerary returns to origin or components are labeled OW. A source-defined combined PU and two independent PUs are distinct valid inputs.

Assert normalized fare-context structure and cross-item bindings; automated servicing passes the whole relevant context to Pricing.

**Assessment: DESIGN-REVIEWED**

---

## S-008 - Connecting itinerary with one through fare

```text
IKA-DOH-BKK
```

Two AirServices, one FareComponent covering SEG1+SEG2.

No 1:1 assumption between Service and FareComponent.

**Assessment: DESIGN-REVIEWED**

---

## S-009 - Same connecting itinerary with fare break

Two FareComponents and possibly one or two PricingUnits according to source.

**Assessment: DESIGN-REVIEWED**

---

## S-010 - Open jaw

```text
IKA-FRA
MUC-IKA
```

Journey supports separate geographic legs; FareConstruction can use `OpenJaw` PU. Surface sector need not be a fulfilable air Service.

**Assessment: DESIGN-REVIEWED**

---

## S-011 - Circle trip / multi-city

Multiple journeys/segments, multiple fare components/PUs.

OrderItem boundary remains accepted commercial boundary, not itinerary topology.

**Assessment: DESIGN-REVIEWED**

---

## S-012 - Mixed ADT / CHD / INF pricing

Separate PricingGroups reference traveler sets and PTCs.

Service granularity remains traveler/segment.

**Assessment: DESIGN-REVIEWED**

---

## S-013 - Dynamic air price with no legacy fare construction

Pricing returns accepted product + price + terms, but no fare basis/PU/FC.

Expected:

```text
AirFareConstruction = null
PricingLines complete
```

No data is fabricated.

**Assessment: DESIGN-REVIEWED**

---

## S-014 - Charter air price

Air Services are created from group/charter context. Pricing line basis can be OrderItem/contract quote without FareConstruction.

**Assessment: DESIGN-REVIEWED**

---

# C. Taxes, fees, commission and currency

## S-015 - Segment tax

Tax line basis Segment with traveler/service allocation.

**Assessment: DESIGN-REVIEWED**

---

## S-016 - Journey/pricing-unit tax without segment split

Preserve source basis. Do not fabricate per-segment tax values.

**Assessment: DESIGN-REVIEWED**

---

## S-017 - YQ/YR carrier surcharge

Separate CarrierSurcharge line, independent refundability and basis.

**Assessment: DESIGN-REVIEWED**

---

## S-018 - Order-level service/booking fee

Fee basis Order, application PerOrder.

**Assessment: DESIGN-REVIEWED**

---

## S-019 - Credit-card fee

Fee belongs to commercial pricing even if triggered by selected tender. PaymentId is not pricing basis; payment linkage is separate.

**Assessment: DESIGN-REVIEWED**

---

## S-020 - Promotion/discount across fare and baggage

One Discount credit line may have source allocation across air/baggage. If no split exists, allocation can remain unavailable.

Partial refund later must call Pricing/policy rather than using an invented split.

**Assessment: DESIGN-REVIEWED**

---

## S-021 - Agency commission

Commission stored as SettlementOnly unless contract explicitly affects customer price.

Customer total and settlement amount remain distinguishable.

**Assessment: DESIGN-REVIEWED**

---

## S-022 - Markup/net fare model

Fare/product charge represents commercial customer value; markup and settlement-only lines represent retailer economics as provided by Pricing/settlement configuration.

No assumption that customer price equals supplier settlement value.

**Assessment: DESIGN-REVIEWED**

---

## S-023 - Accepted transaction contains more than one currency representation

Given OriginalValue USD100 and SaleValue EUR90 at the accepted historical rate, a full reversal records opposing USD100/EUR90 even when a later market rate is0.95. A permitted FX adjustment is separately identified and authorized. Never silently turn that reversal into EUR95. Partial reversals preserve both outstanding-currency caps with explicit source attribution.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-024 - Source/platform rounding and allocation residual

Given an accepted parent amount cannot be divided exactly across stable targets under the authoritative platform/source monetary policy, apply only the allocation/rounding treatment already established by that owner and prove the children conserve the accepted parent value. Do not introduce a new local rounding algorithm or balancing charge. If no authoritative treatment exists, block automated allocation and raise a decision.

**Assessment: DESIGN-REVIEWED**

---

# D. Ancillaries and non-air products

## S-025 - Baggage per piece on one segment

One BaggageService, one ProductCharge, optional tax.

**Assessment: DESIGN-REVIEWED**

---

## S-026 - Excess baggage per kilogram

PricingLine supports quantity/unit/application-level metadata.

**Assessment: DESIGN-REVIEWED**

---

## S-027 - Baggage covering a connecting direction

ServiceCoverage may reference multiple connected segments if that is the sold/delivered product scope.

**Assessment: DESIGN-REVIEWED**

---

## S-028 - One round-trip baggage price covering outbound and inbound services

One OrderItem can contain two BaggageServices. One Item-level price is valid.

If source has no service split, no exact allocation is invented.

**Assessment: DESIGN-REVIEWED**

---

## S-029 - Seat paid separately per segment

Each independently priced seat may be its own OrderItem/SeatService.

**Assessment: DESIGN-REVIEWED**

---

## S-030 - Round-trip seat bundle

One OrderItem contains multiple SeatServices with one bundle price.

**Assessment: DESIGN-REVIEWED**

---

## S-031 - Paid meal

MealService with charge/tax lines.

**Assessment: DESIGN-REVIEWED**

---

## S-032 - Meal included in fare

MealService may be included in an air/bundle OrderItem without a separate zero-value pricing line.

**Assessment: DESIGN-REVIEWED**

---

## S-033 - Lounge tied to departure flight

LoungeService uses traveler + airport/time + optional segment coverage.

**Assessment: DESIGN-REVIEWED**

---

## S-034 - Standalone lounge pass

No Segment required; product remains valid in Order.

**Assessment: DESIGN-REVIEWED**

---

## S-035 - Hotel, three nights, room + VAT + city tax + markup

One HotelService with typed stay details. Pricing lines can use PerNight/PerRoom/Tax/Markup semantics.

**Assessment: DESIGN-REVIEWED**

---

## S-036 - Ground transfer per vehicle

GroundTransportService plus PerService/ProviderDefined price.

**Assessment: DESIGN-REVIEWED**

---

## S-037 - Insurance covering full trip

InsuranceService coverage can be trip/order scoped with third-party supplier reference.

**Assessment: DESIGN-REVIEWED**

---

## S-038 - Mixed bundle: air + bag + seat + priority

One priced OrderItem can contain heterogeneous Services.

Bundle value allocation is only stored if source/valuation policy provides it.

**Assessment: DESIGN-REVIEWED**

---

## S-039 - Notification, penalty or manual adjustment appears in current service enum

Expected final model:

- Notification is not OrderService;
- Penalty is PricingLine;
- ManualAdjustment is PricingLine;
- TaxAdjustment is PricingLine;
- Credit is pricing/payment/stored-value fact according to semantics.

**Assessment: DESIGN-REVIEWED; implementation cleanup required**

---

# E. Payment

## S-040 - One Order, two payments: wallet + card

Payment Service owns both payment legs. Ordering projection shows aggregate applications and coverage.

**Assessment: DESIGN-REVIEWED**

---

## S-041 - Partial payment

Payment summary = PartiallyCovered; commercial Order remains Active if policy allows hold-before-full-payment.

Issuance policy blocks/permits according to required guarantee, not global Order status.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-042 - One payment applied across multiple Orders

Payment Service owns cross-Order allocation; each Ordering instance receives its own application fact.

No Payment aggregate duplication inside each Order.

**Assessment: DESIGN-REVIEWED**

---

## S-043 - Payment provider unknown

Payment projection exposes Pending/Unknown coverage. Order does not become PaymentFailed.

**Assessment: DESIGN-REVIEWED**

---

# F. Ticket / EMD

## S-044 - Partial ticket issuance

P1 ticket issued, P2 issuance unknown/failed.

Documents represent actual outcome; read model shows Partial. No global `Ticketed` state required.

**Assessment: DESIGN-REVIEWED**

---

## S-045 - Ticket issuance timeout after provider committed

ProviderInteraction unknown, reconcile document number before reissue.

**Assessment: DESIGN-REVIEWED**

---

## S-046 - Seat/baggage ancillary requires EMD

EMD coupon links to the corresponding OrderService. Commercial Service remains independent of EMD identity.

**Assessment: DESIGN-REVIEWED**

---

## S-047 - Void inside allowed window

Document policy voids Ticket/EMD/coupon. Related commercial cancellation/price reversal is an explicit OrderChange/PriceChangeSet.

**Assessment: DESIGN-REVIEWED**

---

## S-048 - Attempt void after flown coupon

Document invariant rejects void. Commercial refund/exceptions require separate policy.

**Assessment: DESIGN-REVIEWED**

---

# G. Voluntary servicing

## S-049 - Cancel only one passenger on outbound

Target one AirService. Pricing determines monetary consequence. Other passenger Services remain active.

OrderItem may become PartiallyChanged or be replaced/partitioned if pricing boundary requires.

**Assessment: DESIGN-REVIEWED**

---

## S-050 - Change return flight for one passenger on true RT

Stored FareConstruction identifies the affected RoundTrip PU. Pricing may reprice whole PU. Create successor Segment/Service and PriceChangeSet.

**Assessment: DESIGN-REVIEWED**

---

## S-051 - Same change on OW+OW

Stored construction identifies independent inbound PU. Pricing can reprice that PU only.

**Assessment: DESIGN-REVIEWED**

---

## S-052 - Change after outbound flown

Delivery observation preserves outbound consumption. Pricing evaluates used portion and change rule; new return Service replaces old return Service.

**Assessment: DESIGN-REVIEWED**

---

## S-053 - Add baggage after ticketing

New ancillary Offer accepted -> new OrderItem/BaggageService -> new PriceChangeSet -> payment -> EMD/fulfilment if required.

Existing air Item/ticket need not be rewritten.

**Assessment: DESIGN-REVIEWED**

---

## S-054 - Partial refund of bundle

Cancellation of one component does not automatically refund its internal allocation. Bundle/refund policy is priced explicitly.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-055 - Exchange/reissue

Old Service/Item remains historical and is replaced by successor. Price lines append differential/reversal/penalty. Ticket document lineage is separate but linked by ChangeId.

**Assessment: DESIGN-REVIEWED**

---

# H. Split

## S-056 - Split two passengers before ticketing with exact allocations

Move stable Services for P1 to child Order; partition any straddling Item; append SplitTransfer value lines to source/child; provider reservation split/rebook as needed.

**Assessment: DESIGN-REVIEWED**

---

## S-057 - Split with one bundle price and no allocation

Ordering does not guess the monetary partition. Require Pricing split valuation or explicit configured allocation method marked Derived.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-058 - Split after ticketing

Stable Service moves; ticket/coupon is reassigned or document workflow splits/reissues according to provider/document semantics; payment application is reallocated by Payment, not copied.

**Assessment: DESIGN-REVIEWED**

---

## S-059 - Split after partial travel

Given P1/P2 on a round trip and P1 outbound already used, an approved split moves P1 current membership and their exclusively associated services, including used historical travel, to the child while keeping stable service identity. Old source pricing, issue-time document data and observation scope remain immutable. Shared order-local journey snapshots are cloned/remapped where necessary; historical references remain resolvable.

Validate issuer/provider divide capability and infant/shared-beneficiary constraints. Do not leave an orphaned service without its current traveler or rewrite historical consumption. Monetary and payment transfers are explicit balanced pairs. Unknown provider divide retains the same operation and child identity.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

# I. DCS / delivery

## S-060 - Check-in

DCS event -> append observation -> DeliveryState InProgress -> optional coupon state CheckedIn. Commercial Service remains Active.

**Assessment: DESIGN-REVIEWED**

---

## S-061 - Boarded then flown

Two milestones retained; current DeliveryState eventually Delivered. Late CheckedIn event cannot regress state.

**Assessment: DESIGN-REVIEWED**

---

## S-062 - Boarded then offloaded

Offloaded observation is valid after boarding. Delivery transition policy sets appropriate current state without deleting Boarded history.

**Assessment: DESIGN-REVIEWED**

---

## S-063 - No-show on outbound, return still booked

DCS NoShow -> Service delivery NotClaimed.

Return Service remains commercially Active until NoShow policy creates explicit change.

This prevents accidental automatic cancellation when fare/carrier policy differs.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-064 - Ancillary consumed

Lounge/meal/WiFi/other Service can receive `AncillaryConsumed`, independent of ticket coupon lifecycle.

**Assessment: DESIGN-REVIEWED**

---

## S-065 - Duplicate and batched DCS event

Given one source message with observations for P1 and P2, process both using distinct ObservationKey values. Repeating the whole message or one observation produces no duplicate effect. Message Inbox identity and observation identity are distinct. Completion of only P1 cannot mark the whole input batch finished.

**Assessment: DESIGN-REVIEWED**

---

## S-066 - Out-of-order and corrected DCS evidence

Given source sequences2 then1 in the SAME aspect/epoch, preserve evidence without regressing current state. Seat and boarding sequences do not overwrite each other. Later authoritative corrections have explicit supersession and authority, not merely a larger application receive timestamp. Ambiguous terminal contradiction goes to reconciliation. A missing global-flight event does not prevent accepting a valid passenger-level fact.

**Assessment: DESIGN-REVIEWED**

---

## S-067 - Legacy DCS sends PNR/ticket rather than OrderServiceId

Correlation indexes resolve:

```text
PNR/ticket/coupon + passenger + flight/date
-> OrderServiceId
```

**Assessment: DESIGN-REVIEWED**

---

# J. Flight operations and disruption

## S-068 - Flight time changes by five minutes

Update SegmentOperationalState. Sold schedule snapshot remains unchanged. No commercial change required.

**Assessment: DESIGN-REVIEWED**

---

## S-069 - Flight cancelled

Operational state = Cancelled; affected active Services identified; DisruptionImpact recorded/consumed; recovery workflow starts.

No direct destructive rewrite of Order.

**Assessment: DESIGN-REVIEWED**

---

## S-070 - Aircraft change invalidates paid seat

FlightOps/DCS identifies seat impact. Disruption/recovery or seat policy creates replacement/refund/reassignment action for SeatService.

Air fare Item need not be rewritten unless commercial treatment demands it.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-071 - Airport change

Operational segment shows current airport. If commercially material, DisruptionImpact triggers involuntary servicing.

**Assessment: DESIGN-REVIEWED**

---

## S-072 - Broken connection / misconnection

First segment flown; downstream Service becomes impacted. Recovery creates successor downstream segment/service, preserves consumed history, reprices/revalidates according to involuntary policy.

**Assessment: DESIGN-REVIEWED**

---

## S-073 - Reaccommodation to partner carrier

New successor AirService stores partner supplier/operating carrier metadata. Interline settlement workflow may be deferred, but commercial/domain model does not require schema redesign.

**Assessment: DEFERRED ADAPTER**

---

# K. Group / charter

## S-074 - Group seat block with unnamed passengers

Given confirmed50 outbound seats and40 inbound seats, reject materializing50 round-trip passengers even though the sum of capacity is90. Check every referenced block separately. No fictitious Traveler record is created for unnamed capacity.

**Assessment: DESIGN-REVIEWED**

---

## S-075 - Allocate 50 charter names and create Orders

Given BatchId B1 and50 stable ClientRowIds, materialize according to explicit passenger/order grouping. Suppose49 rows succeed and one fails validation. A retry returns the49 existing results and retries only the corrected/new-intent failed row as allowed by its receipt rules. Never recapture group deposit, reserve capacity or issue another document for completed rows.

AirServices and accepted prices reference the charter contract quote without fabricated ATPCO PUs. Transfer/reserve deposit coverage once to each materialized scope; do not copy the entire deposit to all orders.

**Assessment: DESIGN-REVIEWED**

---

## S-076 - Group deposit

Payment Service owns money; GroupBooking stores deposit/payment application reference used by eligibility policy.

**Assessment: DESIGN-REVIEWED**

---

## S-077 - Name deadline expires

Group time-limit policy releases unallocated slots/capacity according to group rules. Spawned Orders remain independent commercial records.

**Assessment: DESIGN-REVIEWED**

---

# L. Read/query practicality

## S-078 - Get Order by ID

One local read-model operation returns commercial, reservation, payment, document, delivery and disruption facets. Protected traveler payload expansion remains local and authorized. There is no required fan-out to external Payment, Inventory, DCS or Disruption. Stored derived status and totals can be indexed; ordinary retrieval never replays all financial or delivery history.

**Assessment: DESIGN-REVIEWED**

---

## S-079 - Search by PNR

ExternalReferenceIndex -> Order.

**Assessment: DESIGN-REVIEWED**

---

## S-080 - Search by ticket number

DocumentOrderIndex -> Order/Traveler/Service.

**Assessment: DESIGN-REVIEWED**

---

## S-081 - Find all Orders affected by a flight

FlightOrderIndex/ServiceCoverage index returns affected OrderServices without loading every Order aggregate.

**Assessment: DESIGN-REVIEWED**

---

## S-082 - Find one passenger's outstanding services

Read model indexes traveler/service state and delivery summary.

**Assessment: DESIGN-REVIEWED**

---

# M. Reliability

## S-083 - Order DB commit succeeds, RabbitMQ unavailable

Outbox retains event and publishes later.

**Assessment: DESIGN-REVIEWED**

---

## S-084 - Same external integration event delivered repeatedly

Inbox/source event idempotency prevents duplicate domain side effects.

**Assessment: DESIGN-REVIEWED**

---

## S-085 - Ledger temporarily rejects event

Commercial Order remains unchanged. Accounting reconciliation handles rejection; immutable pricing event can be replayed.

**Assessment: DESIGN-REVIEWED**

---


# N. Review-driven acceptance cases

Every case below is **DESIGN-REVIEWED**, not an executed production test. The expected refusal/reconciliation is part of the acceptance outcome.

## S-086 - Monetary-value validation

**Given/When:** an operation attempts to combine or interpret monetary values without the authoritative currency/representation required by the current platform contract.

**Then:** reject or block the operation rather than assuming compatibility/conversion. Use the existing AeroTech monetary representation discovered in P0; this scenario does not prescribe a new `Money` type, numeric scale or rounding implementation.

**Specification:** 01 section19; 02 section3; 13.

---

## S-087 - Single sign for discount and its reversal

**Given/When:** Fare400 + bag50 - discount45; reverse discount45.

**Then:** CustomerTotal405 then450; commission20 SettlementOnly changes neither customer result.

**Specification:** 02 section3.4.

---

## S-088 - Alternative allocation purposes

**Given/When:** CommercialValue set100 and Reporting set100 for same line.

**Then:** Customer charge stays100, not200. Select one current version per line/purpose.

**Specification:** 02 section4.

---

## S-089 - Partial reversal cap

**Given/When:** Original100; prior reversal30; requested additional80.

**Then:** Reject over-reversal; additional70 is the maximum absent a separately authorized adjustment.

**Specification:** 02 section3.4.

---

## S-090 - Cancellation penalty is not reversal

**Given/When:** Cancel original fare100; add penalty10 with Cancellation reason.

**Then:** Residual customer charge10. The new penalty is not skipped by a reason-based IsReversal test.

**Specification:** 02 sections3.4/12.

---

## S-091 - Inclusive tax does not double count

**Given/When:** Gross110 contains tax10.

**Then:** Record net100 plus tax10, not gross110 plus tax10. Unknown required breakdown blocks affected issue/accounting.

**Specification:** 02 section3.5.2.

---

## S-092 - Tax incidence and exemption

**Given/When:** Same tax code at two airports; one passenger exemption.

**Then:** Keep distinct occurrence/scope/rule references and explicit exemption; do not merge by tax code alone.

**Specification:** 02 section3.5.2.

---

## S-093 - Payment allocation need is scoped

**Given/When:** Fully paid Order funded by wallet/card; one service refunded.

**Then:** Order-level applications can suffice with external tender routing; restricted partial issue requires reserved scope coverage.

**Specification:** 03 section3.

---

## S-094 - No fabricated confirmed funding

**Given/When:** Requested880 but confirmed500 or no amount.

**Then:** Show partial500 or pending evidence; never substitute880. Approved credit guarantee is separately evaluated.

**Specification:** 03 section3; 07.

---

## S-095 - Create idempotency before OrderId

**Given/When:** Same caller/key/body submitted twice with no OrderId.

**Then:** One persisted receipt and one allocated OrderId; return/resume original result.

**Specification:** 04 section10; 08 section2.

---

## S-096 - Idempotency payload conflict

**Given/When:** Reuse completed key with a different amount or quote.

**Then:** 409 conflict; authenticated identical replay returns original result before comparing current business version.

**Specification:** 08 section2.

---

## S-097 - Crash before external dispatch

**Given/When:** Durable step committed; process stops before provider call.

**Then:** Resume same StepId/key; no new economic operation is invented.

**Specification:** 07 section5.

---

## S-098 - Provider success then local crash

**Given/When:** Capture succeeds; local outcome persistence fails.

**Then:** Reconcile same persisted intent/key. AttemptCount can change but provider economic key cannot.

**Specification:** 03 section3; 04 section12.

---

## S-099 - Late confirmed payment after expiry

**Given/When:** Order locally expired; authorized owner reports real captured money.

**Then:** Record actual payment evidence and reconciliation/disposition; do not discard cash or silently reactivate travel.

**Specification:** 03 section3; 07.

---

## S-100 - Stale quote across every operation family

**Given/When:** Quote was valid at preview but Order changed before commit.

**Then:** Reject/requote with StaleCommercialVersion or QuoteScopeMismatch; UI preview is not execution authority.

**Specification:** 07 sections1/2.

---

## S-101 - Issue and cancel compete

**Given/When:** Two different operation names target the same Order.

**Then:** One durable blocking Order claim; different Redis prefixes cannot permit both to finalize.

**Specification:** 07 section5.

---

## S-102 - Lease expires with unknown issue

**Given/When:** Redis lease expires while issuer outcome unknown.

**Then:** Claim stays blocking; recovery resumes/reconciles same operation. Lease expiry is not permission to issue again.

**Specification:** 07 section5.

---

## S-103 - Failure saving local read model

**Given/When:** Write/outbox save succeeds inside transaction; projector save fails.

**Then:** Rollback entire shared local transaction. Discard failed context; no user-visible completed command.

**Specification:** 04 sections5/12.

---

## S-104 - Concurrent DCS and commercial projector

**Given/When:** DCS update races a commercial cancellation view rebuild.

**Then:** Same per-order projector fence and current canonical read prevent lost facets; ProjectionRevision is separate.

**Specification:** 04 section5.

---

## S-105 - Projection rebuild is operational

**Given/When:** Corrupt/missing view; authorized rebuild requested.

**Then:** Rebuild from current command/evidence state with same projector; no capture, pricing, issue or notification side effects.

**Specification:** 04 section5; 08 section12.

---

## S-106 - Financial outbox reversal arrives first

**Given/When:** Sequence2 arrives before1; then duplicate2.

**Then:** Stage2 until1 applies, then apply once. Do not post reversal against missing original.

**Specification:** 03 section11.

---

## S-107 - Filtered financial stream sequences

**Given/When:** Commercial events occur between financial changes.

**Then:** FinancialSequence stays consecutive for PriceChangeSets; consumer does not wait for unrelated DCS events.

**Specification:** 08 section9.

---

## S-108 - Batched source message identity

**Given/When:** One envelope carries multiple passengers and aspects.

**Then:** Dedup observation by source/event/ObservationKey; retain all distinct rows.

**Specification:** 03 section5; 08 section10.

---

## S-109 - Ambiguous legacy DCS identity

**Given/When:** Same PNR/name candidate matches two services.

**Then:** Quarantine unresolved correlation; never choose first match or alter a random passenger.

**Specification:** 03 section5.

---

## S-110 - Late DCS event after split

**Given/When:** Source event contains old OrderId but valid stable coupon/service.

**Then:** Resolve current service ownership through canonical alias; preserve old source scope as evidence.

**Specification:** 08 sections7/10.

---

## S-111 - Authoritative no-show correction

**Given/When:** NoShow followed by certified correction with supersession.

**Then:** Update travel aspect through correction policy; reconcile any commercial action already taken, not erase it.

**Specification:** 03 section5.

---

## S-112 - EMD-S for cancellation fee

**Given/When:** Issuer supports monetary fee EMD without deliverable service.

**Then:** CouponPurpose Fee links PricingLine/Change; no fake AirService or Entitlement.

**Specification:** 01 section13; 08 section8.

---

## S-113 - Document number on retry

**Given/When:** Persisted operation has Reserved stock number; issue response lost.

**Then:** Reuse/reconcile that number/step, never allocate a second number for the same issue.

**Specification:** 01 section14.

---

## S-114 - Overlapping stock ranges

**Given/When:** Two concurrent registrations request intersecting issuer ranges.

**Then:** Serialized interval overlap validation rejects one; unique first-number index alone is insufficient.

**Specification:** 08 section8.

---

## S-115 - Per-block group capacity

**Given/When:** OUT50/IN40, request50 return travelers.

**Then:** Reject inbound shortage despite total90; per-block invariant remains true.

**Specification:** 08 section11.

---

## S-116 - Bulk charter row retry

**Given/When:** 49 successful named rows; one rejected; repeat batch.

**Then:** Completed rows retain Order/document/payment IDs; only genuinely unresolved rows proceed.

**Specification:** 08 section11.

---

## S-117 - Shared hotel/vehicle during split

**Given/When:** Two guests share one room; only one traveler moves.

**Then:** Reject unsupported partition or obtain supplier/pricing partition; never duplicate room or charge.

**Specification:** 01 section6.5.

---

## S-118 - Pricing unit spans two items

**Given/When:** Source has one coupled PU across two accepted item IDs.

**Then:** One immutable Order-owned construction with both bindings; not two independently repriced PUs.

**Specification:** 01 section7.

---

## S-119 - Price-only change retains used service

**Given/When:** Return change reprices RT including flown outbound.

**Then:** Outbound service remains same consumed identity; new price context links it without a second delivered obligation.

**Specification:** 08 section4.

---

## S-120 - One service replaced by two connections

**Given/When:** Cancelled direct flight replaced by two connecting flights.

**Then:** New service IDs and one-to-many lineage; old service remains historical.

**Specification:** 01 section8; 08 section7.

---

## S-121 - Privacy erasure and replay

**Given/When:** Approved payload deletion followed by projection rebuild or late event.

**Then:** No resurrection of erased name/passport from immutable history; amounts and structural IDs remain.

**Specification:** 01 section22.

---

## S-122 - Owner boundary

**Given/When:** Client supplies a different OwnerAirlineId/TenantId.

**Then:** Ignore untrusted owner and deny unauthorized scope; partner operating carrier does not become owning tenant.

**Specification:** 01 section22.

---

## S-123 - Derived state remains queryable

**Given/When:** Search active Orders without loading history.

**Then:** Use stored deterministic commercial summary/index; still apply operation eligibility separately.

**Specification:** 01 section17; 04 section5.

---

## S-124 - DCS control release is acknowledged

**Given/When:** Refund requested for coupon under external airport control.

**Then:** Pending until explicit release/control evidence; delivery event receipt alone is not release acknowledgment.

**Specification:** 03 section6; 07 section4.

---

## S-125 - Grouped price amount multiplication

**Given/When:** Two adults source total400 with Quantity2.

**Then:** Persist extended400 once, not800; per-traveler data explicitly identifies its amount basis.

**Specification:** 08 section4.

---

## S-126 - Independent DCS aspect versions

**Given/When:** Seat version20 and boarding version3 arrive.

**Then:** Both aspects apply independently; one global last-sequence value cannot drop boarding.

**Specification:** 08 section10.

---

## S-127 - Paid ancillary after aircraft change

**Given/When:** Paid seat cannot be honored; air travel remains valid.

**Then:** Explicit seat replacement/refund treatment; no hidden cancellation of air or assumption fee equals refund.

**Specification:** 03 section8; 02 section9.

---

## S-128 - Protected expiry with payment in flight

**Given/When:** Hold due-time passes while coverage/issuance is unresolved.

**Then:** Keep actual expiry fact, block unsafe finalization and reconcile; do not guess external hold extension or successful issue.

**Specification:** 07 section2.

---

## S-129 - Balance transfer is not new sale

**Given/When:** Split Fare200+Tax40-Discount20 equally.

**Then:** Source110 + child110 =220; funding sums220; Ledger treats paired transfer as reclassification, not new tax/revenue.

**Specification:** 02 section12 F-P07.

---

## S-130 - Real adapter release gate

**Given/When:** Mocks pass but payment/DCS/issuer contract is uncertified.

**Then:** Capability is not production-ready; fail safe rather than invent supported outcome.

**Specification:** 04 sections14/15.

---

# O. Advanced flight topology and capacity

The cases below close the remaining flight-topology/inventory gaps. They are design-reviewed acceptance specifications; provider rules remain authoritative where noted.

## S-131 - Married segments confirmed as one usable capacity set

**Given/When:** P1 has A-B-C sold as two passenger segments and Inventory marks both Services in one `ReservationCouplingGroup(Kind=MarriedSegments)`, both Confirmed.

**Then:** Both Services can become reservation-ready together. The group reference is retained for later change/cancel; the Order does not invent an independent-cancel promise for either member.

**Specification:** 01 section11; 08 section7.

**Assessment: DESIGN-REVIEWED**

---

## S-132 - Married segments with one Waitlisted/Unknown member

**Given/When:** A-B Confirmed, B-C Waitlisted or Unknown inside the same married/atomic group.

**Then:** The set is not issue-ready as if A-B were independently usable. Preserve each member status; reconcile or obtain a provider-approved replan. Do not globally mark the Order failed.

**Specification:** 01 section11; 07 issuance eligibility.

**Assessment: DESIGN-REVIEWED**

---

## S-133 - Connection and stopover are explicit, not duration guesses

**Given/When:** Two adjacent passenger segments have the same elapsed gap in two sales, but one source calls it a protected Connection and the other an agreed Stopover.

**Then:** Persist `JourneyConnection.ConnectionKind/ProtectionType` from trusted source semantics. Do not infer fare break, protection or reaccommodation duty solely from elapsed hours.

**Specification:** 01 section4.

**Assessment: DESIGN-REVIEWED**

---

## S-134 - One passenger segment contains multiple operational legs

**Given/When:** Passenger buys A-C on flight XY100 and the operating flight has physical legs A-B and B-C without passenger deplaning/re-shopping.

**Then:** Keep one `JourneySegment` and one P1 AirTransportService; bind multiple sold/current operational leg refs. Do not manufacture a second commercial Service/coupon merely because FlightOps has two legs.

**Specification:** 01 section4; 03 section7.

**Assessment: DESIGN-REVIEWED**

---

## S-135 - Same through flight but separately sold passenger segments

**Given/When:** P1 explicitly holds A-B and B-C as separately sold passenger segments, even though both carry the same flight number/through-flight reference.

**Then:** Keep two JourneySegments and two AirTransportServices, linked by `ThroughFlightGroupRef` when supplied. Commercial identity follows sold passenger segments, not flight-number equality.

**Specification:** 01 section4.

**Assessment: DESIGN-REVIEWED**

---

## S-136 - Open air segment accepted before dated-flight assignment

**Given/When:** Legacy/open product is sold with origin/destination but no dated flight.

**Then:** `SegmentKind=OpenAir`; no fabricated flight/time. It is not ReadyForDelivery and cannot consume normal capacity until a concrete assignment/reservation is accepted. Open-coupon issuance is allowed only under an explicit issuer profile.

**Specification:** 01 sections4/6.4.1; 07.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-137 - Open segment later bound to a concrete flight

**Given/When:** The holder selects a dated flight for S-136.

**Then:** Commit an explicit commercial/fulfillment binding operation, preserve old open-product/document/pricing context, obtain required capacity, and publish delivery readiness only after eligibility passes. Do not rewrite historical sale as if it was always dated.

**Specification:** 01 sections4/8; 07.

**Assessment: DESIGN-REVIEWED**

---

## S-138 - Surface/ARNK between air segments

**Given/When:** A-B air, B-C surface/ARNK, C-D air.

**Then:** Surface remains itinerary context with `SegmentKind=Surface`; it consumes no air inventory and has no air coupon/OrderService unless an actual separately sold ground service exists. Fare construction may still reference the surface break.

**Specification:** 01 section4; 02 fare construction.

**Assessment: DESIGN-REVIEWED**

---

## S-139 - Reaccommodation from two connections to one direct flight

**Given/When:** Existing unconsumed Services A-B and B-C are replaced by direct A-C.

**Then:** Create one successor AirTransportService with many-to-one lineage from both replaced Services. Old identities remain historical; pricing/document exchange is explicit and not inferred from segment count.

**Specification:** 01 section8; 07 involuntary/voluntary change.

**Assessment: DESIGN-REVIEWED**

---

## S-140 - Flight diversion

**Given/When:** Sold A-B operates A-C due to diversion and passenger is physically delivered to C.

**Then:** FlightOps records actual/diverted operational evidence; sold origin/destination snapshot remains A-B. Delivery/disruption policy decides whether the air obligation is delivered, partially delivered or needs onward recovery. No automatic commercial rewrite to A-C.

**Specification:** 03 sections7/8.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-141 - Return to origin after departure

**Given/When:** A-B departs and returns to A.

**Then:** Do not infer `Flown/Delivered` merely because departure occurred. Store RTO operational outcome, preserve any DCS passenger milestones, open disruption/recovery impact, and keep original commercial Service until an explicit change resolves it.

**Specification:** 03 sections5/7/8.

**Assessment: DESIGN-REVIEWED**

---

## S-142 - Cancelled flight reinstated before commercial reaccommodation

**Given/When:** FlightCancelled is followed by authoritative FlightReinstated while no replacement Order change has committed.

**Then:** Update operational state; pending disruption impact can be resolved/re-evaluated. Existing Service identity remains. No duplicate service is created.

**Specification:** 03 sections7/8.

**Assessment: DESIGN-REVIEWED**

---

## S-143 - Flight reinstated after replacement already committed

**Given/When:** Original flight is reinstated after passenger has been commercially reaccommodated and old Service replaced.

**Then:** Reinstatement does not resurrect the replaced Service or cancel the successor. A new change would be required to move the customer back.

**Specification:** 03 section7.3; 01 lineage.

**Assessment: DESIGN-REVIEWED**

---

## S-144 - Waitlist created at reservation time

**Given/When:** Inventory returns Waitlisted for one Air Service.

**Then:** Commercial sale/service identity can exist, reservation observation is Waitlisted, issue eligibility follows configured product/issuer policy, and DCS Standby is not set.

**Specification:** 01 section11; 03 reservation integration.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-145 - Waitlist clears later

**Given/When:** Same reservation member changes from Waitlisted to Confirmed with authoritative provider version.

**Then:** Update reservation evidence idempotently; re-evaluate readiness and dependent married group. No new OrderService is created solely because capacity cleared.

**Specification:** 01 section11.

**Assessment: DESIGN-REVIEWED**

---

## S-146 - Airport standby is not inventory waitlist

**Given/When:** Confirmed booking reaches airport and DCS reports StandbyListed, later StandbyCleared.

**Then:** Change delivery/check-in aspect only. Reservation remains its authoritative status; no commercial cancel/refund follows without policy action.

**Specification:** 03 section5; 08 section10.

**Assessment: DESIGN-REVIEWED**

---

## S-147 - Protected connection broken by delay

**Given/When:** A-B delay makes protected B-C connection impossible.

**Then:** FlightOps updates both segment context and connection impact; Disruption may command recovery for downstream active Service. Consumed/operated A-B history remains; B-C replacement carries lineage and pricing treatment.

**Specification:** 01 JourneyConnection; 03 section8.

**Assessment: DESIGN-REVIEWED**

---

## S-148 - Unprotected/self-transfer connection breaks

**Given/When:** Same timings as S-147 but `ProtectionType=Unprotected`.

**Then:** Do not automatically create airline reaccommodation entitlement. Expose disruption context; any goodwill/new purchase follows explicit policy/offer.

**Specification:** 01 section4; 03 section8.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-149 - Time-zone/DST/date-boundary schedule correctness

**Given/When:** Segment crosses time zones/DST or international date boundary.

**Then:** Preserve local departure/arrival plus explicit time-zone IDs and unambiguous instants when scheduled. Duration/connection processing uses instants and trusted connection semantics, not naive local-date subtraction.

**Specification:** 01 section4.

**Assessment: DESIGN-REVIEWED**

---

## S-150 - Flight cancellation fan-out over 1,000 Orders

**Given/When:** One indexed `FlightCancelled` affects 1,000 Orders.

**Then:** Persist/ack source fact first, fan out asynchronously in resumable batches, one short fence/transaction per Order, no N-Order transaction and no provider I/O under the fence. Production-like P5 gate: complete local fan-out <=30s and each normal per-Order transaction <500ms with no lost racing DCS/servicing update.

**Specification:** 03 section7.2; 04 P5.

**Assessment: DESIGN-REVIEWED; RUNTIME LOAD GATE REQUIRED**

---

# P. Codeshare, interline and supplier boundaries

## S-151 - Codeshare marketing and operating carriers differ

**Given/When:** Sold flight is marketed by AA but operated by BB.

**Then:** Preserve both carrier identities in sold snapshot/service context plus Supplier/DeliveryProvider where relevant. Search/display can use marketing flight; operational correlation can use operating identity. Neither carrier field silently becomes Order owner.

**Specification:** 01 sections2/4/6.

**Assessment: DESIGN-REVIEWED**

---

## S-152 - Operating carrier changes after sale

**Given/When:** FlightOps changes operating carrier from BB to CC without changing sold marketing flight.

**Then:** Update operational projection and disruption impact if material; do not rewrite sold carrier snapshot. Delivery provider/supplier handoff is explicit when required.

**Specification:** 03 section7.

**Assessment: DESIGN-REVIEWED**

---

## S-153 - Retailer sells a partner-supplied air service

**Given/When:** AeroTech airline is retailer; partner airline is Supplier/DeliveryProvider.

**Then:** Order remains retailer's commercial master record; Service identifies supplier, partner references and external reservation/document ownership. Unsupported interline execution returns explicit capability unavailable rather than pretending local control.

**Specification:** 01 sections2/6; 03 owner matrix.

**Assessment: DESIGN-REVIEWED / DEFERRED ADAPTER**

---

## S-154 - Partner-supplied ancillary inside retailer Order

**Given/When:** Lounge/hotel/bag is sold by retailer but delivered by partner/third party.

**Then:** Commercial OrderItem/Service/pricing remains local; supplier booking and delivery facts remain external. Refund/replacement requires supplier/pricing evidence; local Order never fabricates consumption.

**Specification:** 01 section6; 03 owner matrix.

**Assessment: DESIGN-REVIEWED / DEFERRED ADAPTER**

---

## S-155 - Multiple external locators for one Order

**Given/When:** Retailer PNR plus partner carrier locator(s) and supplier booking reference exist.

**Then:** Store distinct typed/scoped `ExternalReference`s with owner/system; do not overwrite a single `PNR` field. Searches can resolve any indexed locator to canonical Order/Service.

**Specification:** 01 external references; 08 data dictionary.

**Assessment: DESIGN-REVIEWED**

---

## S-156 - Interline through check-in

**Given/When:** One DCS/partner message checks passenger through across retailer and supplier segments.

**Then:** Normalize member facts per Service/aspect; preserve each authority/version and partner correlation. Through check-in does not merge the Services or imply every downstream segment is flown.

**Specification:** 03 section5; 08 section10.

**Assessment: DESIGN-REVIEWED / DEFERRED ADAPTER**

---

## S-157 - Partner disruption responsibility differs from retailer

**Given/When:** Supplier-operated segment is cancelled and partner owns operational recovery while retailer owns customer Order.

**Then:** Keep disruption owner/responsibility reference distinct from Order owner. Ordering applies only accepted commercial recovery instruction/result; it does not run partner optimization internally.

**Specification:** 03 section8.

**Assessment: DESIGN-REVIEWED / DEFERRED ADAPTER**

---

## S-158 - Issuing/validating carrier differs from operating carrier

**Given/When:** Legacy ticket is issued/validated by carrier AA for segment operated by BB.

**Then:** Ticket issuer/stock namespace, marketing/operating carrier and service Supplier remain distinct fields. Document stock is allocated under issuer authority, never operating-carrier inference.

**Specification:** 01 document aggregates/stock.

**Assessment: DESIGN-REVIEWED**

---

## S-159 - Partner supplier outcome unknown

**Given/When:** partner reservation/ancillary request times out after possible success.

**Then:** Persist stable operation/provider key and Unknown evidence; reconcile/query same economic operation. Do not create duplicate partner booking or fallback supplier until prior outcome is safely resolved.

**Specification:** 03 failure matrix; 07 operations.

**Assessment: DESIGN-REVIEWED**

---

## S-160 - Partner settlement fact does not rewrite customer price

**Given/When:** Supplier settlement/internal value later changes or is disputed.

**Then:** Customer accepted PricingLines remain immutable. Settlement/accounting facts and partner payable are separate; no customer charge/refund occurs unless an explicit commercial servicing result says so.

**Specification:** 02 effect model; 03 Ledger/settlement boundary.

**Assessment: DESIGN-REVIEWED**

---

# Q. DCS and delivery edge cases

## S-161 - Denied boarding versus no-show

**Given/When:** Confirmed passenger presents but airline denies boarding.

**Then:** Record `DeniedBoarding`/provider non-delivery -> `FailedToDeliver`, not `NoShow/NotClaimed`. Compensation/recovery is explicit policy; downstream service cancellation is not automatic DCS mutation.

**Specification:** 03 section5.6.

**Assessment: DESIGN-REVIEWED**

---

## S-162 - Boarded, offloaded, then reboarded

**Given/When:** DCS reports Boarded v1, Offloaded v2, Boarded v3.

**Then:** Preserve all milestones; current boarding aspect follows authoritative version. Do not infer Flown until travel outcome evidence arrives.

**Specification:** 03 section5.5.

**Assessment: DESIGN-REVIEWED**

---

## S-163 - Authoritative correction to terminal Flown

**Given/When:** Flown was posted in error and certified DCS sends correction/supersession.

**Then:** Retain original evidence, apply only an authorized correction with source lineage/revision, update reducer, and open reconciliation for any commercial/accounting actions already caused by the wrong fact.

**Specification:** 03 sections5.5/5.6.

**Assessment: DESIGN-REVIEWED**

---

## S-164 - Through check-in across several segments

**Given/When:** one source envelope reports CheckedIn for three Services.

**Then:** Source envelope dedup happens once but creates three distinct ObservationKeys/aspect updates. A repeated envelope does not drop later members or duplicate earlier ones.

**Specification:** 03 section5.4.

**Assessment: DESIGN-REVIEWED**

---

## S-165 - First segment flown, downstream segment no-show

**Given/When:** P1 Flown on A-B and later NoShow on B-C.

**Then:** S1 remains Delivered; S2 becomes NotClaimed. Pricing/no-show policy evaluates the affected downstream/future scope without undoing flown history.

**Specification:** 03 section5.7; 02 servicing.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-166 - Ticket coupon and DCS outcome disagree

**Given/When:** local coupon says Open/CheckedIn while DCS authoritative travel outcome says Flown, or vice versa.

**Then:** Do not silently choose whichever row was written last. Apply certified synchronization rule; contradictory terminal evidence opens reconciliation and preserves both source references.

**Specification:** 03 sections5/6; 01 documents.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-167 - Seat changed after check-in

**Given/When:** passenger checked in with 12A; DCS changes assignment to 14C.

**Then:** Operational seat projection changes; sold seat product/paid characteristics remain historical. If new seat fails the paid product promise, dependent ancillary servicing/refund is explicit.

**Specification:** 01 SeatServiceDetails; 03 sections5/8.

**Assessment: DESIGN-REVIEWED**

---

## S-168 - Baggage accepted is not baggage delivered

**Given/When:** one paid bag is Accepted/Loaded but no delivery event yet.

**Then:** Preserve baggage handling milestones/quantity; do not mark entire baggage Service Delivered or assume refundability. Delivery completion follows certified obligation mapping.

**Specification:** 03 section5.6.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-169 - Baggage mishandled after passenger flew

**Given/When:** passenger Air Service is Flown, baggage is Mishandled/delayed.

**Then:** Air remains Delivered; baggage handling/service may be Failed/Incomplete according to provider profile. Recovery/claim lives in baggage/customer-care capability; Order retains purchase/delivery evidence.

**Specification:** 03 section5.6.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-170 - Paid ancillary not claimed

**Given/When:** lounge/meal/transfer expires unused and delivery provider reports NotClaimed.

**Then:** Service delivery state records NotClaimed; commercial refund is not inferred. Pricing/terms determine any credit.

**Specification:** 03 section5; 02 refund rule.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-171 - Partial ancillary quantity consumed

**Given/When:** Service sells two lounge guests/two baggage pieces and provider confirms one unit consumed.

**Then:** Track DeliveredQuantity/Unit/portion evidence without shrinking the originally sold Service or PricingLine. Partial refund/replacement is a new servicing price decision.

**Specification:** 01 section6.4.1; 03 section5.6.

**Assessment: DESIGN-REVIEWED**

---

## S-172 - Delivery provider rejects an otherwise active service

**Given/When:** supplier says ServiceFailed/UnableToDeliver while Order commercial Service is Active.

**Then:** Keep commercial truth and delivery failure separate; trigger explicit recovery/refund operation. Do not auto-cancel the OrderItem by projector side effect.

**Specification:** 03 sections5.6/8.

**Assessment: DESIGN-REVIEWED**

---

## S-173 - Document control held by DCS during servicing

**Given/When:** coupon is under external airport control and agent requests refund/exchange.

**Then:** Eligibility blocks unsafe document mutation, sends durable control-release request, waits for explicit acknowledgment, then resumes same operation. Notification publication is not release proof.

**Specification:** 03 section6; 07 document/control eligibility.

**Assessment: DESIGN-REVIEWED**

---

## S-174 - Flight departed does not prove passenger flew

**Given/When:** FlightOps says Departed/Arrived but DCS has no passenger travel outcome.

**Then:** SegmentOperationalState updates; passenger Service does not become Flown/Delivered solely from flight movement.

**Specification:** 03 sections5/7.

**Assessment: DESIGN-REVIEWED**

---

## S-175 - DCS batch for a full 180-passenger flight

**Given/When:** one/batched source feed contains check-in/boarding/travel observations for 180 passengers with duplicates and mixed aspect versions.

**Then:** Normalize/dedup per source event + ObservationKey, update each affected Order under short per-Order fences, retain unrelated aspects, and never lock one 180-Order transaction. Runtime load test proves throughput for the certified DCS adapter.

**Specification:** 03 section5.4; 04 P4.

**Assessment: DESIGN-REVIEWED; RUNTIME LOAD GATE REQUIRED**

---

# R. Extended ancillary and special-service coverage

## S-176 - Paid cabin upgrade

**Given/When:** P1 buys Economy->Business upgrade for one segment.

**Then:** Preserve original AirService commercial history and accepted successor/change treatment; upgrade benefit may be a typed/registered Service/change linked to the air segment. Price differential/tax/document treatment is explicit; do not mutate old fare basis silently.

**Specification:** 01 sections6/8; 02 servicing.

**Assessment: DESIGN-REVIEWED**

---

## S-177 - Free involuntary cabin upgrade

**Given/When:** operations moves passenger to higher cabin without customer purchase.

**Then:** Operational/reaccommodation evidence changes cabin fulfillment; no CustomerBalance charge is created unless an accepted commercial policy explicitly produces one. Preserve sold cabin snapshot.

**Specification:** 03 section7.3; 02 sign/effect.

**Assessment: DESIGN-REVIEWED**

---

## S-178 - Involuntary cabin downgrade with compensation

**Given/When:** paid Business service is delivered in Economy.

**Then:** record downgrade impact and delivered reality; compensation/refund amount is a new Pricing decision/PriceChangeSet, not the price allocation or a DCS side effect.

**Specification:** 03 section8; 02 refund rules.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-179 - Priority / fast-track / CIP

**Given/When:** passenger buys priority boarding, security fast-track or CIP access, possibly airport/time scoped rather than flight scoped.

**Then:** Registered service schema declares beneficiaries, location/time/flight coverage and delivery profile. No new aggregate/type table is required until behavior/query volume justifies it.

**Specification:** 01 GenericServiceDetails; 02 ancillary model.

**Assessment: DESIGN-REVIEWED**

---

## S-180 - Complimentary wheelchair assistance / SSR-like service

**Given/When:** assistance has zero separate customer price but requires operational delivery tracking and sensitive details.

**Then:** Create Service because independent fulfillment matters; `PriceTreatment=Complimentary/Included` rather than fake zero PricingLine. Sensitive details live behind protected payload ref/minimized view.

**Specification:** 01 sections6/22.

**Assessment: DESIGN-REVIEWED**

---

## S-181 - UMNR service

**Given/When:** unaccompanied minor service includes guardian/contact evidence, fee, provider acceptance and delivery handoffs.

**Then:** registered versioned service schema + protected sensitive payload + normal PricingLines/fulfillment refs. Guardian data is not copied into financial outbox. Provider rejection/unknown blocks readiness rather than silently confirming service.

**Specification:** 01 GenericServiceDetails/PII; 03 provider outcomes.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-182 - Pet in cabin (PETC)

**Given/When:** one traveler brings approved pet in cabin with segment-scoped fee/capacity restriction.

**Then:** registered service details carry pet/product class and protected evidence reference; reservation/fulfillment profile carries required provider capacity confirmation. Do not model pet as Traveler.

**Specification:** 01 section6; 03 reservation.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-183 - Animal in hold (AVIH)

**Given/When:** pet/animal travels in hold with supplier-specific weight/container constraints.

**Then:** separate registered service profile from normal baggage when fulfillment/risk differs; quantity/weight units explicit. Provider acceptance is authoritative; pricing remains generic line model.

**Specification:** 01 section6.4.1; 02 ancillary pricing.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-184 - Sports equipment or musical instrument baggage

**Given/When:** bicycle/ski/musical instrument has special dimensions, piece/weight rule and one or more covered segments.

**Then:** BaggageServiceDetails `SpecialItemCode`, units and coverage express it; no new aggregate or pricing ledger is introduced. Provider/product-specific limits remain configuration/terms.

**Specification:** 01 BaggageServiceDetails; 02 baggage pricing.

**Assessment: DESIGN-REVIEWED**

---

## S-185 - Extra seat / cabin baggage seat consumes capacity without a second traveler

**Given/When:** traveler buys an additional seat for comfort/instrument.

**Then:** model a registered segment-scoped ancillary Service associated with the traveler/AirService and `RequiresReservation=true`, with explicit requested/confirmed capacity quantity in supplier reservation evidence. Do not create a fake Traveler; do not create a second AirTransportService unless source actually sells a passenger transport segment.

**Specification:** 01 sections6/11.

**Assessment: DESIGN-REVIEWED**

---

## S-186 - WiFi pass

**Given/When:** WiFi sold per segment, journey or time window.

**Then:** registered service coverage carries the appropriate scope; delivery/consumption independent of ticket coupon. One pricing ledger supports flat/time/package charge without WiFi-specific money entities.

**Specification:** 02 section8.10; 01 GenericServiceDetails.

**Assessment: DESIGN-REVIEWED**

---

## S-187 - SIM/eSIM product

**Given/When:** eSIM is sold for a destination/time/data allowance and fulfilled by third party.

**Then:** registered schema defines delivery code/reference, allowance and validity; no flight segment is mandatory. Third-party activation/consumption is external delivery evidence; commercial Order remains local.

**Specification:** 01 GenericServiceDetails; 03 owner matrix.

**Assessment: DESIGN-REVIEWED / DEFERRED ADAPTER**

---

## S-188 - Hotel stay partially shortened

**Given/When:** three-night room stay is changed to two nights.

**Then:** supplier/pricing returns an explicit supported change; preserve original stay/pricing and successor/current service treatment. Do not mutate historical NightCount/money or assume one-third refund.

**Specification:** 01 HotelServiceDetails; 02 hotel pricing/servicing.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-189 - Shared ground transfer moves after flight change

**Given/When:** one vehicle serves two travelers and pickup must move after reaccommodation.

**Then:** change shared Transfer Service once if all beneficiaries remain together; if travelers split, require supplier/pricing partition or reject unsupported partition. Never duplicate vehicle/charge for convenience.

**Specification:** 01 sections6.4.1/6.5.

**Assessment: DESIGN-REVIEWED**

---

## S-190 - Lounge access with guest count and time window

**Given/When:** traveler + one guest has lounge access 10:00-14:00; flight delay crosses window.

**Then:** sold service remains historical; operational disruption may require supplier extension/replacement. Guest allowance and access window are service details, not PricingAllocation. Any refund is explicit quote/policy.

**Specification:** 01 LoungeServiceDetails; 02 lounge pricing.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-191 - Mixed bundle partially consumed, remaining service cancelled

**Given/When:** bundle includes bag+seat+priority; priority consumed, seat unused, bag delivered.

**Then:** keep each Service delivery state independent while bundle OrderItem remains commercially traceable. Partial refund/cancel uses Pricing result and historical allocations only as valuation evidence; consumed portions are not erased.

**Specification:** 02 bundle/refund; 03 delivery.

**Assessment: DESIGN-REVIEWED**

---

## S-192 - Bundle spans two passengers then Order is split

**Given/When:** one package price covers Services for P1/P2 and P1 moves to child Order.

**Then:** preserve Service IDs; partition item/value only if source allocation/pricing supports it, otherwise require quote/manual resolution. Do not duplicate bundle price or services in both Orders.

**Specification:** 01 split lineage; 02 split pricing.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-193 - Ancillary reassociated after flight rebooking

**Given/When:** paid bag/seat/meal depends on old AirService which is replaced.

**Then:** dependency policy returns Keep/Reassociate/Replace/Cancel/ManualReview per ancillary. Reassociation is explicit and preserves old binding history; EMD-A/document links are exchanged/reassociated separately.

**Specification:** 01 section6.5; 07 change eligibility.

**Assessment: DESIGN-REVIEWED**

---

## S-194 - Included baggage plus separately purchased extra bag

**Given/When:** fare includes 1pc and customer buys +1pc.

**Then:** distinguish included entitlement Service/terms from separately priced extra-bag Service (or explicit product-defined combined service if source sells it that way). Extra charge/refund cannot accidentally reverse included allowance.

**Specification:** 01 PriceTreatment/BaggageServiceDetails; 02 pricing.

**Assessment: DESIGN-REVIEWED**

---

## S-195 - Pooled family baggage allowance

**Given/When:** source sells a 40kg pool to two beneficiaries rather than two independent 20kg entitlements.

**Then:** one shared Baggage Service may list both beneficiaries, `AllowanceBasis=SharedPool`, explicit pooling policy and coverage. Split requires supported partition; consumption tracks quantity against shared pool without inventing per-person 20kg allocations.

**Specification:** 01 sections6.4/6.5.

**Assessment: DESIGN-REVIEWED**

---

# S. Pricing, tax, charge and servicing edge coverage

## S-196 - Per-bound charge

**Given/When:** source charges 20 once for outbound bound containing two connected segments.

**Then:** one PricingLine `ApplicationLevel=Bound` amount20 with appropriate basis/coverage; do not multiply by segment count. Allocation may attribute value but does not redefine application quantity.

**Specification:** 02 charge application/allocation.

**Assessment: DESIGN-REVIEWED**

---

## S-197 - Per-journey charge

**Given/When:** one fee applies to whole round-trip journey/accepted product.

**Then:** charge once at Journey/OrderItem pricing basis as source states; no automatic outbound/inbound duplication.

**Specification:** 02 section7.

**Assessment: DESIGN-REVIEWED**

---

## S-198 - Per-ticket/document charge

**Given/When:** ticketing fee applies once per issued document for two passengers.

**Then:** two document-application charge instances only if source quote explicitly prices per document. Pricing basis remains commercial quote context; document refs are execution/application evidence, not generic PricingLine scope confusion.

**Specification:** 02 F-P10; 01 documents.

**Assessment: DESIGN-REVIEWED**

---

## S-199 - Per-booking charge

**Given/When:** one booking/service fee covers four passenger Services.

**Then:** record exactly one Order/OrderItem customer charge. Do not allocate/multiply into four customer charges; allocations are optional value attribution.

**Specification:** 02 section7.

**Assessment: DESIGN-REVIEWED**

---

## S-200 - Per-person charge

**Given/When:** source fee is 5 per traveler for 3 travelers and reports unit price/quantity.

**Then:** persisted extended amount is 15 once, with source quantity semantics retained. Do not multiply an already extended source total again.

**Specification:** 02 Quantity/Application; 08 source amount normalization.

**Assessment: DESIGN-REVIEWED**

---

## S-201 - Tax jurisdiction changes on exchange

**Given/When:** replacement itinerary changes airport/country and Pricing returns old-tax reversals plus new taxes.

**Then:** preserve original tax occurrences/rule refs; append exact reversals and new tax lines. Never mutate tax code/scope on original sale or calculate tax inside Order.

**Specification:** 02 sections3.5/9.

**Assessment: DESIGN-REVIEWED**

---

## S-202 - Non-refundable tax while fare is refundable

**Given/When:** refund quote credits fare100, tax A20, retains non-refundable tax B10 and adds penalty15.

**Then:** commit only provider-approved lines; customer refund is 105, not blanket sum of allocations/taxes. Tax/refundability remain per line/rule occurrence.

**Specification:** 02 refund and tax rules.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-203 - Carrier surcharge YQ/YR treated separately from fare and tax

**Given/When:** exchange/refund returns different treatment for Fare, YQ and government Tax.

**Then:** distinct ComponentTypes survive reversal/reprice; no classification change to make arithmetic convenient. Ledger/pricing policies can treat each independently.

**Specification:** 02 sections3/7.

**Assessment: DESIGN-REVIEWED**

---

## S-204 - Capped promotion across fare and ancillary

**Given/When:** 10% promotion capped at 30 across Fare250 + Bag100.

**Then:** accepted quote records Discount30 once; any allocations preserve source/derived method. Partial cancellation/refund does not recompute promotion locally unless Pricing returns a new result.

**Specification:** 02 discount/allocation/refund.

**Assessment: DESIGN-REVIEWED**

---

## S-205 - Commission basis gross versus net

**Given/When:** one agency agreement computes commission on fare excluding tax, another uses configured eligible components.

**Then:** Ordering stores source-provided Commission SettlementOnly line + calculation/reference metadata; CustomerTotal unaffected. It does not implement agency commission policy from scratch.

**Specification:** 02 commission/sign matrix.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-206 - Original fare, sale currency and document currency differ

**Given/When:** fare filed USD, sale EUR, document/accounting presentation uses another allowed currency/reference.

**Then:** retain the accepted original/sale/document monetary values and source-applied conversion provenance required by the actual platform contracts. Historical reversal/service operations do not look up a new current rate merely to rewrite the original accepted transaction.

**Specification:** 02 monetary/conversion provenance; 01 shared semantics; 13.

**Assessment: DESIGN-REVIEWED**

---

## S-207 - Currency precision/profile differs

**Given/When:** the accepted source/platform monetary profile differs by currency or provider.

**Then:** use the authoritative existing platform/source profile and preserve the accepted result/provenance. Ordering must not impose a hard-coded decimal scale or invent a rounding profile. If current platform contracts conflict or do not establish the representation required for the operation, raise `BLOCKED_DECISION`.

**Specification:** 01 shared semantics; 02 monetary provenance; 13.

**Assessment: DESIGN-REVIEWED**

---

## S-208 - Allocation creates a smallest-unit residual

**Given/When:** an authorized allocation of an already accepted total across several targets cannot divide exactly under the platform/source monetary representation.

**Then:** use the allocation/rounding treatment already defined by the authoritative source or existing AeroTech platform policy and prove conservation of the accepted parent total. Ordering must not invent a residual-as-charge or a new rounding algorithm. If no authoritative treatment exists, automated allocation is blocked for owner decision.

**Specification:** 02 allocation; 13.

**Assessment: DESIGN-REVIEWED**

---

## S-209 - Exchange produces add-collect versus residual/refund

**Given/When:** one quote requires +70 collection; another produces -40 customer credit.

**Then:** both are normal PriceChangeSets with explicit Debit/Credit customer effect; payment/refund execution is external application. Do not encode exchange outcome as an OrderStatus or mutate old lines.

**Specification:** 02 section9; 03 payment boundary.

**Assessment: DESIGN-REVIEWED**

---

## S-210 - Historical pricing context unavailable

**Given/When:** old provider-defined fare has opaque context and current Pricing cannot defensibly price exchange/refund.

**Then:** block automated servicing with named `PricingContextUnavailable/ManualReview`; do not reverse-engineer a fare, apply current rules, or guess a pro-rata refund.

**Specification:** 01 CommercialTermsSnapshot; 02 servicing.

**Assessment: DESIGN-REVIEWED**

---

# T. Group, fan-out and concurrency completion cases

## S-211 - Group booking spans several flights with different capacities

**Given/When:** block is OUT50 / MID50 / IN40 and user requests 45 complete itineraries.

**Then:** reject/partially accept only according to group policy because IN block has 40; never sum capacities across flights. Each named itinerary consumes one unit from each required block.

**Specification:** 08 section11; 01 GroupBooking.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-212 - Partial group materialization

**Given/When:** 20 of 50 names are finalized today; remaining 30 stay unnamed.

**Then:** materialize only 20 stable rows/Orders/services; preserve remaining NameSlots/block capacity. Retry does not recreate first 20.

**Specification:** 08 section11.

**Assessment: DESIGN-REVIEWED**

---

## S-213 - Group passenger name replacement before deadline

**Given/When:** one allocated name must be replaced according to group policy before issue/deadline.

**Then:** explicit row/name operation preserves audit and external refs; do not mutate unrelated travelers or duplicate capacity. After document issuance, use normal correction/exchange rules instead of group shortcut.

**Specification:** 01 GroupBooking; 07 traveler/document eligibility.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-214 - Group deadline after partial materialization

**Given/When:** 35/50 names materialized and name deadline expires.

**Then:** policy releases/handles only unallocated remaining slots/capacity as authorized; already materialized Orders remain their own commercial records. One root group status cannot erase them.

**Specification:** 01 GroupBooking; 08 section11.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-215 - Group deposit allocated across materialized Orders once

**Given/When:** external deposit1000 is approved and 10 child Orders receive 100 each.

**Then:** funding application totals 1000 exactly and is idempotent by application identity. Never copy deposit1000 onto every child; actual deposit balance remains external owner truth.

**Specification:** 08 section11; 03 payment boundary.

**Assessment: DESIGN-REVIEWED**

---

## S-216 - Partial group ticket issue failure

**Given/When:** 49 ticket rows issue, 50th returns Unknown.

**Then:** retain first 49 document/economic results; reconcile exactly the unresolved 50th using stable row/operation/stock keys. Retrying batch must not reissue/recharge 49 successes.

**Specification:** 08 section11; 07 durable operations.

**Assessment: DESIGN-REVIEWED**

---

## S-217 - Charter ticketing with no classical fare construction

**Given/When:** charter contract gives accepted per-passenger/package commercial price but no PU/FC while real ETKT is required.

**Then:** `AirFareConstruction=null` is valid; PricingLines/terms/document snapshot still complete. Ticket issuance does not require fabricated fare components unless issuer contract explicitly requires a mapped fare basis/context.

**Specification:** 01 section7; 02 dynamic/charter pricing.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-218 - Aircraft change fan-out invalidates many paid seats

**Given/When:** one aircraft change affects 1,000 Orders and 600 paid seat Services.

**Then:** FlightOps fan-out updates impacts by indexed Service/segment in resumable batches; seat replacement/refund operations are not executed inside the fan-out transaction. No global lock. P5 load gate proves bounded per-Order transaction time.

**Specification:** 03 sections7/8; 04 P5.

**Assessment: DESIGN-REVIEWED; RUNTIME LOAD GATE REQUIRED**

---

## S-219 - Flight cancellation races DCS and agent servicing

**Given/When:** cancellation fan-out, passenger Boarded/Offloaded correction and voluntary cancel attempt target the same Order concurrently.

**Then:** per-Order fence/concurrency prevents lost updates; DCS evidence remains append-only, operational impact projection is current, and only an eligible commercial operation finalizes. No cross-Order transaction is introduced.

**Specification:** 03 sections5/7; 04 projector/fence; 07 concurrency.

**Assessment: DESIGN-REVIEWED; RUNTIME CONCURRENCY TEST REQUIRED**

---

## S-220 - Affected-flight search and normal GetOrder stay local

**Given/When:** operations needs every Order affected by flight/date while normal users fetch individual Orders under disruption load.

**Then:** `FlightOrderIndex` resolves impacted Order/Service IDs without external fan-out; `GetOrder` uses local read projection and remains independent of FlightOps/DCS/Payment synchronous calls. Load tests prove query isolation for deployment targets.

**Specification:** 04 read model/indexes; 03 fan-out.

**Assessment: DESIGN-REVIEWED; RUNTIME LOAD GATE REQUIRED**

---

# U. Cancellation, void and refund benchmark completion

## S-221 - Pre-ticket cancellation with no captured value

**Given/When:** An unpaid, unticketed reservation is cancelled.

**Then:** Cancel/release only the accepted commercial services and capacity according to provider evidence. Do not fabricate a ticket refund or a payment transaction.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-222 - Pre-ticket cancellation after value was captured

**Given/When:** The order has no ticket but Payment confirms captured value before cancellation.

**Then:** Commercial/capacity cancellation and monetary refund are separate correlated outcomes. Payment/StoredValue owns value return; Ordering records evidence and must not mark refund complete from commercial cancellation alone.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-223 - Document void inside provider eligibility

**Given/When:** An issued unused ETKT/EMD is confirmed voidable by the issuer/provider.

**Then:** Persist one durable void intent, execute/reconcile with the provider, record document outcome and then coordinate the applicable payment/value treatment. Do not infer provider eligibility from local elapsed-time rules.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-224 - Void denied or window expired

**Given/When:** A customer requests void but issuer/provider reports the document is not voidable.

**Then:** Do not force a Void state. Evaluate the request through normal cancellation/refund servicing with an authoritative quote/result.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-225 - Document void confirmed while payment outcome is unresolved

**Given/When:** Issuer confirms void, while the payment release/refund returns Unknown/Pending.

**Then:** Keep document and monetary outcomes distinct. Order visibility shows the confirmed void and unresolved monetary operation until the payment owner reconciles it.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-226 - Void timeout after provider may have committed

**Given/When:** The void provider times out after receiving the stable operation key.

**Then:** Record Unknown, retain the same operation/document key, reconcile/read back, and never create a second void/refund merely because transport timed out.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-227 - Commercial cancellation with zero refund

**Given/When:** Pricing approves cancellation but returns no refundable/reusable value.

**Then:** Cancel the accepted scope only after required execution guards; retain the source financial decision. Zero refund is an explicit outcome, not inferred from status.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-228 - Cancellation with a fee

**Given/When:** Pricing returns a cancellation charge/penalty for the requested scope.

**Then:** Record the accepted penalty independently from reversals/refund lines and preserve whether it is netted or separately payable. Ordering does not calculate the fee.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-229 - Cancellation accepted before cash return completes

**Given/When:** The commercial scope is cancelled and an approved refund instruction exists, but Payment has not completed it.

**Then:** Commercial cancellation may be final while refund execution is pending/unknown when policy allows; UI/read model must expose both facets, never a single misleading CancelledAndRefunded state.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-230 - Cancel one passenger without cancelling companions

**Given/When:** One traveler in a multi-passenger Order cancels selected unflown services.

**Then:** Scope servicing to affected stable services/items and dependent services only. Preserve unaffected travelers, documents, fare context and values.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-231 - Full unused refundable ticket

**Given/When:** A fully unused ticket is quoted as fully refundable.

**Then:** Commit only the authoritative accepted refund breakdown; coordinate document refund/coupon outcome and external value return; create a final servicing record after outcomes are known.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-232 - Non-refundable fare with refundable taxes

**Given/When:** Fare is non-refundable while the source refund decision returns selected taxes.

**Then:** Preserve fare forfeiture and each refundable tax occurrence separately; customer refund equals the authoritative decision, not blanket non-refundable ticket value.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-233 - Refundable fare with non-refundable tax/charge

**Given/When:** Fare is refundable while one tax/charge is retained.

**Then:** Preserve the retained and refunded components exactly as returned; do not assume all taxes are refundable because fare is.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-234 - Partially used ticket refund reprices used portion

**Given/When:** Outbound is flown and customer refunds unused return; Pricing/Refund owner reprices/values the used portion.

**Then:** Use the source-approved used/unused/refund result. Never compute refund as original total minus service allocations.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-235 - Tax-only refund

**Given/When:** No fare is refundable but source authorizes a tax-only refund.

**Then:** Represent the tax credit and document/payment effect without inventing a fare reversal.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-236 - Refund penalty netted from customer credit

**Given/When:** Refund result contains customer credit and a penalty explicitly netted by source.

**Then:** Retain gross refund components, penalty and netting treatment so final value is auditable; do not double-charge the penalty through Payment.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-237 - Refund penalty collected separately

**Given/When:** Source requires a penalty to be collected before/alongside a later refund or residual.

**Then:** Create/track the separate payable treatment through the payment owner and do not reduce refund again unless source decision explicitly nets it.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-238 - Refund with waiver/authority evidence

**Given/When:** Source/carrier authorizes waiver of normally applicable fee/condition.

**Then:** Preserve waiver/authority/reason reference with the servicing decision; do not create local waiver policy or accept untrusted caller text as authority.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-239 - Refund to original form of payment

**Given/When:** Source/payment contract requires return to original tender.

**Then:** Ordering carries the approved value instruction/reference; Payment routes to the actual original FOP and owns execution/reconciliation.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-240 - Refund as residual/voucher/stored value

**Given/When:** Source approves value for future use instead of direct cash refund.

**Then:** Represent the commercial residual/reusable outcome and hand off to the current stored-value/payment owner. Do not create a wallet/voucher ledger inside Ordering.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-241 - Reusable value known only qualitatively

**Given/When:** Order/item is reusable but source cannot calculate exact value until later reshop/refund.

**Then:** Store reusable eligibility/evidence only; do not persist an estimated reusable amount as truth.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-242 - Refund from an order paid by multiple forms of payment

**Given/When:** One order was funded by several tenders and a refund is approved.

**Then:** Do not derive tender routing from price allocations. Payment/StoredValue determines refund applications under its authoritative contract; Ordering retains correlated facts.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-243 - Payment refund returns Unknown

**Given/When:** Approved refund is dispatched and provider result becomes Unknown.

**Then:** Keep refund operation unresolved and reconcile with the same durable key. Do not retry as a new economic operation or mark servicing fully complete.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-244 - Duplicate refund request/retry

**Given/When:** Same business refund request is repeated after response loss.

**Then:** Idempotency returns/reconciles the same servicing/payment operation; there is at most one economic refund effect per approved instruction.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-245 - Cancel a processed refund

**Given/When:** A previously finalized refund must be cancelled/corrected under provider rules.

**Then:** Do not edit/delete the processed Refund/ServicingRecord. Execute a new corrective operation with lineage and create a new final record/evidence.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

# V. Voluntary change, revalidation and exchange/reissue

## S-246 - Informative reshop without mutation

**Given/When:** Agent requests flight/date change options.

**Then:** Return candidate commercial/pricing outcomes without mutating Order, reservation, documents or payment.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-247 - Stale accepted change quote

**Given/When:** A change quote was obtained at CommercialVersion N but Order changed before acceptance.

**Then:** Reject/requote rather than applying stale financial/document instructions.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-248 - Even exchange without penalty

**Given/When:** Source returns replacement itinerary with no customer balance change.

**Then:** Change services/reservation and perform required document outcome; no add-collect/refund is invented merely because a reissue occurs.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-249 - Even exchange with separately collected penalty

**Given/When:** Fare/tax difference is zero but source returns a separately payable change penalty.

**Then:** Track zero ticket difference and separate penalty payment treatment; do not encode penalty into new fare merely for arithmetic convenience.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-250 - Change with fare/tax add-collect

**Given/When:** New itinerary requires additional fare and/or tax.

**Then:** Preserve accepted old/new/differential provenance and collect exactly the authoritative additional amount before irreversible issue when required.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-251 - Change creates residual value

**Given/When:** New itinerary is cheaper and source authorizes residual/reusable value.

**Then:** Preserve residual independently from new fare and penalty; hand value creation/application to the current owner.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-252 - Change has add-collect and residual components

**Given/When:** Source response legitimately contains both additional collection and residual/value outcomes across components.

**Then:** Keep each component/treatment independent and use source net/payment instructions; never collapse to one signed scalar that loses audit meaning.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-253 - Change has add-collect and refund

**Given/When:** Source returns additional collection for one component and refund/credit for another.

**Then:** Record both accepted outcomes and let payment owner execute required value movements; do not offset locally unless source explicitly instructs netting.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-254 - True round trip return change reprices coupled pricing unit

**Given/When:** Return changes on a true RT pricing unit.

**Then:** Send the coupled fare context to Pricing and accept whole-pricing-unit treatment where returned; service scope alone does not authorize proportional refund/reprice.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-255 - Independent OW plus OW return change

**Given/When:** Return belongs to an independent one-way pricing unit.

**Then:** Pricing may leave outbound unchanged and reprice only return; preserve source fare construction rather than inferring coupling from itinerary shape.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-256 - Exchange after partial travel

**Given/When:** Outbound coupons are used and only remaining unflown scope changes.

**Then:** Preserve used service/coupon history and successor lineage for changed unflown services; source repricing determines retained historical value.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-257 - Eligible revalidation

**Given/When:** Accepted change can be reflected on existing accountable document without replacement under issuer/provider rules.

**Then:** Keep same document identity, record coupon revalidation/evidence, and do not create a replacement ticket or exchange lineage.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-258 - Revalidation not supported; reissue required

**Given/When:** Source/provider rejects revalidation for the accepted change.

**Then:** Do not simulate it. Follow an explicit reissue/exchange plan or block/manual-review according to capability.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-259 - Exchange/reissue creates successor document lineage

**Given/When:** Accepted change requires a new ETKT/EMD.

**Then:** Record old/new document/coupon lineage, preserve original issue facts and correlate pricing/payment/document outcomes to one servicing operation.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-260 - Exchange/reissue provider outcome Unknown

**Given/When:** Provider may have issued a successor document but response is lost.

**Then:** Do not issue another number blindly. Reconcile same operation/stock/provider key before finalizing or retrying.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

# W. Penalties, waivers and no-show

## S-261 - Fixed servicing penalty

**Given/When:** Authoritative source returns a fixed penalty.

**Then:** Record the returned amount, scope and source evidence; Ordering does not calculate it from a locally coded rule.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-262 - Percentage-based penalty

**Given/When:** Source returns a calculated penalty whose underlying rule is percentage-based.

**Then:** Record accepted result and relevant provenance needed for audit; Ordering does not become a percentage penalty engine.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-263 - Penalty with minimum/maximum/higher/lower rule

**Given/When:** Source fare rules choose a penalty using a comparative/min/max rule.

**Then:** Persist only authoritative result/rule evidence necessary for history; do not reproduce the calculation merely to validate the provider.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-264 - Before-departure versus after-departure penalty

**Given/When:** Same requested operation has different source result depending on timing/travel state.

**Then:** Use authoritative request context and result; do not hard-code one penalty onto product/order status.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-265 - Penalty scoped to fare component/pricing unit/ticket

**Given/When:** Source returns a fee scoped above/below individual service granularity.

**Then:** Preserve source scope and allocations only when provided/authorized; do not fabricate per-segment penalties.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-266 - One/highest/combined fee treatment

**Given/When:** Source rules select one fee or combination across several changed components.

**Then:** Ordering accepts the returned servicing result as a whole; it does not sum independently discovered penalties and risk double charging.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-267 - DCS no-show observation alone

**Given/When:** DCS reports NoShow for an outbound segment.

**Then:** Record operational evidence only. NoShow alone does not create penalty, refund, cancellation or forfeiture.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-268 - No-show penalty returned by Pricing

**Given/When:** A subsequent servicing request receives an authoritative no-show fee/forfeit result.

**Then:** Record and execute that explicit commercial outcome with provenance; never infer it from DCS status.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-269 - No-show with onward/return service retained

**Given/When:** Carrier policy/source leaves future segments active after a no-show.

**Then:** Do not auto-cancel onward services; current commercial state stays source-driven.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-270 - No-show with explicit onward cancellation decision

**Given/When:** Carrier/provider produces an explicit decision to cancel remaining services after no-show.

**Then:** Apply only that authorized scope via normal servicing, preserving no-show evidence and cancellation/financial decision separately.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-271 - Schedule-change fee waiver

**Given/When:** Involuntary schedule-change recovery carries a carrier waiver/authority.

**Then:** Preserve authority and apply source pricing result; do not merely set every penalty to zero locally.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-272 - Medical/death or other discretionary waiver

**Given/When:** An authorized business process grants a discretionary waiver.

**Then:** Require trusted authority evidence and source repricing/refund result. Ordering does not implement policy from free-text reason.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

# X. Ancillary and EMD servicing

## S-273 - Paid seat reassociated to replacement flight

**Given/When:** Voluntary/involuntary flight change keeps a compatible paid seat product.

**Then:** Evaluate dependency/provider treatment and reassociate/replace explicitly; retain original service/value history.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-274 - Paid seat unavailable after aircraft change

**Given/When:** Aircraft change makes purchased seat characteristic unavailable.

**Then:** Expose non-delivery/impact and request authoritative keep/replace/refund/compensation treatment; never silently mark delivered or auto-refund from original allocation.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-275 - Baggage product follows flight rebooking

**Given/When:** Air service is replaced and purchased baggage remains valid or must be reissued.

**Then:** Dependency evaluation uses supplier/issuer source plan; preserve service identity when continuing and document lineage when reissued.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-276 - Lounge access invalid after airport change

**Given/When:** Replacement departs from different airport and old lounge cannot be delivered.

**Then:** Mark delivery impact, obtain source servicing value treatment, and replace/cancel/refund as authorized.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-277 - Meal on changed flight

**Given/When:** Meal service depends on replaced flight.

**Then:** Explicitly keep/reassociate/replace/cancel according to provider/product result; do not infer from air change alone.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-278 - Partial hotel stay cancellation during itinerary change

**Given/When:** Third-party hotel stay is shortened/cancelled for some nights.

**Then:** Supplier/Pricing owns cancellability and refund value. Ordering records accepted service change and supplier/financial evidence; no local nightly pro-rata assumption.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-279 - Optional service is non-refundable but reusable

**Given/When:** Source optional-service rule permits reuse rather than refund.

**Then:** Keep service/value treatment distinct and defer exact reuse amount until authoritative reshop if unknown.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-280 - Purchased reissue/refund option overrides normal rule

**Given/When:** Customer bought an optional service that changes reissue/refund conditions.

**Then:** Pass the source product/entitlement evidence to Pricing; apply returned override result without coding an ATPCO rule engine in Ordering.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-281 - EMD-A exchange/reassociation

**Given/When:** Associated EMD service changes together with related air service.

**Then:** Preserve in-connection document/service linkage and execute provider-supported reassociation/exchange/void/refund outcome with old/new lineage.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-282 - EMD-S used to collect a fee/penalty

**Given/When:** Issuer/payment plan requires a standalone EMD for a servicing fee.

**Then:** Allow document to reference the monetary purpose/operation without inventing a deliverable OrderService.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

# Y. Involuntary, correction, records and platform boundaries

## S-283 - Planned schedule change accepted with revalidation

**Given/When:** Airline changes schedule and customer accepts; provider says revalidation is sufficient.

**Then:** Preserve involuntary reason/authority, update current itinerary via explicit change, revalidate same document, and record no invented fare difference.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-284 - Schedule change requires even reissue

**Given/When:** Carrier recovery requires replacement ticket with no customer balance change.

**Then:** Perform explicit reissue/even exchange, keep old/new document lineage and involuntary reason; no voluntary penalty is inferred.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-285 - Cancelled flight reaccommodation accepted

**Given/When:** Flight is cancelled; customer accepts replacement.

**Then:** FlightOps/disruption fact triggers an explicit recovery plan; capacity/document/ancillary outcomes are executed and reconciled under stable operation identities.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-286 - Customer rejects disruption recovery and requests refund

**Given/When:** Customer declines offered reaccommodation after qualifying disruption.

**Then:** Obtain authoritative involuntary refund result/waiver and execute cancellation/document/value flows separately. Do not assume every disrupted ticket is automatically cash refundable.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-287 - Misconnection recovery with dependent ancillaries

**Given/When:** Misconnection changes downstream flights while seats/baggage/lounge/meal exist.

**Then:** Recovery evaluates every dependent service for keep/reassociate/replace/cancel/refund/manual treatment and prevents orphaned paid services.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-288 - Involuntary servicing retains authority and reason

**Given/When:** Disruption operation changes/refunds documents under carrier authorization.

**Then:** Persist involuntary source event/reason/waiver authority and do not relabel it as passenger-requested servicing.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-289 - Name correction may require fee or document action

**Given/When:** Traveler name correction is requested after booking/issue.

**Then:** Use provider/issuer/customer-identity contract to determine allowed mutation/reissue/new issue/fee path. Do not hard-code a universal airline name-change rule.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-290 - Finalized refund servicing record and notice

**Given/When:** Refund completes and the customer/agency needs redisplay/audit plus a notice.

**Then:** Create an immutable semantic ServicingRecord from actual accepted pricing/document/payment facts. Generate/dispatch a Refund Notice only through the discovered platform artifact/notification owner; the notice is not Order truth.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-291 - Payment receipt is not an Ordering document

**Given/When:** Payment owner completes a collection/refund and a receipt is required.

**Then:** Reference/correlate the payment fact; receipt generation/ownership stays with the existing payment/document capability unless platform ownership is explicitly changed.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-292 - Historical conversion does not change during later servicing

**Given/When:** Current ROE differs from the rate/context used for original accepted sale.

**Then:** Preserve and use the source-approved historical/current servicing monetary provenance exactly as instructed by Pricing/AirPrice; Ordering must not look up a fresh rate merely to reverse history.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-293 - Commission changes on refund/reissue

**Given/When:** Source servicing result recalculates/adjusts commission.

**Then:** Preserve source-provided commission treatment independently from customer refund/payable. Do not derive commission from Customer/Core or assume it equals original commission.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-294 - Correction after processed refund

**Given/When:** A finalized refund record contains an operational mistake and provider authorizes correction.

**Then:** Create a new corrective servicing operation/record with lineage. Never mutate the original finalized record to look as if the first outcome never happened.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-295 - Checked-in coupon requires control release before exchange

**Given/When:** Passenger is checked in/under airport control and ticket change is requested.

**Then:** Obtain required DCS/control release acknowledgment before document action when provider contract requires it; Unknown control state blocks unsafe reissue.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-296 - Ticket coupon and DCS evidence disagree during servicing

**Given/When:** Ticket image says open while DCS/provider evidence says checked-in/used or vice versa.

**Then:** Do not choose the convenient state. Reconcile authoritative sources/manual-review before irreversible refund/exchange/void.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-297 - Shared ancillary during passenger split and refund

**Given/When:** A hotel/transfer/pooled allowance covers travelers that are split into different Orders and one side cancels.

**Then:** Do not duplicate the shared service/value. Require supplier/pricing partition or keep it indivisible/manual according to supported contract.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-298 - Third-party ancillary non-delivery

**Given/When:** Supplier cannot deliver a paid hotel/lounge/transfer/other service after sale.

**Then:** Record delivery failure independently, obtain authoritative refund/replacement/compensation decision and keep supplier/payment outcomes reconcilable.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-299 - No confirmed platform owner for customer notice rendering

**Given/When:** Servicing completes but repository/platform inspection finds no approved refund/exchange notice renderer/archiver.

**Then:** Record the semantic ServicingRecord but raise `BLOCKED_DECISION` for artifact ownership. Do not create a PDF/document subsystem inside Ordering as a convenience.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

## S-300 - Shared monetary representation is ambiguous during P0

**Given/When:** Existing services/contracts use incompatible or undocumented amount/currency/rounding representations for a required Ordering boundary.

**Then:** Document evidence and raise `BLOCKED_DECISION`; do not introduce a new platform-wide Money/Currency/FX abstraction from Ordering to resolve the ambiguity unilaterally.

**Specification:** `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md` and applicable `01`/`02`/`03`/`07`/`13` section.

**Assessment: DESIGN-REVIEWED; IMPLEMENTATION/PROVIDER TEST REQUIRED WHERE APPLICABLE**

---

# Z. Final benchmark-specific closure cases

## S-301 - Planned schedule change awaits customer decision

**Given/When:** recovery owner reports a planned schedule change that requires customer acceptance and supplies options/deadline.

**Then:** show the source-provided action-required state/options/deadline locally without changing the commercial promise yet. Acceptance/decline/reshop becomes an explicit servicing operation; Ordering does not invent the deadline behavior.

**Specification:** `03` disruption projection; `12` planned schedule-change decision window.

**Assessment: DESIGN-REVIEWED; RECOVERY-CONTRACT TEST REQUIRED**

---

## S-302 - Customer takes no action before disruption decision deadline

**Given/When:** schedule-change decision deadline expires with no customer action.

**Then:** execute only the disposition explicitly supplied by airline/recovery policy (for example accept/revalidate, cancel/release/refund, release while keeping reusable/open value, or manual handling). No default auto-cancel/refund/accept rule is coded in Ordering.

**Specification:** `03`; `12`.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-303 - Cancel after disruption but keep document/value for future use

**Given/When:** customer does not want immediate replacement or refund and source policy allows capacity release with document/value kept reusable/open.

**Then:** release/cancel current service capacity as instructed while preserving accountable-document/value evidence and reusable eligibility. Do not manufacture an immediate cash refund or wallet balance; exact future value remains source-driven.

**Specification:** `12` reusable/respend and involuntary sections.

**Assessment: DESIGN-REVIEWED / CONFIGURED POLICY**

---

## S-304 - Tax on a servicing penalty

**Given/When:** authoritative Pricing/tax/ticketing result contains a penalty plus a separate tax occurrence on that penalty.

**Then:** keep penalty and tax distinguishable and preserve the source treatment/provenance. Ordering does not calculate tax-on-penalty or disguise the penalty itself as tax.

**Specification:** `02` tax/penalty; `12` penalty taxation.

**Assessment: DESIGN-REVIEWED / SOURCE RESULT REQUIRED**

---

## S-305 - Cheaper exchange with no refundable residual

**Given/When:** replacement itinerary is cheaper but fare/source rules return no refund/residual/reusable value.

**Then:** do not automatically credit the arithmetic difference. Commit the authoritative exchange result and document lineage only.

**Specification:** `12` voluntary change; `02` servicing pricing.

**Assessment: DESIGN-REVIEWED / SOURCE RESULT REQUIRED**

---

## S-306 - Original refund method unavailable

**Given/When:** source/provider cannot return value to original form of payment and returns an allowed alternative (for example residual/EMD/voucher/manual path).

**Then:** preserve the approved value destination/treatment and delegate execution to the current value owner. Ordering does not choose a substitute tender or create stored value itself.

**Specification:** `12` refund destination/financial ownership.

**Assessment: DESIGN-REVIEWED / PROVIDER-CONTRACT TEST REQUIRED**

---

## S-307 - Ancillary EMD provider cannot perform requested partial refund/exchange

**Given/When:** commercial servicing result permits changing/refunding part of an ancillary, but the actual EMD provider capability supports only a narrower document operation.

**Then:** do not fake a document state. Use supported replacement/full-refund/manual/correction path supplied by provider/business policy or block the affected operation; commercial value and document capability remain separate.

**Specification:** `12` ancillary/EMD; `13` external-operation gate.

**Assessment: DESIGN-REVIEWED / PROVIDER-CAPABILITY TEST REQUIRED**

---

## S-308 - Involuntary recovery after a prior voluntary exchange

**Given/When:** ticket has an existing voluntary reissue chain and later disruption requires involuntary recovery.

**Then:** preserve the full document/service/pricing lineage and apply the new source-approved involuntary plan against the current accountable document. Do not reset history to the original ticket or assume previous voluntary status prevents recovery.

**Specification:** `12` exchange/involuntary; `01` document lineage.

**Assessment: DESIGN-REVIEWED / ISSUER-CONTRACT TEST REQUIRED**

---


# Validation conclusion and execution boundary

This catalogue now contains **308 design-reviewed scenarios**. The additional S-221..S-308 pass specifically closes the servicing/financial benchmark surface: pre-ticket cancellation, void, full/partial/part-used refund, voluntary change, revalidation, exchange/reissue, add-collect/refund/residual/reusable outcomes, penalties/waivers/no-show, ancillary/EMD servicing, involuntary recovery, name correction, processed servicing records/notices, and platform-decision gates.

Scenario count is not proof of runtime correctness or universal carrier/provider policy. Carrier-, market-, tax-, issuer-, settlement-plan- and provider-specific treatment still comes from the authoritative owner. The design deliberately does not encode a generic fare/refund/penalty/tax engine or a new monetary/framework stack inside Ordering.

The reviewed design avoids a global payment/ticket Order state, a separate Entitlement layer, a mandatory event store, forced 1:1 service/fare-component mapping, and normal read-time fan-out. Implementation must prove the relevant scenarios under real .NET/SQL/provider failures and contract tests.

See `09-VALIDATION-REPORT.md` for the exact document/reference checks executed during revision and their limits.
