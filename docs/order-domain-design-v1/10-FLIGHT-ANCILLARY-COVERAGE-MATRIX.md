# 10 - Flight, Ancillary and Pricing Coverage Matrix

## Purpose

This matrix is the final design-level coverage pass requested before implementation freeze. It answers a narrower question than `05`: **does the proposed domain represent the important combinations of flight topology, capacity, carrier responsibility, ancillary scope, delivery state, pricing basis and servicing without adding a new aggregate for every case?**

The answer after S-001..S-308 is yes at the design/domain level for the target product. Adapter-specific behavior, jurisdictional tax rules and load/concurrency behavior remain implementation release gates rather than extra domain concepts.

## Benchmark anchors

- IATA Passenger Segment: a leg or group of legs from passenger board point to deplaning point; a passenger segment can therefore span multiple physical legs. This is why a physical leg is not automatically an `OrderService`. Source: https://airtechzone.iata.org/aidm_model/24.1/EARoot/EA5/EA1/EA2/EA9447.htm
- IATA 21.3 implementation guide distinguishes Passenger Segment from Leg and illustrates one passenger segment over multiple operating legs. Source: https://guides.developer.iata.org/docs/21-3_ImplementationGuide.pdf
- IATA AIDX covers operational flight identity, codeshare, operational times, disruption detail, passenger/baggage statistics and aircraft data. Source: https://www.iata.org/en/publications/info-data-exchange/
- IATA Offers & Orders interline direction distinguishes Retailer and Supplier responsibilities and requires cross-party delivery/disruption information. Source: https://www.iata.org/en/programs/airline-distribution/retailing/future-of-interline/
- IATA ONE Order direction keeps one customer Order across air and other products while delivery may involve airlines/partners. Source: https://www.iata.org/en/programs/airline-distribution/retailing/one-order/
- ATPCO optional-service subcodes include special/carry-on baggage, musical instruments and pet-in-cabin examples, confirming that special ancillary variety is not hypothetical. Source: https://www.atpco.net/sites/atpco-public/files/all_pdfs/Opt_Scvs_Industry_Sub_Codes_Online_C.pdf

These sources are benchmark anchors, not message-schema templates for AeroTech.

---

## 1. Flight topology coverage

| Case family | Scenarios | Model element used | Result |
|---|---|---|---|
| Direct OW / RT / OW+OW | S-001, S-005..S-007 | JourneySegment + AirService + FareConstruction | Covered |
| Through fare / fare break | S-008, S-009 | FareComponent may span one/many Services | Covered |
| Open jaw / circle / multi-city | S-010, S-011 | Journey + explicit segment topology | Covered |
| Surface / ARNK | S-010, S-138 | `SegmentKind=Surface`, no fabricated AirService | Covered |
| Open segment | S-136, S-137 | `SegmentKind=OpenAir`, later explicit binding | Covered |
| Connection vs stopover | S-133 | JourneyConnection | Covered |
| Protected vs unprotected transfer | S-147, S-148 | ProtectionType + disruption policy | Covered |
| Through flight, multiple physical legs | S-134, S-135 | one sold segment may bind many operational legs | **Model corrected and covered** |
| Direct -> connection / connection -> direct | S-120, S-139 | one-to-many / many-to-one service lineage | Covered |
| Diversion / return-to-origin | S-140, S-141 | SegmentOperationalState + disruption | Covered |
| Cancellation / reinstatement | S-069, S-142, S-143 | operational state separate from commercial lineage | Covered |
| Time zones/DST/date-line | S-149 | local time + TZ + unambiguous instant | Covered |

### Decision resulting from this pass

`JourneySegment` is a **sold passenger segment**, not an operational leg. A segment can bind one or more physical legs. This avoids creating fake commercial Services/coupons for technical/intermediate stops and is the only new structural correction required by this coverage pass.

---

## 2. Capacity and reservation coverage

| Case family | Scenarios | Rule | Result |
|---|---|---|---|
| Full/partial/unknown reservation | S-003, S-004 | per-Service observed state | Covered |
| Married/atomic segments | S-131, S-132 | ReservationCouplingGroup | Covered |
| Waitlist | S-144, S-145 | reservation status, not Order status | Covered |
| Airport standby | S-146 | DCS aspect, not Inventory waitlist | Covered |
| Extra-seat capacity | S-185 | ancillary + reservation requirement/confirmed quantity | Covered without fake traveler |
| Group capacity per flight | S-074, S-115, S-211 | per-block invariant | Covered |

No new Inventory aggregate is introduced inside Ordering. Inventory remains canonical owner; Ordering stores only reservation/coupling evidence needed for Order eligibility and servicing.

---

## 3. Carrier / supplier / interline coverage

| Case family | Scenarios | Representation | Result |
|---|---|---|---|
| Codeshare | S-151 | marketing + operating + supplier identities | Covered |
| Operating carrier change | S-152 | sold snapshot + current ops state | Covered |
| Retailer/Supplier split | S-153, S-157 | Order owner distinct from Supplier/DeliveryProvider | Covered |
| Partner ancillary | S-154 | same OrderService model + supplier refs | Covered |
| Multiple locators | S-155 | typed ExternalReference collection | Covered |
| Through check-in | S-156 | per-Service/aspect DCS facts | Covered |
| Issuer != operator | S-158 | document issuer/stock separate from flight carriers | Covered |
| Partner unknown outcome | S-159 | durable provider operation + Unknown/reconcile | Covered |
| Partner settlement changes | S-160 | settlement/accounting separate from customer price | Covered |

**Boundary:** cross-carrier execution/settlement protocol certification is not claimed implemented by the domain model. Unsupported partner workflows fail explicitly; this is preferable to introducing speculative interline aggregates now.

---

## 4. DCS / delivery coverage

| State/event family | Scenarios | Result |
|---|---|---|
| Check-in / boarded / flown / no-show | S-060..S-063 | Covered |
| Offload / reboard | S-062, S-162 | Covered |
| Denied boarding | S-161 | Covered; distinct from NoShow |
| Terminal correction | S-066, S-111, S-163 | Covered with supersession/reconciliation |
| Through check-in batch | S-164 | Covered |
| Flown then downstream no-show | S-165 | Covered |
| Coupon vs DCS disagreement | S-166 | Covered with reconciliation |
| Operational seat reassignment | S-167 | Covered without rewriting sold seat product |
| Baggage accepted/loaded/delivered/mishandled | S-168, S-169 | Covered by quantity/milestone evidence |
| Ancillary consumed/not claimed/partial quantity | S-064, S-170, S-171 | Covered |
| Delivery provider failure | S-172 | Covered without commercial auto-cancel |
| External document control | S-124, S-173 | Covered with explicit release ACK |
| Flight departure != passenger flown | S-174 | Covered |
| Large DCS batch | S-175 | structurally covered; runtime load gate |

No separate `ConsumptionFact` aggregate is required. Append-only `DeliveryObservation` plus current `ServiceDeliveryState` is sufficient and materially simpler.

---

## 5. Ancillary product coverage

| Product | Scenarios | Modeling choice | Result |
|---|---|---|---|
| Seat | S-029, S-030, S-070, S-127, S-167 | typed details | Covered |
| Baggage piece/weight/connection/RT | S-025..S-028 | typed details + coverage | Covered |
| Included + extra bag | S-194 | separate price treatment/services as sold | Covered |
| Pooled baggage | S-195 | shared beneficiaries + pooling policy | Covered |
| Sports/musical baggage | S-184 | Baggage special item code | Covered |
| Meal | S-031, S-032 | typed details | Covered |
| Lounge | S-033, S-034, S-190 | location/time/guest scope | Covered |
| Hotel | S-035, S-117, S-188 | typed stay details/shared beneficiaries | Covered |
| Ground transfer | S-036, S-117, S-189 | shared vehicle/beneficiaries | Covered |
| Insurance | S-037 | registered/typed product; claim outside Ordering | Covered |
| Priority / fast-track / CIP | S-038, S-179 | registered schema/profile | Covered |
| Wheelchair/special assistance | S-180 | complimentary Service + protected payload | Covered |
| UMNR | S-181 | registered schema + protected evidence | Covered |
| PETC / AVIH | S-182, S-183 | registered schema + reservation profile | Covered |
| Extra seat / CBBG | S-185 | ancillary capacity service, no fake traveler | Covered |
| WiFi | S-064, S-186 | registered schema + segment/journey/time scope | Covered |
| SIM/eSIM | S-187 | non-flight third-party delivery | Covered |
| Paid/involuntary upgrade/downgrade | S-176..S-178 | Air change + pricing/disruption evidence | Covered |
| Mixed bundle | S-038, S-054, S-191, S-192 | one OrderItem, several Services | Covered |
| Ancillary reassociation after rebook | S-193 | ServiceDependency + explicit policy | Covered |

### Why no `OrderXService` explosion is required

Core types with materially different invariants/query shape (Air, Seat, Baggage, Meal, Lounge, Hotel, GroundTransport) have typed details. Less common products use a **registered, versioned detail schema + fulfillment profile**. They become a dedicated typed table only when behavior/query volume proves it useful. This keeps the model comprehensive without forcing a class/table for every commercial catalogue code.

---

## 6. Pricing application coverage

| Pricing dimension | Scenarios | Result |
|---|---|---|
| True RT vs OW+OW | S-005..S-007, S-050, S-051 | Covered by PricingUnit/FareComponent |
| Through fare / fare break | S-008, S-009 | Covered |
| Dynamic/charter no fare construction | S-013, S-014, S-217 | Covered; construction optional |
| ADT/CHD/INF | S-012 | Covered |
| Fare / Tax / YQ-YR / Fee / Penalty / Discount / Markup / Commission | S-015..S-024, S-087..S-092, S-201..S-205 | Covered as separate line types |
| Per-segment / bound / journey / document / booking / person | S-015, S-018, S-196..S-200 | Covered without amount multiplication |
| Bundle allocation/refund | S-020, S-054, S-088, S-191, S-192, F-P02 | Covered; allocation != refund entitlement |
| Tax exemption/inclusive/non-refundable/jurisdiction change | S-091, S-092, S-201, S-202 | Covered |
| Original/sale/document currency and applied conversion provenance | S-023, S-206, S-292 | Preserve authoritative accepted values/provenance; no new local ROE/currency model |
| Currency precision and residual rounding | S-024, S-207, S-208 | Covered |
| Exchange add-collect / residual | S-055, S-209 | Covered |
| Historical rules unavailable | S-210 | Explicit block/manual review, no guessed fare |
| Split/value transfer | S-056..S-059, S-129 | Covered without new sale/revenue duplication |

The pricing model therefore remains one general immutable monetary ledger plus optional Air FareConstruction; there is no separate price aggregate per product type.

---

## 7. High-fan-out and concurrency coverage

| Case | Scenario | Required implementation evidence |
|---|---|---|
| FlightCancelled 1,000 Orders | S-150 | <=30s local fan-out baseline; <500ms normal per-Order transaction; no global lock/provider call |
| DCS 180-passenger batch | S-175 | per-observation dedup/aspect correctness + adapter load test |
| Aircraft change 1,000 Orders / many seats | S-218 | fan-out only records impact; servicing stays separate |
| FlightOps + DCS + agent servicing race | S-219 | SQL/concurrency test proving no lost state |
| GetOrder/affected-flight search during load | S-220 | local index/projection, no synchronous cross-service fan-out |

These are **operational release gates**, not additional domain entities. The domain is intentionally not distorted to optimize a benchmark before code exists.

---

## 8. Servicing and financial-flow coverage

The S-221..S-308 benchmark pass closes the post-sale matrix that was previously thinner than flight/ancillary topology.

| Servicing risk family | Covered scenarios | Required ownership/result |
|---|---|---|
| Pre-ticket cancellation / abandonment | S-221..S-222 | commercial/capacity cancellation separate from captured-value disposition |
| Document void | S-223..S-226 | issuer/provider voidability + separate payment outcome + Unknown reconciliation |
| Cancellation without/with refund | S-227..S-245 | authoritative refund/fee/tax/waiver/FOP/reusable result; no allocation-derived refund |
| Informative reshop and voluntary change | S-246..S-256 | source quote first; even/add-collect/refund/residual combinations; pricing-unit context retained |
| Revalidation / exchange / reissue | S-257..S-260 | provider/issuer capability; same-document vs successor-document lineage |
| Penalties and waivers | S-261..S-272, S-304 | source calculation, scope/timing/netted-vs-paid/waiver/tax treatment; no local generic rules engine |
| No-show | S-267..S-272 plus DCS scenarios | operational fact does not automatically price/cancel/forfeit |
| Ancillary change/refund / EMD | S-273..S-282, S-307 | dependency/supplier/issuer decision, EMD-A/S and provider capability limits |
| Involuntary / disruption recovery | S-283..S-288, S-301..S-303, S-308 | explicit recovery plan, customer-decision window when supplied, authority/reason, no blanket assumption on fare difference/refundability |
| Name correction / control conflicts | S-289, S-295..S-296 | provider/issuer/DCS contract controls irreversible document action |
| Servicing record / notices | S-290..S-294, S-299 | semantic immutable record; presentation artifact owner discovered/reused |
| Shared monetary/platform ambiguity | S-292..S-300, S-306 | authoritative source/reference + `BLOCKED_DECISION`, never agent-invented shared primitive |

The benchmark basis is detailed in `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md`. The matrix specifies semantic coverage, not carrier-specific automatic eligibility.

---

## 9. Final gap decision

After S-001..S-308, no remaining reviewed scenario requires:

- a new business aggregate;
- an `Entitlement` layer;
- product-specific pricing ledgers;
- a global Order payment/ticket/DCS state;
- Event Sourcing for Order reconstruction;
- one `OrderXService` class/table for every catalogue product;
- read-time fan-out to Payment/DCS/Inventory/FlightOps.

The only structural correction from this last pass is the sold passenger-segment vs operational-leg distinction in `JourneySegment`. Everything else fits the existing six aggregate types, typed-or-registered Service details, reservation/document evidence, immutable pricing, delivery observations and disruption projection.

### Implementation freeze rule

A new concept may be added during implementation only when a concrete acceptance scenario cannot be represented or guarded by the existing model. Provider-specific fields belong in an adapter/profile or registered typed schema unless they change Order invariants. This is the primary anti-overengineering rule for future development.
