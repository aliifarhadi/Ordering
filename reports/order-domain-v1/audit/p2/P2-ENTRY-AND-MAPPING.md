# P2 — Entry Baseline and Concept Mapping

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
**Scope of this document:** the P2 entry gate only — verified baseline, the treatment decision for every existing
concept P2 touches, and the boundaries P2 will not cross. No implementation decisions are recorded here as done.

Design sources read in full before writing this: `02-PRICING-AND-SERVICING.md` (all sections), `01-DOMAIN-MODEL.md`
§4, §5, §6/6.4/6.4.1/6.5, §7, §8, §12, §13, §14, §17–§23, `08-CONTRACTS-AND-DATA-DICTIONARY.md` §4–§9,
`10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md` §1/§5/§6, `11-SLICE-READING-MAP.md` (P2).

---

## 1. Verified entry baseline

| Check | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors, warnings only (pre-existing xUnit analyzer + duplicate-using) |
| `AeroTech.Ordering.Domain.Tests` | **69 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **128 passed**, 0 failed (real SQL Server `DotAirOrderNewP0Tests`) |
| **Total** | **197 passed, 0 failed** — matches the expected P2 entry baseline exactly |

Migrations present on the local dev database: `P0OperationsAndRatePrecision`, `P0CommercialVersion` (query),
`P05OperatorSettingsProjection` (reference), `P06ClaimConcurrencyToken`, `P1OrderVerticalSlice`,
`P1OrderDetailsProjection` (query), `P11IssuerCarrierIdentity`.

P0 / P0.5 / P0.6 foundation is frozen and is **not** reopened by P2. P1 / P1.1 / P1.2 behaviour is preserved;
every one of the 197 tests must stay green at P2 exit.

---

## 2. Treatment legend

| Treatment | Meaning |
|---|---|
| **KEEP** | Correct as-is for the v1 target model. Not touched beyond mechanical wiring. |
| **REFINE** | Concept survives; fields/semantics change so it matches the design. Same type, same table lineage. |
| **REPLACE-IN-PLACE** | Same name and same table lineage, but the semantics are rewritten. **No `V2` type is created.** |
| **REMOVE-FROM-ACTIVE-V1** | Concept stops being authoritative in the active target path. It is deleted or demoted to a derived cache. |
| **LEGACY-OFF-PATH** | Pre-existing code not on the target path. Untouched in P2, scheduled for removal in P3. |

---

## 3. Required mapping

### 3.1 Pricing

| Concept | Treatment | What changes and why |
|---|---|---|
| `OrderPricingLine` | **REPLACE-IN-PLACE** | Survives as the single immutable monetary ledger line, but its semantics are rewritten: it becomes a child of a `PriceChangeSet` (not of `Order` directly), gains `ComponentType`, `Effect`, `LineRole`, application metadata (`ApplicationLevel`, `Quantity`, `UnitAmount`), pricing basis, source-line idempotency key, tax/calculation provenance, and reversal lineage with outstanding-value capping. **Polarity is inverted**: today `Direction=Credit` means *sale*, which contradicts the design's `Debit=+1 / Credit=-1` contract. This is the single most dangerous existing semantic and is why P2-A runs before anything else. No `PricingLineV2`. |
| `OrderPricingLineDirection` | **REFINE** (enum, `Contracts/AeroTech.Messages`) | Members `Debit`/`Credit` are correct; the *meaning* attached to them changes at every call site. The enum file itself needs no new member. |
| `OrderPricingLineCategory` | **REFINE** | Maps onto the design's ComponentType set. `Fare`, `Tax`, `Fee`, `Penalty`, `Discount`, `Commission` already exist; `CarrierImposedSurcharge` is the design's `CarrierSurcharge`. Missing and to be added: `ProductCharge`, `Markup`, `Adjustment`, `Other`. `Ancillary` and `Charge` are ambiguous today and are resolved to `ProductCharge`. `Rounding=100` is retained as source-supplied rounding evidence, never as an Ordering-invented residual. |
| `OrderPricingLineSubCategory` | **REMOVE-FROM-ACTIVE-V1** | Five members (`BaseFare`, `Tax`, `VAT`, `ServiceFee`, `Promo`) that duplicate `LineCategory` and carry no design meaning. The distinctions the design actually requires (tax code, jurisdiction, occurrence key) belong in tax provenance, not a second category enum. Enum file remains in Contracts for legacy rows; the target path stops writing it. |
| `OrderPricingLineScope` | **REFINE** → pricing basis | Becomes the explicit `PricingBasis` discriminator (Order / OrderItem / OrderService / Segment / Journey / PricingUnit / FareComponent / Traveler). Binding constraint from `02` §3.6: **`PaymentId`, `TicketId` and `CouponId` are never a pricing basis.** |
| `OrderPricingReason` | **REFINE** → moves up to `PriceChangeSet` | Reason is a property of the accepted commercial operation, not of each line. It moves to the change set and gains the design's full set: `OriginalSale`, `AddProduct`, `Reprice`, `VoluntaryChange`, `Exchange`, `InvoluntaryChange`, `Cancellation`, `Refund`, `Void`, `ManualAdjustment`, `Correction`, `SplitTransfer`. P2 commits only `OriginalSale` and `AddProduct`; the rest are declared but unreachable until P3. |
| `Effect` (CustomerBalance / SettlementOnly / Informational) | **NEW** | Does not exist today. Required by INV-P10 and the `02` §3.4 permission matrix. `Tax + SettlementOnly` is rejected outright. |
| `PriceChangeSet` | **NEW** | No equivalent exists. One immutable accepted commercial monetary operation, carrying `FinancialSequence`, reason, source decision/quote reference and commit time. Pricing lines cannot be appended outside one. |
| `OrderPricingLineAllocation` | **REPLACE-IN-PLACE** | Today it is a flat child of a line with an `OrderPricingLineAllocationTargetType` enum containing exactly one member (`OrderAirTransportService`) — unusable for the design. It becomes `PricingAllocation` under a new `PricingAllocationSet` (one parent line, one Purpose, one Version, explicit `Completeness = Complete/Partial/Unavailable`, mandatory source/method when derived). Allocations are never added to totals and are never a refund entitlement (INV-P07). |
| `OrderPricingLineAllocationTargetType` | **REFINE** | Single-member enum expands to the scope kinds an allocation can attribute to (Item / Service / Traveler / Segment / FareComponent). |
| `OrderAmount` | **REMOVE-FROM-ACTIVE-V1** (demoted to cache) | Its seven totals are currently computed with the inverted polarity in `Order.RecalculateTotal()`. Under the design, `CustomerTotal = Σ signedSale where Effect=CustomerBalance` is **derived**; a persisted total is a materialized cache recomputed in the same transaction (`01` §17, §20). It is kept as a stored projection of the ledger, never as an input to any decision. |
| `Commission` (value object) | **REMOVE-FROM-ACTIVE-V1** | It *calculates* `totalAmount * rate / 100` locally and is folded into the order total. That is Ordering owning a monetary calculation it must not own. Commission becomes a source-owned `PricingLine` with `ComponentType=Commission` and, by default, `Effect=SettlementOnly` — so it does **not** reduce `CustomerTotal` (`02` §7.8, INV-P09). |
| `ExchangeRate` (value object) | **KEEP** | Already the P0-ratified conversion-provenance carrier at `decimal(28,12)`, AirPrice-aligned. P2 reuses it verbatim. **No new `Money`, `CurrencyCode`, ROE engine or rounding library is created** (`01` §19, `13`). |
| `RefundabilityRule` | **KEEP** on the line | Source-supplied refundability treatment, preserved as accepted evidence. It is never used by Ordering to compute a refund amount. |
| `PricingLineSnapshot` (DTO) | **REFINE** | Event/projection DTO; regains the new line shape. `Order.Pricing.cs::NetOf` encodes the inverted polarity and is rewritten. |

### 3.2 Commercial structure

| Concept | Treatment | What changes and why |
|---|---|---|
| `OrderItem` | **REFINE** | Structure is right (individually priced commercial item containing services). Gains `SourceOfferId/SourceOfferItemId/SourceOwnerCode`, `ProductSnapshot`, `CommercialTermsSnapshot`, `CreatedByChangeId/ReplacedByChangeId/CancelledByChangeId`. Removals required by `01` §5.2: `PaymentStatus`, `FulfillmentStatus`, `FinancialStatus` are **not** commercial states and move to projections. `CommercialStatus` gains `PartiallyChanged`, `Replaced`, `Expired`, `Partitioned`. |
| `OrderItemPolicySnapshot` | **REFINE** → splits in two | Today it mixes commercial terms with fulfillment configuration. It splits into `CommercialTermsSnapshot` (item-level: refund/change/no-show policy summaries, policy source + version, `OriginalSalePricingDate`, rule references) and `FulfillmentProfileSnapshot` (service-level: `RequiresReservation`, `RequiresDocument`, `DocumentKind`, `RequiresPaymentCoverage`, supplier/delivery refs). The current single snapshot is hard-coded per call site in `Order.Create.cs` (`AirTransportPolicy`, `OrderChargePolicy`); that hard-coding is removed. |
| `OrderService` (abstract base) | **REPLACE-IN-PLACE** (inheritance → composition) | The type survives and remains the smallest servicing unit, but stops being an abstract EF base class. Typed details become one-to-one detail rows keyed by `ServiceType` (AirTransport, Seat, Baggage, Meal, Lounge, Hotel, GroundTransport) plus `GenericServiceDetails` with a **registered** `SchemaName`/`SchemaVersion` that fails closed on an unknown schema. It gains `BeneficiaryTravelerIds`, `ServiceCoverage`, `PriceTreatment`, `ServiceVersion`, `FulfillmentProfileSnapshot` and change lineage. Removals per `01` §6.2: `FinancialStatus`, `DeliveryStatus` and payment status are not canonical on the service. |
| `OrderAirTransportService` | **REFINE** → `AirTransportServiceDetails` | Same data, no longer a subclass. Explicitly **no EF TPT**. `FareBasis` stops being authoritative here and moves to `FareComponent` when a construction exists (`01` §6.4). |
| `OrderServiceType` | **REFINE** | Product members (`AirTransportation` … `InsurancePolicy`) are kept. `Penalty`, `ServiceFee`, `Credit`, `Voucher`, `TaxAdjustment`, `ManualAdjustment`, `Notification` are **financial pseudo-services** and are removed from the active path — a fee is a `PricingLine`, never a Service (`01` §13: "an EMD-S for a change fee … must NOT require a fake seat/flight service"). Missing product members to add: `Priority`, `WiFi`, `ExtraSeat`/`Cbbg`, `SpecialAssistance`, `Umnr`, `Petc`, `Avih` — these use registered generic detail schemas, **not** new `OrderXService` classes (`10` §5). |
| `ProductType` | **REFINE** | Same treatment as `OrderServiceType`: the financial pseudo-products (`Penalty`, `ServiceFee`, `Credit`, `Voucher`, `TaxAdjustment`, `ManualAdjustment`) leave the active path. `Order.Create.cs::BuildOrderCharges` currently manufactures a `ProductType.ServiceFee` **OrderItem per order-level charge** — a fabricated product for a fee. That is removed; an order-level fee becomes a `PricingLine` with `Basis=Order`, `ApplicationLevel=PerOrder`. |
| `OrderItemServiceLink` | **NEW** | Immutable commercial membership evidence (`01` §6.5). Does not exist; item↔service membership is currently only the mutable `OrderService.OrderItemId`. Required so an old item's sold contents are never recomputed from a service's current owner. |
| `OrderChange` | **NEW** (minimal) | Every accepted commercial mutation gets a stable `ChangeId`. P2 implements only `Create` and `AddProduct`; the remaining change types are declared and unreachable. |
| `AirFareConstruction` / `PricingGroup` / `PricingUnit` / `FareComponent` | **NEW** | No equivalent exists. `OrderSegment` currently carries a flat `AirFareId` and `OrderAirTransportService` a flat fare basis — that cannot express true-RT versus OW+OW, which changes exchange/refund/no-show behaviour (`01` §7.1). Construction is **optional** (INV-F05) and is **never inferred from itinerary shape** (INV-P13, INV-F01). |
| `OrderSegment` / `OrderSegmentLeg` / `OrderItinerary` | **KEEP** | Already sold-segment-with-legs shaped, matching `10` §1's one correction (a segment binds one or more physical legs). Untouched by P2. |
| `OrderTraveller` | **KEEP** | Untouched by P2. |

### 3.3 Source normalization

| Concept | Treatment | What changes and why |
|---|---|---|
| `OfferDetail` | **REFINE** → moves out of Domain decision logic | Remains the wire shape of an accepted AirPrice offer, but stops being a parameter of `Order.Create`. It becomes the input of an Application/Provider adapter that produces the normalized accepted-quote shape. |
| `OfferReader` | **REPLACE-IN-PLACE** → normalizer in Application/Providers | Today `Order.Create` constructs an `OfferReader` and the aggregate walks bounds, flights, charge lines and rate periods itself — the domain is parsing a provider payload and deciding tax-vs-fee (`isTax = charge?.Kind == AirChargeKind.Tax`) inline. That logic moves to a source normalizer producing `AcceptedQuote` (`08` §4): `ProductSnapshot`, `CommercialTermsSnapshot`, service definitions, price lines with declared `ComponentType`/`Effect`/`Direction`, optional fare constructions and allocation sets. The domain then *accepts* an already-classified result. Source-line idempotency (`SourceLineRef`) prevents duplicate occurrences (`02` §13). |
| `Order.Create(args, OfferDetail, …)` | **REFINE** | Signature changes to take the normalized accepted source. Behaviour preserved for the existing single-carrier air path so all 197 tests stay green. |

### 3.4 Documents

| Concept | Treatment | What changes and why |
|---|---|---|
| `ElectronicTicket` + `TicketCoupon` | **KEEP**, extended at the edges | P1-built and correct. P2 only adds what pricing needs: `DocumentPriceLink` must reference the new `PricingLineId`/`AllocationId` with a frozen issue-time attributed value, and never recompute from current totals (`01` §12). |
| `DocumentPriceLink` | **REFINE** | Already exists; repointed at the new pricing identities. |
| `DocumentStock` + `DocumentStockAllocation` | **KEEP**, reused for EMD | The P1 aggregate already models prefix/serial-width/check-digit-profile, `Reserved/Issued/Retired` states and `(OperationId, DocumentRole)` idempotent allocation with a fail-closed unconfigured check-digit profile. EMD issuance reuses it with a distinct `DocumentType` and its own issuer namespace — **no second stock mechanism**. |
| `ElectronicMiscDocument` + `EmdCoupon` | **NEW** | Purposes `Service | Fee | Deposit | ResidualValue`; EMD-A associates at **coupon** level; a fee-only EMD-S must not fabricate a Service. |
| `TrafficDocumentAggregate` (`TrafficDocument`, `TicketDocument`, `EmdDocument`, `EmdCoupon`, `DocumentCoupon`, `DocumentAmounts`) | **LEGACY-OFF-PATH** | The pre-design document aggregate, superseded by `ElectronicTicket` (P1) and `ElectronicMiscDocument` (P2). P2 does **not** extend it and does **not** build EMD inside it. `OrderService.TrafficDocumentId` / `DocumentCouponId` are its remaining hooks and are removed from the active path with it in P3. |

### 3.5 Foundation — explicitly untouched

| Concept | Treatment |
|---|---|
| `CommandReceipt`, `ServicingOperation`, `OperationOrderClaim`, `OperationsWriteBoundary`, `OrderOperationCoordinator` | **KEEP** — frozen P0 foundation. `AddProduct` is a new operation *kind* running through the existing machinery, not a new mechanism. |
| `CommercialVersion`, `EventOrdinal`, `CommercialEventSequence`, `ObligationVersion`, `RowVersion` | **KEEP** — semantics fixed in P0/P1.1 and unchanged. `FinancialSequence` is **NEW** and independent: it advances once per committed `PriceChangeSet` and is never inferred from `CommercialVersion` (`08` §9). |
| `IReservationPort`, `IFundingCoveragePort`, `IDocumentIssuancePort` | **KEEP** — P2 adds an EMD issuance capability alongside them with the same Confirmed/Rejected/Pending/Unknown outcome contract. |
| `OrderStatus` (legacy derived facet), `CommercialSummary` | **KEEP** — `Status` stays a derived legacy facet; `CommercialSummary` stays the canonical commercial lifecycle. |
| `ExceptionFactory` / `ExceptionMessages` | **REFINE** — new P2 reason codes are appended in Ordering's 2xxx block with REST-accurate statuses. No new error mechanism. |

### 3.6 Legacy off-path (untouched by P2, removed in P3)

`Order.Cancel.cs`, `Order.Expire.cs`, `Order.Issue.cs`, `Order.Payment.cs`, `Order.Remarks.cs`, `Order.Split.cs`,
`Order.Termination.cs`, `Order.TimeToLive.cs`, `Order.Reserve.cs`, `PaymentAggregate`, `FulfillmentTaskAggregate`,
`ProviderInteractionAggregate`, `TrafficDocumentAggregate`, `OrderPaymentSummary`, `IssuedServiceLink`,
`ReservedServiceLink`, and the legacy `OrderPaid` / `OrderIssued` / `OrderCancelled` / `OrderSplit` events.

These still compile against `OrderPricingLine`, so the P2-A replacement must keep them compiling — that is the
practical constraint on how far the in-place rewrite can go in one step, and it is the reason P2 rewrites the
line's *semantics and additions* rather than deleting fields the off-path code still reads.

---

## 4. Ordering-owned versus source-owned — the line P2 will not cross

Ordering **accepts and preserves**; it does not calculate. Concretely, P2 will not compute a tax, a fare split, a
penalty, a commission amount, a currency conversion, a rounding residual, a refundable amount or an allocation
split. Every one of those must arrive already decided by its authoritative owner, with provenance, or the
operation fails with a named reason. The existing `Commission` value object and `Order.RecalculateTotal()` are the
two places where today's code violates this, and both are addressed in P2-A.

Derived allocations are the one permitted exception, and only inside a declared `PricingAllocationSet` carrying
its method metadata and marked as derived — never as a silent split (`02` §8.4, INV-P06).

## 5. Explicit non-goals restated

P2 does not start P3; does not implement or redesign AirPrice, FlightFlow, JetPay, StoredValue, Ledger, DCS or
FlightOps; does not reopen enum placement (all enums stay in `Contracts/AeroTech.Messages`); does not create
`Money`, `CurrencyCode`, an ExchangeRate framework, a currency service, a ROE engine or a rounding library; does
not build a second framework; does not create `PricingV2` / `OrderV2` / `ServiceV2` / `DomainV2`; and does not
implement refund, exchange, void, split, DCS, disruption, group booking, a tax engine or a fare-rule engine.

## 6. Implementation order (unchanged from the P2 brief)

P2-A pricing foundation → P2-B accepted source normalization → P2-C AirFareConstruction → P2-D service/item model
→ P2-E initial sale + AddProduct → P2-F EMD → P2-G projections/APIs/events → P2-H verification.

P2-A runs first because the old monetary polarity is still authoritative today, and no ancillary or EMD work may
be built on top of an inverted sign convention.
