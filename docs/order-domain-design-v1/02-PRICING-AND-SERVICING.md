# 02 - Pricing and Servicing Model

## 1. Separation of concerns

The final pricing design intentionally separates four questions:

```text
OrderItem
  What was sold/priced as one commercial item?

OrderService
  What can be serviced/delivered independently?

FareConstruction
  How was an accepted air fare constructed for repricing/rule context?

PricingLine + PricingAllocation
  What money was charged/credited and how is its value attributed?
```

Do not use one concept to impersonate another.
**Representation note.** Names such as `PriceChangeSet`, `PricingLine`, `Direction`, `Effect` and component labels describe required business semantics. The pack does not mandate a particular enum/value-object layout; implementation maps them to the existing AeroTech framework/contracts. Monetary/currency/FX representation is governed by `13`.


---

## 2. PriceChangeSet

Every accepted monetary mutation appends ONE `PriceChangeSet` in the same local transaction as its OrderChange and financial outbox event. A quote/proposal is not a committed line. The operation may have a zero delta, but pure delivery updates never fabricate a price change.

```text
PriceChangeSet
--------------
PriceChangeSetId
OrderId
ChangeId
FinancialSequence                 # contiguous per Order monetary commit
ExpectedCommercialVersion
Reason
Source
SourceOfferId?
SourcePricingRef?
CreatedAt
CommittedAt
```

Business reasons must remain distinguishable where they change servicing/audit semantics; the exact code representation follows repository conventions. Examples:

```text
OriginalSale
AddProduct
Reprice
VoluntaryChange
Exchange
InvoluntaryChange
Cancellation
Refund
Void
ManualAdjustment
Correction
SplitTransfer
```

A PriceChangeSet is immutable after commit.

---

## 3. PricingLine

### 3.1 Definition

A `PricingLine` is one immutable commercial monetary fact.

```text
PricingLine
-----------
PricingLineId
OrderId
PriceChangeSetId
OrderItemId?

ComponentType
Code?
Description?
Effect
Direction
LineRole                          # Original | Reversal | Adjustment | Transfer

OriginalValue : [platform monetary representation]             # non-negative magnitude
SaleValue : [platform monetary representation]                 # non-negative, Order sale currency
SourceAppliedConversionProvenance?  # semantic content only; map owner contract, do not create ROE subsystem

Refundability
ApplicationLevel?
Quantity?
UnitOfMeasure?
UnitPrice?                         # high precision unit rate, not final quantized total

BasisType
BasisReferenceId?

SourceLineRef?
OriginalPricingLineId?
OriginalAllocationId?
TransferGroupId?
RelatedOperationId?
CalculationSnapshot?
TaxDetails?
SettlementPartyRef?
SettlementCategory?
CreatedAt
```

### 3.2 ComponentType

```text
Fare
ProductCharge
Tax
CarrierSurcharge
Fee
Discount
Markup
Penalty
Commission
Adjustment
Other
```

`YQ/YR` should normally be `CarrierSurcharge`, not Fare or Tax.

### 3.3 Effect: one balance domain, not a second sign

```text
CustomerBalance
SettlementOnly
Informational
```

This explicitly replaces the ambiguous prior CustomerPayable/CustomerCredit values. A discount is `Effect=CustomerBalance, Direction=Credit`; the effect is NOT multiplied by a second negative sign. SettlementOnly requires party/category/currency and does not alter the customer's debt. Informational values are excluded from every payable total.

No GL posting direction is implied by these names. Ledger maps commercial facts to its own double-entry entries. The old repository's Credit-means-sale enum is mapped at its adapter, never reused without translation.

### 3.4 Binding sign and reversal contract

`OriginalValue.Amount >= 0` and `SaleValue.Amount >= 0`. Direction is the ONLY source of sign:

```text
sign(Debit) = +1
sign(Credit) = -1
signedSale(line) = sign(line.Direction) * line.SaleValue.Amount
CustomerTotal = SUM(signedSale for Effect=CustomerBalance)
```

The table is the complete allowed matrix. Entries name the normal original direction; `Both` requires a reason and explicit trusted pricing/authorized adjustment. `No` is rejected at validation. Every permitted original can have an opposite-direction Reversal with the SAME ComponentType and Effect.

| ComponentType | CustomerBalance | SettlementOnly | Informational |
|---|---|---|---|
| Fare | Debit | Both, supplier/contract valuation only | Both, display only |
| ProductCharge | Debit | Both, supplier/contract valuation only | Both |
| Tax | Debit | **Forbidden in Ordering v1** | Debit/Credit display only when explicitly non-customer monetary |
| CarrierSurcharge | Debit | Both, supplier/contract valuation only | Both |
| Fee | Debit | Both, explicit settlement basis | Both |
| Discount | Credit | Both, explicit settlement discount | Both |
| Markup | Debit | Both, explicit retailer economics | Both |
| Penalty | Debit | Both, explicit party treatment | Both |
| Commission | No; customer concession is Discount instead | Debit for commission entitlement; Credit to reduce it | Both |
| Adjustment | Both; mandatory code, reason, provenance | Both; mandatory party/category | Both |
| Other | No in v1 | No in v1 | Both; mandatory descriptive code |

A goodwill customer payment or refund exceeding prior charges is an authorized `Adjustment/Credit`, not an over-reversal. An increase to an existing fare/tax normally adds Original or Adjustment/Debit, not a fake negative reversal.

**Binding tax rule:** if a tax is included in the customer's accepted payable amount, persist it as `ComponentType=Tax, Effect=CustomerBalance` with Debit/Credit direction according to the commercial movement. Agency/government/partner settlement of that tax is represented by settlement/accounting facts outside this customer-balance line. `Tax + SettlementOnly` is rejected by Ordering v1. A source-side tax that the customer does not owe may be retained as `Informational` when needed for audit; it must not disappear into `SettlementOnly` and accidentally reduce `CustomerTotal`.

`LineRole=Reversal` requires `OriginalPricingLineId`, opposing Direction, identical component/effect/currencies and traceable historical FX. `OriginalPricingLineId != null` ALONE does not define reversal because adjustments, replacements and transfers can also have lineage. Reversal is never inferred from Cancellation/Void reason. A cancellation Penalty/Debit is a new charge.

For a full reversal, copy the original accepted monetary magnitudes and historical source-applied conversion evidence exactly, then invert Direction. For a partial reversal, cumulative direct reversed SaleValue/OriginalValue may not exceed the referenced outstanding value in either currency; use the original allocation or an explicit source split. Serialize this check with the Order monetary commit. Undoing a reversal is an explicit Correction/Adjustment with lineage, not a second reversal-chain algorithm.

A replacement quote may return component deltas or complete old/new amounts. Normalize once to exactly one recorded treatment per component. NEVER post the full new price AND the quote differential. Current balance caches are derived from committed lines; replacement snapshots are not another balance source.

**Example (synthetic EUR values):** Fare Debit 400 + Bag Debit 50 + Discount Credit 45 = CustomerTotal 405. Commission SettlementOnly Debit 20 leaves CustomerTotal 405. Reversing the discount adds CustomerBalance Debit 45 and yields 450, not 360.

---

### 3.5 Currency, conversion and rounding provenance

The previous draft prescribed an `FxSnapshot` type, SQL precision and a local rounding algorithm. Those implementation-specific instructions are **superseded**.

Binding business semantics are:

- every accepted customer-effective monetary component is interpretable in the sale/payment context expected by the source contract;
- if the authoritative Pricing/AirPrice/provider result contains original currency/value, converted/sale value, applied-rate reference/snapshot, conversion convention, rounding result or source calculation evidence needed for audit/reversal, Ordering preserves that accepted evidence;
- historical values are never recomputed with a newer rate/table or a new local rounding choice;
- Ordering does not silently sum different currencies or derive a missing conversion;
- Pricing/tax/provider is authoritative for calculated accepted values and source-specific rounding; Ordering validates only reconciliation that is unambiguously specified by the incoming contract;
- allocation of an accepted parent value may be performed locally only when the approved contract/business rule explicitly allows it and the existing platform already establishes the monetary/rounding representation. Otherwise the source must provide the split or implementation is blocked for a decision;
- a source mismatch is not repaired by inventing a rounding adjustment unless that adjustment is explicitly part of the authoritative source result/policy.

The concrete .NET type, currency key, numeric precision/scale, FX representation and rounding primitive must be discovered from the current AeroTech Framework and owning services. See `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md`.

### 3.5.1 Tax and calculation provenance

Tax and other calculated commercial components retain the **source evidence actually available and required** to explain/reverse/service the accepted result: source line/occurrence identity, tax/fee code where supplied, jurisdiction/rule reference when supplied, scope/application context, inclusion/exemption/refund treatment when supplied, and related source-calculated amount/base/rate data when the source contract exposes it.

Ordering does not create a generic tax/formula engine. Same tax code on different occurrences/scopes remains distinguishable when the source distinguishes it. Transfer/stopover tax logic, exemptions, tax-on-tax, fee formulas and quantity formulas are calculated by the authoritative Pricing/Tax/Supplier owner.

For an inclusive-price result, store exactly one authoritative commercial decomposition. If the source returns an accepted gross with no reliable net/tax breakdown, Ordering must not fabricate a zero tax or synthetic split merely to satisfy a local schema. Any downstream operation that genuinely requires a missing breakdown is blocked until the authoritative owner can provide one.

Per-document/per-journey/per-bound/per-service application context may be preserved before a physical ticket number exists using the source/planned scope identity. A pricing basis describes why/how value was assessed; it is not replaced by PaymentId or document-number ownership.


### 3.6 Pricing basis

`BasisType` means **where/why the pricing calculation applies**, not how it was paid or documented.

```text
Order
OrderItem
OrderService
Journey
Segment
PricingUnit
FareComponent
ExternalCharge
```

Do not include Payment, Ticket or Coupon as interchangeable commercial pricing bases. Retain explicit TriggerContext/RelatedOperationId and DocumentPriceLink/PaymentApplicationLink for those identities. Fee-only EMD-S has a direct monetary link and MUST NOT be forced through a fake OrderService.

### 3.7 Application level

Calculation/application metadata can include:

```text
PerOrder
PerTraveler
PerSegment
PerBound
PerJourney
PerPricingUnit
PerService
PerPiece
PerWeight
PerRoom
PerNight
PerRoomNight
PerGuestNight
PerDirection
PerRoundTrip
PerDocument
ProviderDefined
```

This describes calculation/application semantics, not monetary allocation.

---

## 4. PricingAllocationSet

### 4.1 Scope and ownership

An AllocationSet belongs to EXACTLY ONE PricingLine, one Purpose and one version. This replaces the prior ambiguous whole-Order Completeness flag. It is an owned immutable value-attribution record, not an aggregate or a payment allocation.

```text
PricingAllocationSet
  AllocationSetId, OrderIdAtCreation, PricingLineId
  Purpose : CommercialValue | Servicing | Accounting | Settlement | Reporting
  Version, SupersedesAllocationSetId?, Source, Method, Completeness
  PricingContextRef?, PolicyVersion?, CreatedAt

PricingAllocation
  AllocationId, AllocationSetId
  OrderItemIdAtAllocation?, OrderServiceId?, TravelerId?
  JourneyIdAtAllocation?, SegmentIdAtAllocation?, CoveragePortionRef?
  OriginalValue : [platform monetary representation]?, SaleValue : [platform monetary representation]
  OriginalAllocationId?
```

RefundBasis is not a default persistent purpose: a refund quote supplies its own calculation/evidence. v1 stores only CommercialValue allocations needed for sale/servicing and an additional purpose when an actual external requirement uses it. Ledger owns accounting allocations and can return their versioned reference; do not build six sets for every price line.

Source = OfferProvider/PricingEngine/Supplier/Manual. Method = SourceProvided/DirectBasis/ExactRule/ProRata/EqualSplit/Weighted/Manual. Derived methods require rule/version/weights and are never labeled SourceProvided.

### 4.2 Completeness and reconciliation

Complete: sum SaleValue of rows equals the single parent line SaleValue exactly; OriginalValue also reconciles when supplied. Partial: non-negative sum is at most the line magnitude, with an explicit residual. Unavailable: zero rows and no invented zero shares. Completeness is checked per line/set/currency, never across unrelated lines.

Allocations inherit parent Direction/Effect; they do not create money and are NEVER added to PricingLines when computing totals. Do not sum versions or purposes together. Active set is highest accepted version for `(PricingLineId, Purpose)`; superseded sets remain immutable. A unique index on `(PricingLineId, Purpose, Version)` plus commit concurrency prevents duplicate current versions.

A one-target `Basis=OrderService` line needs no redundant stored allocation set: the value attribution can be returned as `Method=DirectBasis` from the immutable line scope. Store actual rows when partial coverage, several services or historical redistribution must be represented.

### 4.3 Service and portion granularity

One allocated row can carry Service + Traveler + Segment dimensions. For a shared room or transfer, allocate to the service unless a defensible traveler valuation is required; do not divide by guest count merely to populate a table. Segment is optional for non-air services.

A through-baggage service may cover several flight segments; portion-specific rows or observations track fulfillment where the provider requires them. Partial travel does not automatically consume the entire service or create another bag charge. Historical target IDs remain valid evidence after split, even when current ownership changes.

### 4.4 Refund and partial reversal

Allocation is value attribution, NOT refund entitlement. A refund quote may reprice the used portion, retain penalties or credit taxes independently of the old allocation. Preserve the quote's approved credit amounts and the referenced money lines. Cumulative credit/reversal caps and tender refund caps are separate checks; goodwill above the original value is a separate authorized adjustment.

---

## 5. Price totals and payment visibility

Let `C = sum signed SaleValue for Effect=CustomerBalance` over committed lines. `C` includes balanced SplitTransfer lines for each order's current commercial attribution. Let `P` be NET confirmed externally applied funding (applications minus application reversals/refunds, each MovementId counted exactly once). Neither pending intentions nor an unsecured credit limit is P.

```text
CustomerTotal = C
AppliedNet = P
BalanceDue = max(C - P, 0)
CustomerCreditBalance = max(P - C, 0)
```

A CustomerCreditBalance is an amount requiring disposition, NOT permission to trigger a refund automatically. An approved RefundPlan with original tender/application references controls execution. Active refund reservations reduce available refundable funds until resolved. RefundCompleted updates funding ONCE; it does not append the same commercial credit again.

Component totals (FareTotal/TaxTotal/AncillaryTotal/PenaltyTotal) use the SAME customer-effect and sign filter. Settlement totals are grouped by party/category/currency; a naked SettlementOnlyTotal is not a universal supplier-payable balance.

Credit sales use a verified `CoverageGuarantee` from the external credit/payment owner. It satisfies issuance eligibility for a stated amount/scope without pretending cash was captured. Payment projections expose CashApplied, GuaranteedCoverage, AvailableCoverage and PendingRefund separately. Never double-count a guarantee and the later payment that settles it.

Order.CustomerTotal and read-side totals are cached, transactionally maintained and rebuildable from immutable line rows. They are not a computed monetary property that walks the whole history on every access. An Order may be fully financially covered while one document is still unknown; these facts are shown independently.

---

## 6. Air fare construction scenarios

### 6.1 Direct one-way

```text
IKA -> IST
P1
```

```text
OrderItem OI1
  AirService S1 (P1, SEG1)
  FareConstruction (Order-owned, linked to OI1)
    PricingGroup PG1 (P1, ADT)
      PricingUnit PU1 OneWay
        FareComponent FC1 (SEG1)
```

Typical lines:

```text
Fare 180 -> Basis FC1
Tax TAX-A 10 (synthetic code) -> Basis SEG1 or FC1 according to source
YQ 20 -> Basis FC1/PU1 according to source
```

### 6.2 True round trip

```text
IKA -> IST -> IKA
```

```text
PU1 RoundTrip
  FC1 outbound
  FC2 inbound
```

The accepted price may contain:

- one fare line at PU level;
- separate fare lines at FC level;
- source-provided value allocations to individual services.

If only the PU total exists, do not fabricate FC amounts.

### 6.3 Round trip from one-ways

```text
PU1 OneWay -> outbound
PU2 OneWay -> inbound
```

A change to inbound normally reprices PU2 only unless Pricing says otherwise.

This semantic difference is why itinerary shape must not determine fare coupling.

### 6.4 Local combination

Two one-way fare components may be combined into a larger pricing unit.

Model the structure returned by Pricing:

```text
PU1 RoundTrip
  CombinationMethod = LocalCombination
  FC1 OW fare
  FC2 OW fare
```

Do not assume `RoundTripFromOneWays` means exactly two independent PricingUnits.

### 6.5 Through fare over connection

```text
IKA -> DOH -> BKK
```

Two air Services may map to one FareComponent:

```text
FC1 SegmentRefs = [SEG1, SEG2]
```

This is valid and expected.

### 6.6 Fare break

The same itinerary may instead contain:

```text
FC1 IKA-DOH
FC2 DOH-BKK
```

Either one or multiple PricingUnits depending on source.

### 6.7 Open jaw

```text
IKA -> FRA
MUC -> IKA
```

Use `PricingUnit.Type = OpenJaw` when the Pricing source says so. Ground/surface sectors may exist in the journey representation but are not forced into an air Service.

### 6.8 Circle trip / multi-city

Represent multiple FareComponents/PricingUnits exactly as returned. Do not introduce custom OrderItem segmentation to imitate fare construction.

### 6.9 ADT / CHD / INF

Use one traveler per PricingGroup by default. Group multiple travelers only with an explicit homogeneous source construction and clearly extended line amounts; passenger type alone does not prove identical price or eligibility.

```text
PG1: Traveler P1, ADT
PG2: Traveler P2, CHD
PG3: Traveler P3, INF
```

PricingLines allocate to the relevant travelers/services as supplied.

### 6.10 Dynamic/opaque air price

If dynamic pricing returns only an accepted product and price with no traditional fare construction:

```text
AirFareConstruction = null
```

PricingLine still provides a complete monetary record.

This is a supported first-class case, not a data error.

### 6.11 Charter / contract price

A charter Order may contain Air Services but no ATPCO fare construction.

```text
PricingLine
  ComponentType = Fare or ProductCharge
  BasisType = OrderItem
  SourceRef = CharterContractPriceQuote
```

---

## 7. Tax, charge and fee scenarios

### 7.1 Segment tax

```text
Tax XX 10
Basis = Segment SEG1
Allocation = P1/S1/SEG1
```

### 7.2 Journey or pricing-unit tax

If the source returns a journey/PU-level tax, preserve that level. Do not split per segment merely for convenience.

### 7.3 Carrier surcharge

```text
ComponentType = CarrierSurcharge
Code = YQ or YR
```

Refundability and application basis remain independent from fare rules.

### 7.4 Booking/order fee

```text
ComponentType = Fee
Basis = Order
ApplicationLevel = PerOrder
```

### 7.5 Payment/card fee

The fee can be priced at Order level while the actual Payment remains external.

Do not use `PaymentId` as PricingBasis.

### 7.6 Discount

```text
Fare 400 Debit
Baggage 50 Debit
Discount 45 Credit
```

If the discount is allocated across products, store the source allocation. If not, leave the split unavailable rather than inventing it.

### 7.7 Markup

Markup is its own line. Whether it affects customer total or settlement depends on the selling arrangement.

### 7.8 Agency commission

Commission is normally `SettlementOnly` unless the commercial contract explicitly changes the customer payable amount.

Do not subtract commission from customer total by default.

### 7.9 Penalty

Change/refund/cancellation/no-show and other servicing penalties remain distinct from fare/tax/reversal components when the authoritative source distinguishes them. Preserve the applied result, affected scope, source/rule reference and waiver/collection treatment supplied by Pricing/provider. Ordering does not calculate a penalty from raw rules or infer it from a NoShow/cancellation status. See `12` section 9.

### 7.10 Cross-scope application contract

`ApplicationLevel` may be PerDirection/PerJourney/PerDocument/PerOrder without changing the line's pricing basis or multiplying the amount by every referenced service. A per-document fee is one accepted charge for the document grouping; it is not cloned per coupon. A per-bound charge covering two segments remains one monetary line with explicit coverage/allocation metadata. Multi-jurisdiction taxes remain separate tax occurrences/lines with their own jurisdiction, source occurrence key and refund treatment.

---

## 8. Ancillary scenarios

### 8.1 Baggage per piece on one segment

```text
OrderItem BAG1
  BaggageService BS1
    Traveler P1
    Coverage SEG1
    PieceCount 1

ProductCharge 30
Basis = OrderService BS1
```

### 8.2 Baggage per weight

```text
BaggageService BS1
Weight = 10 KG
PricingLine
  ApplicationLevel = PerWeight
  Quantity = 10
  UnitAmount = 4
  Total = 40
```

### 8.3 Baggage across a connecting direction

The baggage product can cover multiple relevant segments through ServiceCoverage while retaining one commercial service identity if that is how the product is sold/delivered.

### 8.4 Round-trip baggage bundle

```text
OrderItem BAG-BUNDLE
  BS1 outbound
  BS2 inbound
PricingLine 50 Basis OrderItem
```

If no source allocation exists between outbound/inbound, do not pretend each is worth 25. A derived allocation is allowed only in a declared allocation set with method metadata.

### 8.5 Seat per segment

A paid seat on one flight is normally one Service and may be one OrderItem if independently priced.

### 8.6 Seat bundle

One priced bundle may contain multiple Seat Services across multiple segments.

### 8.7 Paid meal

Meal Service may have product charge and tax lines.

### 8.8 Included meal

A meal included in a fare/bundle does not require a separate zero-price line.

`not separately priced != price zero`.

### 8.9 Lounge

Lounge can be flight-related, journey-related or standalone. Model Service type separately from Coverage/application context.

### 8.10 WiFi

Support per-flight, per-journey or time-based coverage and pricing.

### 8.11 Hotel

Hotel pricing can be:

- per room;
- per night;
- per guest;
- package total;
- taxes/fees separately;
- supplier/retailer markup separately.

No flight Segment is required.

### 8.12 Ground transport

Price per vehicle, passenger, leg or transfer package. Coverage is location/time based.

### 8.13 Insurance

Insurance can be traveler/trip/order scoped and fulfilled by a third party. Use typed or GenericServiceDetails according to product complexity.

### 8.14 Bundle with mixed product types

```text
OrderItem FLEX-BUNDLE
  AirService
  BaggageService
  SeatService
  PriorityService
```

A single bundle price may be allocated internally only if source or an explicit valuation policy supplies the split.

### 8.15 Priority, fast-track, CIP and assistance

These use the same general pricing ledger. The Service may be separately priced, included or complimentary and may be airport/time/segment scoped. Wheelchair/special-assistance and similar zero-separate-price products still create a Service when supplier execution/delivery tracking matters; do not manufacture a zero PricingLine merely to make them visible. Sensitive service details are protected/minimized as specified in `01`.

### 8.16 PETC, AVIH, special baggage and extra-seat/CBBG

PETC/AVIH/special baggage retain explicit quantity/weight/unit/product references and provider acceptance. Extra-seat/cabin-baggage-seat is an ancillary capacity product associated with the traveler and Air Service; it can require one additional seat reservation without creating a fake Traveler or another AirTransportService. Pricing remains ProductCharge/Tax/Fee/etc. lines, not a special money model.

### 8.17 SIM/eSIM and other third-party digital products

No flight segment is required unless the sold product says so. Validity/data allowance/delivery code are registered service-detail fields; activation/consumption is external delivery evidence. Supplier settlement does not rewrite customer price.

### 8.18 Paid upgrade, involuntary upgrade and downgrade

A paid cabin upgrade is an explicit accepted commercial change with new price/tax/document treatment and preserved old fare/service lineage. An involuntary operational upgrade does not create a customer charge by itself. A downgrade may create disruption compensation/refund only through an accepted Pricing/policy result; delivery evidence never calculates compensation.

### 8.19 Shared and pooled ancillary value

Guest count, shared vehicle/room, pooled baggage and multi-unit products must preserve the source quantity/beneficiary semantics. Never multiply a package/vehicle/room/pool price by passenger count unless the source price is explicitly per passenger. Partial consumption changes delivery quantity, not historical sold quantity or PricingLine amount.

---

## 9. Servicing pricing rules

The complete benchmarked flow semantics are in `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md`; this section states the pricing invariants used by those flows.

### 9.1 Principle

Servicing changes current commercial obligations and appends accepted monetary history. Prior sale/document/payment history is never rewritten to make the new state look simple.

A servicing request normally uses this separation:

```text
current Order + historical pricing/document context
-> side-effect-free reshop/refund/change decision by authoritative owner
-> accepted quote/change plan
-> durable Ordering operation
-> reservation/document/value execution
-> commercial + immutable price finalization
-> immutable servicing redisplay/audit record
```

Ordering does not implement ATPCO Category 16/31/33, tax, fare, FX or supplier-refund engines. It supplies the historical/current context those owners require and records their accepted result.

### 9.2 Add ancillary

An ancillary may be added from an accepted Offer/price decision or another explicitly authorized source path. Create the sold item/service obligations and persist the authoritative accepted price result once. Reserve/fulfil/document only when that product requires it.

### 9.3 Pre-ticket cancel / withdrawal

If no accountable document exists, cancellation is a commercial/reservation operation, not a fabricated ticket refund. Obtain the approved cancellation/value treatment, release capacity/unused payment coverage as required, and record captured-money refund separately when Payment confirms it.

### 9.4 Void

Void is a provider/issuer-qualified accountable-document operation. It is distinct from normal refund. The provider determines whether the exact document/coupons are within the permitted void conditions. Ordering records confirmed document outcome and accepted commercial reversal treatment; Payment separately confirms release/refund of actual money.

### 9.5 Cancel one service / voluntary refund

1. identify service/item/dependency/document scope;
2. obtain a trusted cancellation/refund decision for that exact scope;
3. persist one durable operation/claim;
4. secure required reservation/document/control actions under stable keys;
5. commit commercial cancellation and accepted PriceChangeSet once;
6. request the approved refund/value disposition exactly once through its owner;
7. keep Pending/Unknown payment result visible without repeating commercial cancellation.

Service allocation amount is never assumed to equal refundable amount.

### 9.6 Change one segment / voluntary change

The accepted decision must identify affected service scope, pricing context, replacement obligations, dependent ancillaries, document action and monetary outcome. Depending on the authoritative result, an unchanged/delivered service can retain identity even when its containing PricingUnit is repriced.

Possible monetary outcomes include even exchange, add-collect, refund, residual/reusable value, combinations of add-collect with credit/residual, and penalties that are netted or separately payable. Preserve the source-approved components instead of collapsing everything into one unlabeled delta.

### 9.7 True RT vs OW+OW

A change to one leg of a true round-trip PricingUnit can require repricing the whole pricing unit. An independently priced OW+OW construction can often isolate the changed one-way. Ordering preserves the source fare construction; Pricing decides the actual rule/result.

### 9.8 No-show / failure to use confirmed space

A DCS `NoShow` observation is not itself a financial rule. It may later lead to explicit cancellation, forfeiture, no-show penalty, reusable value or no commercial action, depending on the authoritative servicing decision. Remaining services are not cancelled implicitly.

### 9.9 Refund

Refund is never computed from current allocation totals alone. For partially used travel the authoritative refund engine may reprice/value flown portions and independently determine refundable fare, tax, surcharge, ancillary value, penalty and residual. Ordering records the accepted result and Payment/value owner executes it.

### 9.10 Exchange/reissue

Commercial change and document exchange are correlated but separate. Preserve old/new services, old/new accountable documents/coupons, accepted pricing decision, penalty/fare/tax difference, value-movement refs and voluntary/involuntary authority. Partially used travel keeps consumed services/document history rather than cloning them into the replacement sale.

### 9.11 Revalidation

Revalidation keeps the same accountable ticket and changes eligible coupon/flight binding. Eligibility is returned by the issuer/provider/rule capability for the exact case; Ordering does not copy one vendor's fixed revalidation rules into the domain. If revalidation is not confirmed, use the explicitly accepted alternate plan or remain unresolved.

### 9.12 Involuntary change

Preserve disruption/recovery authority and reason. Pricing/recovery policy returns the approved monetary/waiver treatment. Do not infer that every involuntary action has zero fare difference or zero penalty merely from the word “involuntary”.

### 9.13 Penalties and waivers

A servicing penalty is a distinct applied commercial outcome, not a reversal. Industry sources show variations in trigger, timing, assessment scope, amount/percentage logic, netted vs separately collected treatment, waiver authority and refund/reuse behavior. Ordering persists the source-applied result/provenance; it does not implement a generic penalty rules engine.

### 9.14 Residual / reusable value

Reusable value may be known to exist while the exact amount is not known until a new reshop/refund. Therefore do not persist a guessed guaranteed residual amount. When the authoritative owner creates an exact residual/voucher/wallet value, retain its reference and confirmed amount/disposition through that owner.

### 9.15 Final servicing record

After finalization, the operation must remain redisplayable without repricing today's Order. The immutable ServicingRecord capability described in `01`/`12` references the accepted pricing and confirmed document/payment outcomes; it is not another source of monetary truth.

---


## 10. Split pricing

### 10.1 Easy case

If each passenger/service has source-provided allocations, moving P1 to child Order can transfer the allocated value explicitly.

### 10.2 Hard case

If a true RT/bundle line has no defensible allocation across split passengers/services, Ordering must not invent a monetary partition.

Use one of:

1. Pricing returns a split valuation;
2. split is performed only after pricing creates successor items/lines;
3. a configured business allocation method generates a clearly marked derived AllocationSet.

### 10.3 Price lineage

Recommended accounting-safe pattern:

Source Order:

```text
SplitTransfer opposite-direction Transfer lines for the moved signed value
```

Child Order:

```text
SplitTransfer same-as-source-original direction Transfer lines for moved value
```

This gives both Orders a coherent local balance/history. Transfer is a RECLASSIFICATION, not tax refund/new taxation or a new payment. Preserve TransferGroupId, source/child OrderIds, component, original currency and sale currency. Across both orders each pair sums to zero. Discount transfers use the opposite directions to fare transfers. Ledger must apply the pair atomically/idempotently under TransferGroupId, or stage both legs until complete.

Do not physically move old immutable PricingLines between Orders.

---

## 11. Pricing invariants

- INV-P01: PriceChangeSet is immutable after commit.
- INV-P02: PricingLine is immutable after commit.
- INV-P03: a reversal requires explicit original-line lineage, opposite economic polarity, compatible component/effect/currency context, preserved historical conversion provenance when applicable, and an outstanding-value cap. A lineage link alone is not a reversal.
- INV-P04: customer-effective monetary lines must carry the accepted sale/customer-value representation required to compute the customer commercial total without cross-currency guessing.
- INV-P05: every Complete AllocationSet reconciles to its ONE parent PricingLine in each supplied currency; partial and unavailable have explicit semantics.
- INV-P06: Allocation source/method is mandatory when allocations are derived.
- INV-P07: PricingAllocation is not a refund entitlement.
- INV-P08: no component category may silently change semantics during reversal.
- INV-P09: Fare, Tax, CarrierSurcharge, Fee, Penalty, Discount, Markup and Commission remain distinguishable.
- INV-P10: an amount classified only for settlement/accounting does not alter CustomerTotal; customer-collected tax cannot be moved out of customer payable by labeling it settlement-only.
- INV-P11: one Service does not imply one FareComponent.
- INV-P12: one FareComponent does not imply one Segment.
- INV-P13: PricingUnit structure is never inferred from round-trip itinerary shape.
- INV-P14: missing fare construction is valid for dynamic/charter/provider-defined pricing.
- INV-P15: included product is not automatically represented as a zero-price line.


---

## 12. Fully worked acceptance fixtures (synthetic, not real tariff/tax advice)

### F-P01: Same itinerary, two different fare constructions

P1 flies A-B and B-A. True RT quote has PU-RT with FC-OUT and FC-IN: Fare Debit 400, Tax Debit 40, CustomerTotal 440. A permitted whole-RT replacement quote says Fare 460, Tax 50, Penalty 25. Store old Fare reversal Credit 400; old Tax reversal Credit 40; new Fare Debit 460; new Tax Debit 50; new Penalty Debit 25. Delta +95, resulting C=535. Other travelers' lines do not change. For an already flown outbound, use the servicing engine's explicit used-portion treatment rather than mechanically reusing this unused-ticket example.

Independent OW quote: Fare-OUT 180, Fare-IN 210, Tax-OUT 20, Tax-IN 20 => C=430. Inbound change quote: Fare-IN 260, Tax-IN 25, Penalty20. Credit 210+20, Debit260+25+20 => delta75, C=505. PU-OUT and its lines remain unchanged. A provider-defined linked ticket/fare rule can widen the quote scope; Order never assumes independence merely from two OW labels.

### F-P02: Bundle partial refund is not the allocation

Bag+seat+priority bundle charged 80; CommercialValue shares 35/30/15. Supplier fails the seat. Accepted refund decision credits 20, not automatically 30. New ProductCharge/Reversal Credit20 references the relevant original price, with its approved monetary target scope; C=60. Actual tender refund20 reduces P from80 to60. Both final balances zero; the original allocation remains historical valuation, not an entitlement rule.

### F-P03: Fully paid round trip, partial service refund

Two passengers: Fare800 + Tax80 = C880, P880. Engine approves a P1 refund: fare credit100 + tax credit20 - penalty charge30. C becomes790, pending refund90. Before refund completion P880 and CustomerCreditBalance90; after one confirmed refund90 P790 and balance0. A duplicated refund fact changes neither total nor funds. No service/coupon is marked Refunded solely because a PricingLine was appended.

### F-P04: Tax inclusion and percentage

Hotel: three nights at100 -> ProductCharge300. Source VAT30 + city tax15 + service fee10 -> C355. Two guests do not double the room charge. If city tax is payable at property, its Effect is Informational for the Order's payable, with CollectionParty=Supplier, and C340. A tax-inclusive quoted room110 with tax10 is net100+tax10=110, not120. A source-defined 10% fee on eligible base200+30 is23; the persisted base list excludes a non-eligible tax20. Order checks the returned result and base references; it does not own local tax law.

### F-P05: Commission, original FX and exact reversal

Customer Fare100 EUR + Tax10 EUR = C110. Commission7.50 EUR SettlementOnly does not reduce C. Original USD100 converted at0.9 EUR/USD gives SaleValue90 EUR. A full reversal remains USD100 / EUR90 even if today's rate is0.95. Any permitted FX difference is a separate authorized adjustment with a different money fact; the reversal never silently changes to95.

### F-P06: Equal split, negative direction, version choice

Allocate a EUR100 Credit line into equal shares: magnitudes33.34/33.33/33.33; signed sum=-100. Creating an alternate Reporting set of100 does not change customer total or double value. Superseding CommercialValue version1 by version2 uses only version2 in current valuation; both remain auditable.

### F-P07: Split with mixed effects

Source C220 contains Fare200 + Tax40 - Discount20; source P220. Move half with source-supported valuation. Source transfer: Fare Credit100, Tax Credit20, Discount Debit10 -> C110. Child: Fare Debit100, Tax Debit20, Discount Credit10 -> C110. Combined C remains220; balanced application transfer moves110 of P to child so each P110. Ledger classifies the event as ownership transfer of existing components; no new sale/revenue/tax/refund. Settlement commission transfers use separately party-scoped pairs if needed.

### F-P08: Charged quantity versus service delivery unit

Excess baggage10kg at4/kg =40 once, even across two flight legs in one priced through-portion. A round-trip bag package50 with two directional services is still50. A three-night hotel can have lines100/120/80 =300 without creating three separate hotel reservations. A shared transfer vehicle90 for three travelers is90, not270. A consumed quantity2 of a purchased5-unit pass does not itself create another price line; consumption measure carries2 with Unit=Entry.

### F-P09: Partial reversal and cancellation penalty

Fare100 Debit. First approved reversal Credit30; remaining reversible70. A second Credit80 is rejected before commit. A Cancellation PriceChangeSet adds Penalty Debit10; it is LineRole=Original and must not be discarded as a reversal. The remaining Fare reversal70 plus Penalty retained10 yields net10.

### F-P10: Dynamic/charter and per-document fees

A charter name materialization can record a contract-attributed fare150 without a fictional ATPCO PU/FC. Whether an additional payment is needed is determined by verified contract coverage, not by the presence of a new ticket. A10-per-document fee for two planned documents yields20 once. If only one document succeeds, the second10 remains subject to the accepted failure/refund rule; retrying that document does not charge another10.

## 13. Hard validation boundaries

Reject missing currency, inconsistent FX directions, unknown monetary component semantics, ambiguous per-passenger vs extended totals, duplicate source-line occurrences, incomplete mandatory tax/issuance breakdown, stale quote versions, over-reversal and unsupported manual adjustment authority. Missing optional allocation or legacy fare construction is not itself a failure, but operations that require a valuation must receive one before execution.

A change to one passenger may reprice a wider PU but cannot silently alter another passenger's commercial promise. Quote validation compares ExplicitTargetServiceIds, PricingScopeServiceIds and NonTargetChanges separately; unexpected non-target changes require explicit acceptance. This guards the central round-trip scenario without pretending every Service is independently priceable.
