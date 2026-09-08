# P2-H — P2 Release-Gate Matrix

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-09

Verification documentation. Every row is backed by an existing or newly added executing test; no new
orchestration flow was created. Deltas are per **accepted, committed** mutation.

Version baseline after `Order.Create`: `CommercialVersion = 1`, `FinancialSequence = 1`,
`ObligationVersion = 2` (it starts at 1 and advances once because the customer total moves from zero).

---

## 1. Matrix

| # | Scenario | ΔCommercialVersion | ΔFinancialSequence | ΔObligationVersion | PriceChangeSet? | OrderPricingChanged? | External provider IO | Projection updated? | Result |
|---|---|---|---|---|---|---|---|---|---|
| 1 | **Create air Order** from an accepted offer | **+1** → 1 | **+1** → 1 | **+1** → 2 | **Yes** — one `OriginalSale` | **Yes** — exactly 1 | one offer retrieval through the AirPrice ACL, **before** the transaction | Yes | Pass |
| 2 | **Get local OrderView** (`GET Api/v1/Bookings/{id}`, `GET Backoffice/v1/Orders/{id}/Details`) | 0 | 0 | 0 | No | No | **none** — asserted zero for quote, reservation, funding, ticket, EMD | No | Pass |
| 3 | **Reserve** | 0 | 0 | 0 | No | **No** | reservation provider, outside the transaction, keyed on the operation | Yes | Pass |
| 4 | **Funding evidence / confirmation** | 0 | 0 | 0 | No | **No** | funding coverage port | Yes | Pass |
| 5 | **ETKT issue** | 0 | 0 | 0 | No | **No** | document issuance port; number allocated before dispatch | Yes | Pass |
| 6 | **Add ancillary / service** (separately priced: ProductCharge + Tax + SettlementOnly Commission) | **+1** → 2 | **+1** → 2 | **+1** | **Yes** — one `AddProduct` | **Yes** — exactly 1, all 3 lines in one envelope | one quote acceptance, outside the transaction | Yes | Pass |
| 7 | **Pricing event payload** for #6 | — | — | — | — | 1 event / 3 lines | — | — | `DerivedCustomerBalanceImpact = 220 000` for 200 000 charge + 20 000 tax; the 10 000 commission is `SettlementOnly` and **excluded** from customer total |
| 8 | **EMD issue** (Associated and Standalone) | 0 | 0 | 0 | No | **No** | EMD issuance port; number allocated before dispatch; retry reuses number + operation key | Yes | Pass |
| 9 | **Idempotent replay** of #6 (same key, same request) | 0 | 0 | 0 | **No second set** | **No second event, no second outbox row** | **quote provider not called again** | No new revision | Pass |
| 10 | **Cross-customer denial** (foreign `GET` and foreign `Change`) | 0 | 0 | 0 | No | No | **quote call count = 0** | **No** | 404, non-disclosing; no receipt / operation / claim / change / item / service / line / outbox row |
| 11 | **Settlement-only pricing change** | **+1** | **+1** | **0 — unchanged** | **Yes** | **Yes** — exactly 1 | one quote acceptance | Yes | `DerivedCustomerBalanceImpact = 0`, `CustomerTotal` unchanged |
| 12 | **Current AirPrice source, no fare construction** | +1 → 1 | +1 → 1 | +1 → 2 | Yes | Yes | offer retrieval | Yes | `FareConstructions = []`; nothing fabricated; ETKT falls back to the transitional service field |
| 13 | **Explicit fare-construction domain scenario** (true RT, OW+OW, OpenJaw, through fare, fare break) | unchanged by the construction itself | unchanged | unchanged | No | No | none | Yes | Accepting a construction moves no monetary state; ETKT takes its fare context from the active `FareComponent` |
| 14 | **Rejected / stale-version change** | 0 | 0 | 0 | **No partial set** | No | quote not called (stale version rejected first) | No | Order not left blocked |
| 15 | **Failed quote acceptance** | 0 | 0 | 0 | No | **No orphan message** | quote called and failed | No | Claim released, aggregate unchanged |
| 16 | **OTA creation without customer context** (`Api` and `OtaPanel` `FlightOffers`) | 0 | 0 | 0 | No | No | **none — offer never retrieved** | No | 2890 / 403; **no Order row created** |

## 2. Evidence index

| Row | Tests |
|---|---|
| 1 | `An_initial_sale_records_one_create_change_and_the_expected_versions`, `Creating_an_order_writes_one_pricing_change_message`, `Creating_an_order_emits_exactly_one_pricing_change_event`, `Every_original_sale_line_travels_in_the_same_envelope`, `The_create_events_share_the_first_commercial_version` |
| 2 | `Retrieval_calls_no_upstream_provider`, `An_ota_read_touches_no_upstream_provider`, `Get_order_redisplays_from_the_local_projection_without_upstream_calls`, `The_query_contract_is_strongly_typed` |
| 3 | `Reservation_confirmation_does_not_advance_the_commercial_version_but_updates_the_projection`, `Reservation_outcomes_do_not_advance_the_commercial_version`, `Reservation_and_issuance_write_no_pricing_message` |
| 4 | `Funding_that_is_not_confirmed_blocks_issue`, `The_full_slice_creates_reserves_verifies_funding_issues_and_redisplays` |
| 5 | `Document_issue_does_not_advance_the_commercial_version`, `Each_coupon_maps_to_the_correct_traveler_service_and_sold_segment`, `The_provider_key_derives_from_the_operation_not_from_the_attempt_count`, `A_retried_issue_with_the_same_key_resumes_the_same_operation_and_issues_no_second_ticket` |
| 6, 7 | `Adding_a_service_writes_one_further_message_with_every_line`, `Adding_a_service_emits_one_further_pricing_change_event`, `The_message_keeps_modern_polarity_and_settlement_semantics`, `Commission_stays_settlement_only_in_the_event`, `The_added_service_detail_beneficiaries_and_coverage_round_trip`, `The_current_total_equals_the_accepted_pricing_line_truth`, `Sibling_events_share_the_commercial_version_and_use_distinct_ordinals` |
| 8 | `A_standalone_document_issues_without_any_ticket_dependency`, `The_association_resolves_through_the_explicit_air_service_relationship`, `The_document_number_is_allocated_before_the_provider_is_called`, `A_retry_reuses_the_same_number_and_provider_operation_key`, `An_unknown_provider_outcome_keeps_the_document_recoverable`, `Issuance_creates_no_new_commercial_truth`, `Document_issuance_adds_no_order_item_service_or_fare_construction` |
| 9 | `An_exact_replay_creates_no_second_price_change_set_and_advances_no_version`, `A_committed_replay_does_not_resolve_the_quote_provider_again`, `A_replayed_change_writes_no_second_message`, `A_replay_after_a_later_unrelated_change_still_resolves_the_original_one` |
| 10 | `A_cross_customer_change_never_enters_the_commercial_workflow`, `Reading_another_customers_order_is_not_found`, `A_cross_customer_read_error_discloses_nothing_about_the_other_order`, `An_unknown_order_and_another_customers_order_are_indistinguishable`, `A_non_owner_cannot_replay_the_owners_idempotency_key` |
| 11 | `A_committed_settlement_only_change_publishes_one_neutral_pricing_event` **(new)**, `A_settlement_only_change_writes_exactly_one_outbox_row` **(new)**, `A_settlement_only_change_still_emits_an_event_with_no_balance_impact`, `Settlement_only_commission_does_not_move_the_customer_total` |
| 12 | `The_current_airprice_source_fabricates_no_fare_construction`, `An_order_without_a_construction_persists_none`, `An_order_without_fare_construction_stays_empty_and_valid`, `Issue_time_fare_context_falls_back_to_the_transitional_service_field_without_a_construction` |
| 13 | `An_explicit_true_round_trip_construction_persists_as_accepted`, `The_same_itinerary_priced_as_two_one_ways_stays_structurally_distinct_from_a_true_round_trip`, `An_open_jaw_construction_is_stored_when_the_source_declares_it`, `A_through_fare_component_covers_several_sold_services_and_segments`, `A_fare_break_produces_separate_components_with_separate_service_scopes`, `Accepting_a_fare_construction_does_not_move_monetary_state`, `Issue_time_fare_context_uses_the_fare_component_when_a_construction_exists` |
| 14 | `A_stale_expected_version_is_rejected_and_changes_nothing`, `A_rejected_change_does_not_leave_the_order_blocked`, `A_rejected_change_writes_no_pricing_message`, `A_rejected_first_line_leaves_the_aggregate_exactly_unchanged` |
| 15 | `A_failed_quote_leaves_no_orphan_pricing_message`, `An_expired_quote_changes_nothing` |
| 16 | `The_ota_api_creation_fails_closed_without_customer_context` **(new)**, `The_ota_panel_creation_fails_closed_without_customer_context` **(new)**, `An_unauthenticated_caller_cannot_create_an_ota_order` **(new)**, `An_authenticated_customer_reaches_the_creation_workflow` **(new)** |

## 3. Cross-cutting gates

| Gate | Result |
|---|---|
| One `OrderPricingChanged` per committed `PriceChangeSet`, never per line/service/tax | Pass — rows 1, 6, 11 |
| No pricing event on reserve, funding, ET issue, EMD issue, recovery, provider confirmation, projection refresh, replay, rejection, failed quote | Pass — rows 3, 4, 5, 8, 9, 14, 15 |
| Event carries the **final** `CommercialVersion`, never `ExpectedCommercialVersion` | Pass — `The_event_carries_the_final_commercial_version_not_the_expected_one` |
| Ordinals: `OrderCreated` 1 / `OrderPricingChanged` 2; `OrderProductAdded` 1 / `OrderPricingChanged` 2 | Pass — `EventOrdinalTests`, `Sibling_events_share_the_commercial_version_and_use_distinct_ordinals` |
| Command state + read model + outbox in one UnitOfWork; no provider IO inside the transaction | Pass — the only `BeginTransaction` in `src/` is inside `OrderingUnitOfWork.SaveChangesAsync`; every provider call happens before it |
| No Ledger call on any P2 path | Pass — zero `Ledger` hits in `src/` |
| No active P2 dependency on a local Payment aggregate | Pass — zero `Payment` references in Creation / Reservation / Issuance / OrderChange / Access |
| Backoffice not customer-restricted | Pass — `The_backoffice_change_is_not_customer_restricted`, `Every_customer_facing_controller_takes_the_access_guard_and_backoffice_does_not` |
| P0 operation fencing intact | Pass — `ClaimFencingTests`, `OperationClaimStoreTests`, `FoundationLifecycleTests`, `CommandReceiptScopeTests`, `PersistenceConstraintTests`, `HomeOperatorIdentityTests`, `OwnerAirlineSourceTests`, `A_conflicting_operation_is_blocked_while_an_irreversible_one_is_unresolved`, `Two_commercial_mutations_cannot_share_one_operation_id` |
