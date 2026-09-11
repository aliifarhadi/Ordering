# 12 - Servicing Benchmark and Financial Flows

**Status:** binding business/domain specification for v1.  
**Scope:** post-sale servicing of air and ancillary services: cancellation/withdrawal, void, refund, voluntary change, revalidation, exchange/reissue, involuntary change, no-show/forfeit, ticket/EMD effects, penalties/waivers, residual/reusable value, payment interaction, and customer-facing servicing records/notices.

This document specifies **business semantics and ownership**. It does **not** prescribe C# representation, enum layout, numeric precision, framework abstractions, or provider-specific wire schemas. Implementation mechanics follow the existing AeroTech repository/framework. When a required cross-service representation or rule is not unambiguously available from the existing platform, `13-PLATFORM-OWNERSHIP-AND-DECISION-GATES.md` applies and implementation is blocked pending an explicit decision.

---

## 1. Benchmark coverage and evidence hierarchy

The model was checked against five complementary evidence classes rather than one vendor workflow:

1. **IATA Offers & Orders / AIDM** for Order/OrderItem/Service semantics, servicing, change/cancel restrictions, reusable value, penalty relationships and accountable-document lineage.
2. **ATPCO** for fare-rule based voluntary changes/refunds, penalties, optional-service reissue/refund behavior, form-of-refund and service settlement characteristics.
3. **Amadeus** for mature operational ticketing workflows: refund records, partially used refunds, reissue/revalidation, fare/tax difference, penalties, residual value and document handling.
4. **Travelport** for an independent GDS/API cross-check of void/cancel/refund/exchange distinction, structured penalty conditions, waivers, reissue/revalidation and involuntary exchange.
5. **Sabre** as a second vendor cross-check for Offers & Orders ancillary cancellation/EMD void-refund behavior, exchange/reissue, schedule-change even exchange and DCS/control prerequisites.

Vendor workflows are **evidence of industry behavior**, not instructions to clone a GDS/Altéa data model. Carrier-, market-, issuer-, settlement-plan- and provider-specific rules stay behind the appropriate source contract/profile.

### Benchmark register

| Area | Primary benchmark conclusion | AeroTech consequence |
|---|---|---|
| Servicing definition | IATA treats servicing as Order changes triggered voluntarily by customer or involuntarily by airline | Both voluntary and involuntary flows use explicit servicing operations and stable Order/service lineage |
| Cancel/change restrictions | IATA OrderItem can carry change/cancel restrictions with fee/refund relationships | Accepted terms may preserve source restrictions, but Ordering does not become the rule calculator |
| Reusable value | IATA notes reusable value may exist while the exact amount is unknown until reshop/refund | Never persist an estimated “current refundable amount” as truth |
| Penalty | IATA represents penalties arising from servicing and distinguishes netted vs separately paid treatment | Penalty outcome must remain distinct from refund/add-collect; collection treatment comes from source |
| Voluntary change | ATPCO Cat31 automates voluntary reissue/change using original ticket/journey context | Pricing owner must receive historical fare construction/sale context required for reprice |
| Voluntary refund | ATPCO Cat33 automates full/partial refund and can require repricing of used portions | Allocation is not refund entitlement; Pricing/Refund owner returns the approved result |
| Penalty rules | ATPCO Cat16 plus Cat31/33 encode timing, scope, amount/percent, restrictions and waivers | Ordering records applied outcome/provenance, not a generic penalty rules engine |
| Optional services | ATPCO allows optional-service refundability/reusability and reissue/refund overrides | Ancillary servicing is source-driven; flight change triggers dependent-service evaluation |
| Void/cancel/refund/exchange | Travelport treats these as distinct ticket operations | Do not model one generic “cancel” that silently means all four |
| Revalidation | Amadeus/Travelport support revalidation only under issuer/rule/coupon eligibility and without issuing a replacement ticket | Revalidation is a document outcome of an accepted change, not a synonym for exchange |
| Reissue/exchange | Amadeus ATC separately exposes old/new fare, tax difference, penalty, add-collect and residual | Preserve accepted components independently and correlate them to the servicing operation |
| Partial refund | Amadeus refund record separates fare paid, fare used, fare refund, tax refund, penalty, fee, commission, FOP | Final servicing record must be redisplayable without recomputing today’s fare/rules |
| Refund commission | Amadeus/Travelport refund workflows retain commission and allow authorized refund commission treatment | Commission servicing result remains source-provided and independent from customer refund arithmetic |
| Ancillary accountable document | Sabre Offers & Orders / Travelport show fulfilled ancillary EMDs can be voided/refunded/exchanged under provider rules | Ancillary cancellation does not silently delete document/value history; provider document outcome is explicit |
| Involuntary | IATA and Travelport treat disruption recovery separately; revalidation or even exchange may apply | Preserve involuntary authority/reason and do not infer voluntary penalties |
| Group bookings | IATA Reservations Handbook/AIDM recognizes group bookings and special/bilateral rules | GroupBooking remains a separate v1 aggregate; commercial group policy is not invented locally |

## 1A. Benchmark completeness audit

The following post-sale topic families were explicitly rechecked in this final pass. “Covered” means the Ordering domain has the correct ownership/boundary and acceptance scenarios; it does **not** mean one universal carrier rule has been coded.

| Topic family | External benchmark used | Domain conclusion | Scenario coverage |
|---|---|---|---|
| pre-ticket withdrawal/cancellation | IATA Order servicing + vendor reservation/ticket distinction | commercial/capacity cancellation can exist without ticket refund | S-221..S-222 |
| void vs refund | Travelport, Sabre, IATA same-day/void examples | issuer/provider decides voidability; value execution remains separate | S-223..S-226 |
| full/partial refund | ATPCO Cat33, Amadeus Refund Record, Travelport refund flows | source computes approved fare/tax/fee/penalty result; allocation is not entitlement | S-231..S-245 |
| partially used refund | ATPCO/Amadeus/Travelport | used portion may be repriced/valued by source | S-234, S-256 |
| refund method / reusable / respend | IATA, ATPCO, Travelport | exact value/destination can be cash, residual/reusable or another owner-managed instrument | S-239..S-242, S-303, S-306 |
| voluntary change / reshop | IATA OrderReshop/Change, ATPCO Cat31 | quote/reshop is side-effect free; accepted plan drives mutation | S-246..S-256 |
| revalidation | Amadeus, Travelport | same document identity; exact eligibility is issuer/provider/rule driven | S-257..S-258 |
| exchange/reissue | Amadeus ATC, Travelport, Sabre | successor document lineage; fare/tax/penalty/value outcomes remain distinct | S-249..S-260, S-305 |
| penalty / waiver / no-show | ATPCO Cat16/31/33, IATA Penalty, Travelport structured rules | source-owned calculation; timing/scope/netting/waiver retained; DCS NoShow is not a fee | S-261..S-272 |
| tax on penalty and refund/reissue commission | Travelport, Amadeus | keep source-calculated tax/commission distinguishable; no local formula | S-293, S-304 |
| EMD-A / EMD-S / ancillary refund-exchange | ATPCO Optional Services, Travelport EMD, Sabre Offers & Orders | provider capability + service dependency determine document/value action | S-273..S-282, S-307 |
| planned schedule change / customer decision | IATA implementation guidance | action-required/options/deadline are recovery facts; no local auto policy | S-301..S-303 |
| disruption / involuntary recovery | IATA, Travelport, Sabre | preserve authority/reason; revalidation/even exchange/refund are explicit outcomes | S-283..S-288, S-308 |
| checked-in/control conflict | Sabre/Amadeus/Travelport document-control behavior | control release/reconciliation precedes unsafe document action | S-295..S-296 |
| name correction/change | IATA servicing scope + Amadeus/vendor provider behavior | provider-specific; correction vs traveler substitution remains distinct | S-289 |
| corrective refund/exchange/void | Amadeus refund cancellation + ticketing provider history | append corrective operation; never erase processed history | S-245, S-294 |
| refund/exchange record / notice / receipt | Amadeus Refund Record, Travelport Refund/Exchange Notice | immutable semantic servicing record; presentation artifacts are derived/platform-owned | S-290..S-291, S-299 |
| multiple FOP / payment uncertainty | IATA payment structure + existing AeroTech Payment boundary | Payment/StoredValue owns routing/execution; Ordering retains confirmed evidence | S-040..S-043, S-242..S-244 |
| group/charter servicing continuity | IATA Reservations Handbook + normal Order servicing once materialized | GroupBooking owns block/name lifecycle; child Orders use normal servicing/document rules | S-074..S-077, S-211..S-217 |

No additional Ordering aggregate emerged from this audit. Remaining carrier-specific exact rules (void window, waiver authority, EMD capabilities, refund destination, fare/tax/commission formula, checked-in control procedures, etc.) are **provider/configuration contracts**, not undocumented decisions for the agent.

---

## 2. Canonical servicing model

Every post-sale operation is separated into four concerns:

```text
1. Commercial intent / Order change
   what service obligation is added, cancelled, retained, replaced or moved

2. Pricing decision
   what commercial value changes and why

3. Fulfillment/document action
   reservation, ETKT/EMD coupon, revalidation, reissue, void, control/reassociation

4. Payment/value execution
   additional collection, refund, release, residual/voucher/wallet application, or no movement
```

A fifth concern is created after finalization:

```text
5. Servicing record
   immutable redisplay/audit view of what was actually accepted and completed
```

These concerns are correlated by the same durable servicing-operation/change identity but do not share one global state. A successful document change does not fabricate a successful refund. A confirmed refund does not by itself prove the commercial service was cancelled. A cancelled commercial service does not prove money was returned.

### Mandatory quote/commit separation

A servicing quote/reshop is side-effect free. It may answer:

- whether the requested scope can be changed/refunded;
- affected services/items/fare construction;
- replacement services/itinerary;
- fare and tax treatment;
- penalty/waiver treatment;
- add-collect, refund, residual or reusable-value treatment;
- document action required;
- ancillary/dependency treatment;
- expiry/version/provenance.

Only an explicit accepted quote/change plan starts the durable execution flow. Before irreversible execution, Ordering rechecks commercial version plus external evidence relevant to the operation. A stale quote is not silently recalculated by Ordering.

---

## 3. Pre-ticket cancellation / abandonment

A reservation/order can be withdrawn before an accountable document exists.

Canonical behavior:

1. Identify exact service/item scope and dependencies.
2. Obtain cancellation treatment from the commercial/pricing owner when value or fees can change.
3. Release/cancel external capacity under a stable operation key when required.
4. Release unused payment authorization/coverage through its owner when required.
5. Commit the commercial cancellation only after the required external finalization evidence for that provider flow.
6. Record any separate cancellation fee returned by Pricing.

This is **not a ticket refund** because no ticket was issued. If money was already captured, Payment may still execute a refund, but the refund is correlated to the cancelled commercial obligation rather than to a fabricated ticket.

---

## 4. Void of an issued ticket or EMD

Void is a document operation available only when the issuer/provider/market rules say the document is voidable. Travelport notes that the void period is often same-day but can vary; therefore no hard-coded “same day” rule belongs in Ordering.

Canonical behavior:

1. Verify current document/coupon state and control.
2. Verify issuer/provider void eligibility.
3. Persist the void intent with stable provider identity.
4. Execute/reconcile the void.
5. Record confirmed document outcome.
6. Apply the accepted commercial/pricing reversal treatment.
7. Ask Payment to release/void/refund the corresponding monetary movement according to the actual payment state.

A document void does **not** prove a PSP capture was reversed. An authorization may be released, while a captured payment may require a refund. Payment is authoritative for that distinction.

If the void window is no longer available, the operation must be evaluated as refund/cancellation according to current rules; never force a document into Void to simplify accounting.

---

## 5. Voluntary cancellation and refund

Voluntary cancellation is a commercial decision. Refund is the resulting value/money disposition. They are correlated but not identical.

### 5.1 Fully unused ticket/order scope

Pricing/Refund owner decides, from original terms and current request context:

- refundable fare/value;
- refundable/non-refundable taxes and surcharges;
- cancellation/refund penalty;
- ancillary treatment;
- residual/reusable value if applicable;
- commission/settlement adjustment if it is part of the source result;
- form/value destination when that is a commercial rule.

Ordering records the accepted result and its provenance; Payment/StoredValue executes the actual value movement.

### 5.2 Partially used ticket

A partially used ticket cannot be refunded by subtracting service allocations mechanically. Industry refund automation may reprice the flown/used portion using applicable historical rules and then determine unused refundable value.

Therefore:

```text
refund != original total - allocations of delivered services
```

The refund decision may contain independently determined:

- original fare paid;
- used/flown fare valuation;
- unused fare credit;
- refundable tax occurrences;
- retained/non-refundable tax occurrences;
- carrier-surcharge treatment;
- cancellation/no-show penalty;
- ancillary value;
- refund total/residual.

Ordering persists the source-approved result. It does not calculate `FareUsed` or reprice flown portions.

### 5.3 Non-refundable fare with refundable tax

A zero refundable fare does not imply zero refund. Refundable government tax or service amounts may still be returned. Conversely, a refundable fare does not imply every tax/fee is refundable. Each source occurrence keeps its own accepted treatment.

### 5.4 Refund destination

Industry rules may return value to original form of payment, voucher/e-voucher, residual value, wallet/travel bank or another authorized destination. Ordering records the approved disposition reference. The system that owns that value instrument executes it.

Do not create a voucher/wallet subsystem inside Ordering merely because a refund quote names that disposition.

---

## 6. Voluntary itinerary change

The canonical change flow is:

```text
Request change
-> reshop / informative quote
-> user accepts exact quote/version
-> protect replacement capacity
-> protect required additional funding
-> obtain document/control readiness
-> execute reservation/document plan
-> commit commercial replacement + immutable price delta
-> complete/refconcile value movement
-> update dependent ancillaries / DCS
-> create final servicing record
```

The accepted change plan must state which current services continue, which are replaced/cancelled, and which new services are created. A repriced PricingUnit does not imply every underlying `OrderService` gets a new identity. Already delivered or commercially unchanged services retain their stable identity.

### 6.1 Monetary outcomes

A change can produce any of these industry-recognized outcome shapes:

| Outcome | Meaning |
|---|---|
| Even exchange | no net fare/tax difference; a penalty may still exist if source says so |
| Additional collection | customer owes more because of new fare/tax/fees and/or separately collected penalty |
| Refund | accepted change produces customer credit returned by authorized value mechanism |
| Residual/reusable value | unused value is retained for later use rather than returned now |
| Add-collect + refund | different components move in opposite directions and source authorizes both |
| Add-collect + residual | new collection is required while separate old value is retained for later use |
| Penalty netted | penalty reduces credit/refund/residual amount |
| Penalty separately paid | penalty is collected independently even when another component creates credit |

The implementation must not collapse these into one signed “difference”. The source-approved components and their purpose must remain auditable.

---

## 7. Revalidation

Revalidation changes the binding of an existing ticket/coupon without issuing a replacement ticket. Amadeus and Travelport both make revalidation eligibility dependent on carrier/issuer support, fare/rule outcome and coupon/control status.

Domain rules:

- revalidation is **one possible fulfillment/document outcome** of an accepted itinerary change;
- Ordering must not decide eligibility from a fixed list of dates/airports/carriers copied from one vendor;
- the issuer/provider capability contract returns whether this exact change can be revalidated;
- the accepted pricing result must confirm the required value treatment;
- affected coupons and replacement flight binding are recorded;
- old ticket identity remains the accountable document;
- no replacement ticket is fabricated.

If provider/rule eligibility is unresolved, the operation is PendingEvidence/blocked. Do not silently fall back to reissue unless the accepted change plan explicitly allows that alternative.

---

## 8. Exchange / reissue

Exchange/reissue creates a successor accountable document for eligible unused coupons/services and links it to the original document.

A mature industry reissue calculation can independently include:

- old fare/value;
- new fare/value;
- fare balance/difference;
- old and new taxes and their difference;
- penalty;
- total additional collection;
- residual/refundable value;
- original and new form-of-payment references;
- commission effects;
- document endorsements/restrictions.

Ordering does not calculate those components. It stores the accepted Pricing result and links it to the old/new service and document lineage.

### Partially used exchange

The change scope may include only unflown services while historical used segments remain part of the pricing context. Pricing may require all affected unflown segments or the whole relevant pricing unit. Ordering must therefore retain source FareConstruction and used-service history instead of rebuilding it from current itinerary alone.

### Document lineage

A successful reissue records:

```text
original document/coupon refs
successor document/coupon refs
affected service refs
servicing operation/change ref
accepted pricing decision ref
provider/issuer confirmation ref
voluntary/involuntary authority context
```

Do not delete or overwrite the old issued document to represent reissue.

---

## 9. Penalties, fees and waivers

Ordering needs to represent the **applied result**, not reproduce ATPCO Category 16/31/33 logic.

The source result may distinguish these semantic dimensions:

| Dimension | Examples observed in industry workflows |
|---|---|
| Trigger | cancellation, refund, voluntary change/reissue, no-show/failure-to-use, replacement/name correction, optional-service change |
| Timing | before departure, after departure, anytime, same-day/defined period when source rules use it |
| Assessment scope | fare component, pricing unit, direction, journey, ticket, coupon, OrderItem/service |
| Calculation outcome | fixed amount, percentage, higher/lower/minimum/maximum, or already-computed source result |
| Collection treatment | net from refund/residual, collect separately, include on replacement document, document through EMD when supported |
| Waiver | schedule change, disruption, illness/death or other carrier-authorized reason; requires authority/evidence when used |
| Reusability/refundability | refundable, non-refundable, reusable/residual according to source rules |
| Settlement/commission | separate source result where applicable; never inferred by Ordering |

These dimensions are **business semantics only**. This document does not prescribe their code representation.

### Penalty taxation and commission

Some ticketing workflows collect a servicing penalty through an EMD/fee/tax-coded mechanism, and tax can itself apply to a penalty. Those are provider/market/source results. Ordering keeps the underlying penalty and any source-calculated tax occurrence distinguishable; it does not recode a penalty as tax or calculate tax-on-penalty locally.

Refund/reissue commission can differ from original issue commission. When the source result supplies a refund/reissue commission treatment, preserve it independently from customer payable/refund. Core customer/agency data is not a commission calculator.

### Penalty invariants

1. A penalty is a new applied servicing charge/effect, not a reversal of original fare merely because it occurs during cancellation.
2. A penalty may be netted from a credit or separately collected. Do not assume one treatment.
3. A waived penalty is not a zero-valued fabricated penalty; preserve the waiver/authority and source decision needed for audit.
4. No-show fee is not inferred from `NoShow` delivery status.
5. If provider/Pricing returns no reliable applied penalty result, Ordering does not calculate one from raw fare-rule text.
6. A penalty documented through EMD-S does not require inventing a deliverable `OrderService`.

---

## 10. No-show, forfeiture and failure to use space

`NoShow` is an operational observation from DCS/reservation context. Structured fare-rule systems separately recognize failure-to-use/no-show penalties. Therefore:

```text
DCS NoShow != automatic cancellation
DCS NoShow != automatic forfeiture
DCS NoShow != automatic penalty
```

After a NoShow observation, policy/Pricing may decide that:

- downstream services remain active;
- remaining itinerary is cancelled;
- a no-show penalty applies;
- unused value is forfeited;
- some value remains reusable/refundable;
- manual review is needed.

Each commercial consequence requires an explicit servicing decision/change. Never derive monetary loss from delivery status alone.

---

## 11. Involuntary change and disruption

Involuntary servicing is initiated by an authoritative airline/disruption fact rather than customer choice. IATA examples include cancellation, delay, misconnection and non-delivery of ancillary due to operational change.

Canonical flow:

1. Flight/DCS/Disruption owner publishes the operational/business impact.
2. Ordering projects affected services without rewriting sold history.
3. Disruption policy provides authorized recovery options or a recovery instruction.
4. Pricing/servicing source provides the approved involuntary monetary/waiver treatment when required.
5. Customer/agent accepts an option when business flow requires choice.
6. Ordering executes reservation/document/value steps with stable operation keys.
7. Original and replacement services/documents retain lineage and involuntary reason/authority.

Do **not** hard-code “involuntary always means zero price/zero penalty”. Many standard workflows are even exchanges and waive change fees, but the authoritative recovery/pricing contract must state the treatment for the exact case.

If the customer rejects the offered recovery, the next outcome may be another reaccommodation or an involuntary refund. That is a new accepted servicing decision, not a mutation of the original disruption fact.

---


### Planned schedule-change decision window

IATA implementation guidance explicitly allows a planned schedule change to remain pending customer action. The customer/seller may accept the proposed change, reshop an alternative, cancel and refund, or cancel/release capacity while keeping value/document reusable/open for later action when the airline policy allows. Source policy can also define what happens when the decision deadline expires.

AeroTech consequence: the Disruption/Recovery projection must expose source-provided customer-action requirement, options, deadline and final disposition, but Ordering does not implement an airline-wide timeout policy. A deadline never means “auto refund” or “auto accept” unless the authoritative recovery instruction says so.

`Respend/reusable` is a commercial/value disposition, not a wallet balance. Exact reusable value may remain unknown until later reshop/refund.

---

## 12. Ancillary and EMD servicing

When an air service changes, every dependent ancillary is evaluated explicitly. Possible source-approved outcomes include:

- keep unchanged;
- reassociate to replacement flight/service;
- replace/reissue;
- refund;
- retain residual/reusable value;
- cancel without refund;
- manual review because supplier/provider cannot automate the action.

Examples:

- paid seat invalidated by aircraft swap;
- baggage moved to a replacement itinerary;
- lounge access tied to old departure airport/time;
- meal on a changed segment;
- third-party hotel/ground supplier cancellation terms;
- paid upgrade/downgrade value;
- EMD-A associated to a flight service;
- EMD-S documenting a standalone fee, penalty, deposit or residual-purpose value.

ATPCO optional-service data supports refundable/non-refundable/reusable treatment and can specify form-of-refund/commission/interline characteristics. Ordering stores the accepted source outcome and does not invent an ancillary refund formula.

---

## 13. Name correction / passenger servicing

Name correction and passenger substitution are not the same business action. Airline/provider policy may require anything from a simple correction to reissue, refund/new issue, fee/penalty, or manual authorization.

Ordering therefore:

- preserves the distinction between correction and change of traveler identity;
- obtains provider/issuer/pricing treatment before changing issued-document consequences;
- preserves original and corrected audit references subject to privacy retention;
- never implements a universal “name correction fee” or “always reissue” rule.

---

## 14. Corrective operations: cancel refund / reverse void or exchange

Some providers allow cancellation/reversal of a refund or correction of a reissue/void under limited conditions. These are **new corrective operations with their own provider evidence**, not deletion of historical operations.

Rules:

- retain original refund/reissue/void record;
- require provider/issuer confirmation that reversal/correction is allowed;
- preserve new document/payment movements and lineage;
- reconcile external reality first if previous outcome is Unknown;
- never restore commercial/document state merely by flipping a local status.

---

## 15. ServicingRecord: redisplay/audit, not another aggregate

Mature ticketing systems retain a processed refund/reissue record that can be redisplayed after the operation. Amadeus Refund Record is a clear operational benchmark: it can show original document/coupons, fare paid/used/refund, tax refund, cancellation penalty, fees, commission, original form of payment and authorization, and becomes non-editable after processing.

AeroTech needs the same **business capability**, but not an Amadeus clone.

After an operation finalizes, Ordering stores or projects an immutable `ServicingRecord` semantic view sufficient to redisplay what happened without recalculating current fares/rules. It references, as available from authoritative sources:

- servicing operation/change;
- original and successor documents/coupons;
- affected services/items;
- accepted pricing/change decision and immutable price lines;
- penalty/waiver/authority evidence;
- payment/refund/add-collect/residual/value-movement references;
- provider confirmations and timestamps;
- actor/seller/office context required for audit.

**Implementation shape is not prescribed here.** It may be a projection/table/view using existing patterns; it is not a new aggregate unless implementation evidence later proves independent invariants/lifecycle require one.

Processed records are append-only audit facts. A correction/cancel-refund creates another correlated record rather than editing historical financial facts.

---

## 16. Notices, receipts and customer documents

A `RefundNotice`, `Exchange/ReissueNotice`, e-ticket receipt or EMD receipt is a **presentation artifact derived from authoritative records**, not Order truth.

Ordering is responsible for exposing/publishing the finalized servicing facts needed to generate the artifact. Rendering, storage, language/template selection and delivery must reuse an existing AeroTech document/notification capability if one exists. If ownership is not established in the current platform, the agent must stop at the integration boundary and raise `BLOCKED_DECISION`; it must not introduce a PDF/document subsystem inside Ordering.

Ownership defaults:

| Artifact/fact | Default authoritative owner |
|---|---|
| Refund/exchange servicing record | Ordering servicing/audit view, built from accepted authoritative facts |
| Refund/exchange customer notice | derived presentation artifact; discover/reuse platform document/notification owner |
| ETKT / EMD accountable document | Ordering fulfillment/document boundary as defined in v1 |
| ETKT/EMD receipt | presentation artifact derived from document truth |
| Payment receipt | Payment/JetPay owner |
| Voucher/wallet balance | its value owner (e.g. StoredValue), not Ordering |
| Invoice / credit note / receivable | billing/receivables/accounting owner, not Ordering |
| Accounting journal/posting | Ledger owner |

---

## 17. Financial ownership and audit invariants

### Pricing / AirPrice / Offer owner

Authoritative for commercial calculations it is designed to perform, including accepted fare/tax/surcharge/discount/penalty/reprice/refund/change result. It may consume external fare/tax rule sources. Ordering does not reproduce these engines.

### Ordering

Authoritative for the accepted commercial history:

- what was sold;
- what was changed/cancelled/replaced;
- which accepted monetary result belongs to each change;
- service/item/fare/document lineage;
- servicing operation and immutable final record/provenance.

Ordering may validate conservation/reconciliation rules that are unambiguous from the accepted source payload, but does not derive a missing fare/tax/penalty/refund result.

### Payment / JetPay / StoredValue

Authoritative for actual value movement: authorization, capture, refund, release, payment method/tender, voucher/wallet execution and provider outcome. Ordering consumes confirmed evidence and retains references required for commercial audit.

### Ledger

Authoritative for accounting recognition, posting, settlement/accounting classifications and financial books. Ordering’s commercial price history is not the GL.

### Currency / rate ownership

AirInfo/BasicInfo supplies currency reference data in the current platform and AirPrice owns rate-of-exchange source tables according to existing AeroTech service documentation. Ordering may retain the exact applied/source-provided transaction snapshot needed to reproduce accepted history, but must not create a second currency/ROE master or exchange-rate engine. Representation is resolved from current platform contracts under document `13`.

---

## 18. Financial outcome matrix

| Scenario | Commercial effect | Pricing decision | Document effect | Value execution |
|---|---|---|---|---|
| pre-ticket cancel, no captured money | cancel service/item | possible fee or zero delta | none | release unused authorization/coverage if any |
| ticket void | cancel/withdraw scope as approved | reversal/void treatment | void eligible coupons/document | release or refund according to actual Payment state |
| full voluntary refund | cancel refundable scope | fare/tax/fee/penalty result | refund/withdraw eligible coupons | Payment/value owner executes approved credit |
| partial used refund | retain used, cancel/refund unused scope | source reprices/values used portion + refundable remainder | only eligible unused coupons affected | approved refund/residual only |
| voluntary change even | replace target service | zero net price difference, penalty possible | revalidate or reissue | no movement or penalty collection |
| change with add-collect | replace target service | positive additional requirement | revalidate/reissue as source permits | collect confirmed difference/penalty |
| change with residual/refund | replace target service | source returns credit/residual | reissue typically; provider-specific | refund or retain value through owner |
| no-show | no automatic commercial mutation | source policy determines penalty/forfeit/reuse | coupon may remain/transition per issuer evidence | no automatic movement |
| involuntary recovery | replace/retain according to recovery | approved involuntary/waiver result | revalidate/even reissue/new issue as provider supports | only source-approved value treatment |
| ancillary unavailable after disruption | cancel/replace ancillary | source refund/replacement value | EMD reassociate/refund/reissue if applicable | refund/residual/replacement through owner |

---

## 19. Hard servicing invariants

1. `Cancel != Void != Refund != Exchange/Reissue != Revalidation` even when a UI exposes one action.
2. Quote/reshop is side-effect free; acceptance starts mutation.
3. Allocation is not refundable value.
4. Current delivery state is not fare-rule outcome.
5. No-show does not automatically cancel downstream travel or create a penalty.
6. Used/flown value is not inferred from original service allocation when source rules require repricing.
7. Fare, tax, carrier surcharge, commercial fee, penalty, discount and commission treatment remain distinguishable when the authoritative quote distinguishes them.
8. Customer-collected tax remains part of customer commercial value even if settlement/accounting treatment is handled elsewhere.
9. Revalidation does not create a replacement ticket.
10. Reissue/exchange preserves old/new document lineage.
11. Unknown provider outcome is reconciled under the same durable operation key; it is never blind-retried as a new economic/document action.
12. Refund/payment completion and commercial cancellation are separately observable.
13. Residual/reusable value is not represented as an exact guaranteed amount unless an authoritative owner has produced that amount.
14. Involuntary authority/reason is preserved; penalty waiver is not guessed.
15. Corrective operations append evidence; they do not delete prior servicing history.
16. Customer-facing notices are derived artifacts, not sources of Order truth.
17. Ordering does not create fare, tax, FX, penalty, payment, ledger, voucher/wallet or document-rendering engines that belong elsewhere.

---

## 20. Implementation acceptance for P3/P5

Before P3 is complete, automated tests must cover at least:

- pre-ticket cancellation with and without captured money;
- void success, void-window rejection and unknown void outcome;
- full unused refund;
- partially used refund where source provides used-fare repricing;
- refundable-tax/non-refundable-fare and the reverse;
- penalty netted from credit and penalty separately collected;
- waiver with explicit authority;
- even exchange, add-collect, refund, residual/reusable, and mixed outcomes;
- revalidation vs reissue based on issuer capability response;
- partially used exchange;
- cancel/refund correction without deleting old record;
- no-show with downstream travel retained and no-show with explicit cancellation/forfeit decision;
- ancillary reassociation/refund after flight change;
- EMD-A and EMD-S servicing outcomes;
- immutable ServicingRecord redisplay after later Order changes;
- payment/refund Unknown reconciliation with one stable economic operation.

Before P5 is complete, add:

- schedule change with revalidation;
- schedule change with even reissue;
- airline cancellation with reaccommodation accepted;
- customer rejects recovery and receives another option/refund path;
- misconnection; aircraft/seat downgrade; ancillary non-delivery;
- involuntary authority/waiver retained through ticket/document history.

See `05-SCENARIO-VALIDATION.md` for concrete scenario IDs.

---

## 21. Public benchmark sources checked in this pass

These are evidence references, not implementation dependencies:

- IATA, **Servicing in NDC**: https://www.iata.org/contentassets/6de4dce5f38b45ce82b0db42acd23d1c/ndc-infocus-servicing.pdf
- IATA AIDM, **Order Item**: https://airtechzone.iata.org/aidm_model/25.2/EARoot/EA6/EA2/EA2/EA3/EA11441.htm
- IATA AIDM, **Cancel Restrictions**: https://airtechzone.iata.org/aidm_model/24.1/EARoot/EA6/EA1/EA2/EA11/EA11978.htm
- IATA AIDM, **Change Restrictions**: https://airtechzone.iata.org/aidm_model/20.1/EARoot/EA4/EA1/EA3/EA12/EA2520.htm
- IATA AIDM, **Order Item Reusable Indicator**: https://airtechzone.iata.org/aidm_model/24.1/EARoot/EA6/EA1/EA2/EA7/EA11955.htm
- IATA AIDM, **Order Penalty Information**: https://airtechzone.iata.org/aidm_model/24.2/EARoot/EA6/EA2/EA2/EA3/EA12878.htm
- IATA AIDM, **Penalty**: https://airtechzone.iata.org/aidm_model/24.2/EARoot/EA6/EA1/EA2/EA7/EA11985.htm
- IATA AIDM, **Coupon**: https://airtechzone.iata.org/aidm_model/24.2/EARoot/EA6/EA2/EA2/EA3/EA12822.htm
- IATA, **Reservations Handbook**: https://www.iata.org/en/publications/manuals/reservations-handbook/
- ATPCO, **Automated changes and refunds**: https://info.atpco.net/automated-changes-and-refunds-foundational-for-airline-retailing
- ATPCO, **Category 16/31/33 overview**: https://info.atpco.net/change-refund-billing-disputes
- ATPCO, **Optional Services / Reissue-Refund**: https://faremanager.atpco.net/atpapps/fmhelp/mergedProjects/Optional%20Services/what_are_optional_services.htm
- ATPCO, **Optional Services provisions**: https://faremanager.atpco.net/atpapps/fmhelp/mergedProjects/Optional%20Services/Create_and_Update_Provisions.htm
- Travelport, **Exchange, Refund, and Void Guide**: https://support.travelport.com/webhelp/jsonapis/airv11/content/air11/General/ExchangeRefundGuide.htm
- Travelport, **Refund/Exchange Dialog and Notice (REN)**: https://support.travelport.com/webhelp/GlobalWare/Content/05-Invoice/Using_the_Refund_Exchange_Dialog_Box.htm
- Travelport, **Structured Fare Rules Definitions**: https://support.travelport.com/webhelp/uapi/Content/Air/Fare_Rules/Structured_Fare_Rules_Definitions.htm
- Travelport, **Involuntary Exchanges**: https://support.travelport.com/webhelp/smartpoint1p/content/air/ticketexchange/InvoluntaryExchange/Involuntary_Exchanges.htm
- Amadeus, **How to refund a ticket**: https://amadeusdev.service-now.com/csm/en/how-to-refund-a-ticket-cryptic?id=kb_article&sysparm_article=KB0017923
- Amadeus, **Redisplay a refund record**: https://dev.myamadeus-services.amadeus.com/csm/en/how-to-redisplay-a-refund-record-cryptic?id=kb_article&sysparm_article=KB0017970
- Amadeus, **ATC Reissue**: https://amadeusdev.service-now.com/csm/en/amadeus-ticket-changer-atc-reissue-how-to-reissue-an-e-ticket-cryptic?id=kb_article&sysparm_article=KB0017922
- Amadeus, **Revalidate an e-ticket**: https://amadeusdev.service-now.com/csm/en/how-to-revalidate-an-e-ticket-cryptic?id=kb_article&sysparm_article=KB0017934
- Amadeus, **Refund ticket / commission and waiver handling**: https://amadeusdev.service-now.com/csm/en/how-to-refund-a-ticket-cryptic?id=kb_article&sysparm_article=KB0017923
- Travelport, **Assisted Ticketing Partial Refund**: https://support.travelport.com/webhelp/Smartpoint1G1V/Content/Air/Ticketing/TicketAssistant/TicketAssistant_RefundPartial.htm
- Travelport, **EMD refund/void/exchange**: https://support.travelport.com/webhelp/formats/Content/DocProd/EMD.htm
- Sabre, **Offers and Orders APIs user guide** (ancillary fulfilled cancellation / EMD outcomes): https://developer.sabre.com/sites/default/files/2024-04/Sabre%20Offers%20and%20Orders%20APIs%20user%20guide%20v1.6.pdf
- Sabre, **Schedule Change Web Service user guide**: https://developer.sabre.com/sites/default/files/2019-08/Schedule_Change_Web_Service_User_Guide_SDS_v1.1.pdf
- Sabre, **eTicket coupon status reference**: https://developer.sabre.com/soap-api/send-sabre-command/2.0.0/help-documentation/eticketcouponllsrq.html

