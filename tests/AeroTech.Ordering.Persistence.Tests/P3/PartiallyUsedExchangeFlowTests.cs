using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PartiallyUsedExchangeFlowTests
    {
        private const string SecondQuoteId = "EXC-QUOTE-2";

        private readonly OrderingDatabaseFixture _fixture;

        public PartiallyUsedExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- B. one used coupon, one reissued coupon

        [Fact]
        public async Task B_a_used_coupon_stays_historical_while_the_remaining_open_coupon_is_reissued()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, flownCouponNumbers: [1], changedCouponNumbers: [2]);
            var predecessorBefore = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            var request = Assert.Single(harness.ExchangeQuotes.ObservedQuoteRequests);
            var scopeCoupon = Assert.Single(request.ExchangeScope);
            var historical = Assert.Single(request.HistoricalUsedCoupons);

            Assert.Equal(predecessorBefore.DocumentNumber, request.PredecessorDocumentNumber);
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, predecessorBefore.StatusSummary);
            Assert.Equal(scenario.CouponIds[2], scopeCoupon.PredecessorTicketCouponId);
            Assert.Equal(2, scopeCoupon.CouponNumber);
            Assert.True(scopeCoupon.ServiceIsChanging);
            Assert.Equal(scenario.CouponIds[1], historical.PredecessorTicketCouponId);
            Assert.Equal(1, historical.CouponNumber);
            Assert.Equal(TicketCouponFinancialStatus.Used, historical.FinancialStatus);
            Assert.Equal(scenario.CouponServiceIds[1], historical.CurrentOrderServiceId);
            Assert.Null(historical.CurrentBoundSegment);
            Assert.DoesNotContain(request.ExchangeScope, coupon => coupon.CouponNumber == 1);
            Assert.DoesNotContain(scenario.CouponServiceIds[1], request.ChangedOrderServiceIds);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);
            var exchanged = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 2);
            var successorCoupon = Assert.Single(successor.Coupons);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, exchanged.FinancialStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(exchanged.Id, successorCoupon.PredecessorTicketCouponId);
            Assert.Equal(TicketCouponFinancialStatus.Open, successorCoupon.FinancialStatus);
            Assert.Equal(exchanged.Id, Assert.Single(outcome.Coupons).PredecessorTicketCouponId);
            Assert.Equal(exchanged.Id, Assert.Single(Assert.Single(predecessor.Exchanges).Coupons).PredecessorTicketCouponId);

            var dispatch = Assert.Single(harness.DocumentExchanges.ObservedRequests);

            Assert.Equal([2], dispatch.Coupons.Select(coupon => coupon.PredecessorCouponNumber));
            Assert.Equal([2], Assert.Single(harness.DocumentExchanges.ObservedEligibilityRequests).PredecessorCouponNumbers);
            Assert.Equal(
                scenario.CouponServiceIds[2],
                Assert.Single(Assert.Single(harness.ReservationChanges.ObservedApplies).Items).ReplacedOrderServiceId);
        }

        [Fact]
        public async Task A_partially_used_quote_leaves_no_ordering_visible_trace_however_often_it_is_asked()
        {
            await using var setup = NewHarness();
            var issued = await FlownAsync(_fixture, setup, [1], candidate => candidate.CreateOnwardBoundOrderAsync());
            var before = await ReloadAsync(_fixture, issued.OrderId);
            var ticketsBefore = await TicketsAsync(_fixture, issued.OrderId);
            var operationsBeforeAnyQuote = await ServicingOperationCountAsync(issued.OrderId);

            Assert.Equal(0, await AcceptedPlanCountAsync(issued.OrderId));

            await using var harness = NewHarness();
            var scenario = await QuotedAsync(_fixture, harness, issued, [2]);

            var second = await harness.Exchange.QuoteAsync(scenario.OrderId, scenario.ChangedOrderServiceIds);
            var third = await harness.Exchange.QuoteAsync(scenario.OrderId, scenario.ChangedOrderServiceIds);

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(3, harness.ExchangeQuotes.ObservedQuoteRequests.Count);
            Assert.Equal(second.PredecessorElectronicTicketId, third.PredecessorElectronicTicketId);

            Assert.Equal(operationsBeforeAnyQuote, await ServicingOperationCountAsync(scenario.OrderId));
            Assert.Equal(0, await AcceptedPlanCountAsync(scenario.OrderId));
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.DoesNotContain(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);

            Assert.Empty(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);

            Assert.Equal(ticketsBefore.Count, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.All(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => Assert.Null(candidate.PredecessorElectronicTicketId));
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, predecessor.StatusSummary);
            Assert.Equal(scenario.DocumentVersion, predecessor.DocumentVersion);
            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.All(
                predecessor.Coupons.Where(coupon => coupon.CouponNumber != 1),
                coupon => Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.OrderServices.Count, after.OrderServices.Count);
        }

        // ---------------------------------------------------------------- C. used coupon, one replaced and one continued

        [Fact]
        public async Task C_a_used_coupon_is_excluded_while_one_open_coupon_is_replaced_and_another_is_continued()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(
                _fixture, setup, harness, [1], [2], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var replaced = outcome.Coupons.Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced);
            var continued = outcome.Coupons.Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Continued);
            var usedService = after.OrderServices.Single(service => service.Id == scenario.CouponServiceIds[1]);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, outcome.Coupons.Count);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(3, predecessor.Coupons.Count);
            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 2).FinancialStatus);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 3).FinancialStatus);

            Assert.Equal(2, replaced.PredecessorCouponNumber);
            Assert.Equal(3, continued.PredecessorCouponNumber);
            Assert.Equal(scenario.CouponServiceIds[2], replaced.ReplacedOrderServiceId);
            Assert.Null(continued.ReplacedOrderServiceId);
            Assert.Equal(scenario.CouponServiceIds[3], continued.OrderServiceId);

            Assert.Equal(OrderServiceDocumentStatus.Issued, usedService.DocumentStatus);
            Assert.NotEqual(OrderServiceCommercialStatus.Exchanged, usedService.CommercialStatus);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == scenario.CouponIds[1]);
            Assert.DoesNotContain(
                Assert.Single(predecessor.Exchanges).Coupons,
                coupon => coupon.PredecessorTicketCouponId == scenario.CouponIds[1]);

            Assert.Equal([2, 3], Assert.Single(harness.DocumentExchanges.ObservedRequests).Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.Single(Assert.Single(harness.ReservationChanges.ObservedApplies).Items);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
        }

        // ---------------------------------------------------------------- D. used coupon with two changed coupons

        [Fact]
        public async Task D_two_changed_open_coupons_produce_two_inventory_items_and_two_successor_coupons()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(
                _fixture, setup, harness, [1], [2, 3], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);
            var dispatch = Assert.Single(harness.DocumentExchanges.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, applied.Items.Count);
            Assert.Equal(scenario.ChangedOrderServiceIds, applied.Items.Select(item => item.ReplacedOrderServiceId).Order());
            Assert.Equal(2, dispatch.Coupons.Count);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.All(outcome.Coupons, coupon => Assert.Equal(ExchangeCouponDisposition.Replaced, coupon.Disposition));
            Assert.DoesNotContain(1, dispatch.Coupons.Select(coupon => coupon.PredecessorCouponNumber));
        }

        // ---------------------------------------------------------------- E, F, G. refused before any external work

        [Fact]
        public async Task E_changing_a_used_service_is_refused_before_any_external_work()
        {
            await using var setup = NewHarness();
            var issued = await FlownAsync(_fixture, setup, [1]);
            var used = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .Single(coupon => coupon.CouponNumber == 1);

            await using var harness = NewHarness();
            var refusal = await RefuseAsync(harness, issued, [used.CurrentOrderServiceId]);

            Assert.Equal(20292, refusal.Code);
            await AssertNothingExternalAsync(harness, issued);
        }

        [Fact]
        public async Task F_a_coupon_state_outside_open_and_used_is_refused_before_any_external_work()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var coupons = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .OrderBy(coupon => coupon.CouponNumber)
                .ToList();

            await SetCouponStatusAsync(_fixture, coupons[1].Id, TicketCouponFinancialStatus.Refunded);

            await using var harness = NewHarness();
            var refusal = await RefuseAsync(harness, issued, [coupons[0].CurrentOrderServiceId]);

            Assert.Equal(20270, refusal.Code);
            Assert.Contains("capability", refusal.Message, StringComparison.OrdinalIgnoreCase);
            await AssertNothingExternalAsync(harness, issued);
        }

        [Fact]
        public async Task G_a_fully_used_document_has_nothing_left_to_reissue()
        {
            await using var setup = NewHarness();
            var issued = await FlownAsync(_fixture, setup, [1, 2]);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await using var harness = NewHarness();
            var refusal = await RefuseAsync(harness, issued, [ticket.Coupons.First().CurrentOrderServiceId]);

            Assert.Equal(20292, refusal.Code);
            Assert.Equal(ElectronicTicketStatus.Used, ticket.StatusSummary);
            Assert.All(ticket.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Used, coupon.FinancialStatus));
            await AssertNothingExternalAsync(harness, issued);
        }

        // ---------------------------------------------------------------- H. revalidated then used history

        [Fact]
        public async Task H_a_used_coupon_reports_issue_time_and_current_bound_segment_facts_apart()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var revalidated = await RevalidationFixture.RevalidateAsync(_fixture, setup, issued, couponNumber: 1);
            var used = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(revalidated, used.CurrentOrderServiceId);

            await FlyCouponAsync(_fixture, issued.TicketId, used.Id);

            await using var harness = NewHarness();
            await QuotedAsync(_fixture, harness, issued, [2]);

            var request = Assert.Single(harness.ExchangeQuotes.ObservedQuoteRequests);
            var historical = Assert.Single(request.HistoricalUsedCoupons);
            var scopeCoupon = Assert.Single(request.ExchangeScope);

            Assert.Equal(used.IssuedSegment.FlightNumber, historical.IssuedSegment.FlightNumber);
            Assert.NotNull(historical.CurrentBoundSegment);
            Assert.Equal(RevalidationFixture.ReplacementFlightNumber, historical.CurrentBoundSegment!.FlightNumber);
            Assert.NotEqual(historical.IssuedSegment, historical.CurrentBoundSegment);
            Assert.Equal(revalidated, historical.CurrentOrderServiceId);
            Assert.Equal(scopeCoupon.IssuedSegment, scopeCoupon.CurrentSegment);
        }

        // ---------------------------------------------------------------- I, J. associated miscellaneous documents

        [Fact]
        public async Task I_a_miscellaneous_document_on_a_used_coupon_does_not_block_the_remaining_reissue()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var coupons = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .OrderBy(coupon => coupon.CouponNumber)
                .ToList();

            await AssociateMiscDocumentAsync(setup, issued, coupons[0].Id);
            await FlyCouponAsync(_fixture, issued.TicketId, coupons[0].Id);

            await using var harness = NewHarness();
            var scenario = await QuotedAsync(_fixture, harness, issued, [2]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(coupons[1].Id, Assert.Single(successor.Coupons).PredecessorTicketCouponId);
        }

        [Fact]
        public async Task J_a_miscellaneous_document_on_an_open_scope_coupon_reissues_under_an_explicit_disposition()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var coupons = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .OrderBy(coupon => coupon.CouponNumber)
                .ToList();

            var document = await AssociateMiscDocumentAsync(setup, issued, coupons[1].Id);
            await FlyCouponAsync(_fixture, issued.TicketId, coupons[0].Id);

            await using var harness = NewHarness();
            var scenario = await QuotedAsync(_fixture, harness, issued, [2]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var coupon = (await AncillaryAsync(_fixture, issued.OrderId, document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(Assert.Single(successor.Coupons).Id, coupon.AssociatedTicketCouponId);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task J_a_miscellaneous_document_without_a_disposition_fails_closed_before_the_reissue()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var coupons = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .OrderBy(coupon => coupon.CouponNumber)
                .ToList();

            var document = await AssociateMiscDocumentAsync(setup, issued, coupons[1].Id);
            await FlyCouponAsync(_fixture, issued.TicketId, coupons[0].Id);

            await using var harness = NewHarness();
            var scenario = await QuotedAsync(_fixture, harness, issued, [2]);

            harness.AncillaryDispositions.OmittedCoupons.Add(AncillaryKey(document, 1));

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var after = await ReloadAsync(_fixture, issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupon = (await AncillaryAsync(_fixture, issued.OrderId, document)).Coupons.Single();

            Assert.Equal(20296, refusal.Code);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(coupons[1].Id, coupon.AssociatedTicketCouponId);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.All(
                await TicketsAsync(_fixture, issued.OrderId),
                candidate => Assert.Null(candidate.PredecessorElectronicTicketId));
        }

        // ---------------------------------------------------------------- K, L. accepted scope must equal the reissue scope

        [Theory]
        [InlineData("adds-the-used-coupon")]
        [InlineData("omits-an-open-coupon")]
        public async Task K_L_an_accepted_result_that_misstates_the_reissue_scope_is_refused_before_inventory(string shape)
        {
            await using var setup = NewHarness();
            var issued = await FlownAsync(_fixture, setup, [1], candidate => candidate.CreateOnwardBoundOrderAsync());
            var used = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).Coupons
                .Single(coupon => coupon.CouponNumber == 1);

            await using var harness = NewHarness();
            var scenario = await QuotedAsync(
                _fixture,
                harness,
                issued,
                [2],
                accepted => shape == "adds-the-used-coupon"
                    ? accepted with
                    {
                        Coupons =
                        [
                            .. accepted.Coupons,
                            accepted.Coupons[0] with
                            {
                                PredecessorTicketCouponId = used.Id,
                                PredecessorCouponNumber = used.CouponNumber,
                                PredecessorOrderServiceId = used.CurrentOrderServiceId,
                                Disposition = ExchangeCouponDisposition.Continued,
                                Replacement = null
                            }
                        ]
                    }
                    : accepted with { Coupons = [accepted.Coupons[0]] });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20273, refusal.Code);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
        }

        // ---------------------------------------------------------------- M. a host mapping for a used coupon is invalid evidence

        [Fact]
        public async Task M_a_host_mapping_that_names_the_used_coupon_needs_reconciliation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(
                _fixture, setup, harness, [1], [2], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            harness.DocumentExchanges.UnknownPredecessorCouponNumber = 1;

            var key = NewKey();
            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(outcome.OperationId, replay.OperationId);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Null(replay.SuccessorElectronicTicketId);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, predecessor.StatusSummary);
            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.All(
                predecessor.Coupons.Where(coupon => coupon.CouponNumber != 1),
                coupon => Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);

            Assert.Contains(1, plan.Successor!.Coupons.Select(coupon => coupon.PredecessorCouponNumber));
            Assert.DoesNotContain(
                plan.Coupons.Select(coupon => coupon.PredecessorCouponNumber),
                asked => asked == 1);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- N. unresolved inventory holds the operation

        [Fact]
        public async Task N_an_unresolved_reservation_change_makes_no_document_call_and_holds_the_claim()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, [1], [2]);

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Unknown, plan.ReservationOutcome);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, predecessor.StatusSummary);
        }

        [Fact]
        public async Task N_an_unresolved_reservation_change_is_read_back_on_replay_and_never_applied_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(
                _fixture, setup, harness, [1], [2], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);
            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.NotEmpty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.All(
                harness.ReservationChanges.ObservedRecoveryKeys,
                recovered => Assert.Equal(applied.OperationKey, recovered));
            Assert.Single(applied.Items);
            Assert.Equal(scenario.CouponServiceIds[2], Assert.Single(applied.Items).ReplacedOrderServiceId);

            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Contains(plan.ReservationOutcome, new[] { ProviderOperationOutcome.Pending, ProviderOperationOutcome.Unknown });
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.All(
                predecessor.Coupons.Where(coupon => coupon.CouponNumber != 1),
                coupon => Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, predecessor.StatusSummary);
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.All(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => Assert.Null(candidate.PredecessorElectronicTicketId));
        }

        // ---------------------------------------------------------------- O. a document confirmation survives a crash

        [Fact]
        public async Task O_a_document_exchange_that_crashes_after_dispatch_recovers_without_a_second_dispatch()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, [1], [2]);
            var key = NewKey();

            harness.DocumentExchanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.DocumentExchanges.ThrowAfterDispatch = false;
            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, recovered.SuccessorElectronicTicketId!.Value))!;

            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(scenario.CouponIds[2], Assert.Single(successor.Coupons).PredecessorTicketCouponId);
        }

        [Fact]
        public async Task O_a_durable_document_confirmation_finalizes_in_a_fresh_process_with_no_provider_call()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-partial-{Guid.NewGuid():N}");

            await using var setup = NewHarness();
            await using var crashed = NewHarness(caller);
            var scenario = await PartiallyUsedAsync(
                _fixture, setup, crashed, [1], [2], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());
            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var key = NewKey();

            crashed.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;

            var unresolved = await crashed.Exchange.ExchangeAsync(scenario.Execution(key));
            var staged = (await crashed.ExchangePlans.FindAsync(unresolved.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, unresolved.OperationStatus);
            Assert.True(staged.IsEligibilityEstablished);
            Assert.True(staged.IsReservationConfirmed);
            Assert.Equal([2, 3], staged.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());

            await crashed.ExchangePlans.RecordDocumentExchangeOutcomeAsync(
                unresolved.OperationId,
                ProviderOperationOutcome.Confirmed,
                "EXCH-PARTIAL-DURABLE",
                new SuccessorDocumentIdentity(
                    $"EXC{unresolved.OperationId}",
                    1,
                    null,
                    DocumentAuthority.Local,
                    null,
                    staged.Coupons
                        .OrderBy(coupon => coupon.PredecessorCouponNumber)
                        .Select((coupon, index) => new SuccessorCouponIdentity(coupon.PredecessorCouponNumber, index + 1))
                        .ToList()),
                null);
            await crashed.UnitOfWork.SaveChangesAsync();

            var stillUnfinalized = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Empty(stillUnfinalized.Exchanges);
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, stillUnfinalized.StatusSummary);
            Assert.Equal(before.CommercialVersion, (await ReloadAsync(_fixture, scenario.OrderId)).CommercialVersion);

            await using var resumed = NewHarness(caller);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Empty(resumed.ReservationChanges.ObservedApplies);
            Assert.Empty(resumed.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Empty(resumed.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Empty(resumed.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(resumed.ExchangeQuotes.ObservedSelections);

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, finalized.SuccessorElectronicTicketId!.Value))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var tickets = await TicketsAsync(_fixture, scenario.OrderId);
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(unresolved.OperationId, finalized.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal("EXCH-PARTIAL-DURABLE", finalized.ProviderExchangeReference);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(tickets, candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(3, tickets.Count);

            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.All(
                predecessor.Coupons.Where(coupon => coupon.CouponNumber != 1),
                coupon => Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));
            Assert.Equal([2, 3], successor.Coupons.Select(coupon => coupon.PredecessorTicketCouponId!.Value)
                .Select(couponId => predecessor.Coupons.Single(candidate => candidate.Id == couponId).CouponNumber)
                .Order());
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.DoesNotContain(
                Assert.Single(predecessor.Exchanges).Coupons,
                coupon => coupon.PredecessorTicketCouponId == used.Id);

            Assert.Equal(before.OrderServices.Count + 1, after.OrderServices.Count);
            Assert.Equal(after.OrderServices.Count, after.OrderServices.Select(service => service.Id).Distinct().Count());
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.FinancialSequence + 1, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(
                after.FinancialSequence,
                Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange).FinancialSequence);

            var again = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));
            var settled = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(finalized.OperationId, again.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, again.OperationStatus);
            Assert.True(again.IsReplay);
            Assert.Empty(resumed.ReservationChanges.ObservedApplies);
            Assert.Empty(resumed.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Empty(resumed.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(settled.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(after.CommercialVersion, settled.CommercialVersion);
        }

        // ---------------------------------------------------------------- P. repeated reissue after partial use

        [Fact]
        public async Task P_a_partially_used_document_supports_repeated_reissue_without_touching_used_history()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(
                _fixture, setup, harness, [1], [2], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var successorId = first.SuccessorElectronicTicketId!.Value;
            var continuedService = first.Coupons
                .Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Continued)
                .OrderServiceId;

            var reloaded = await ReloadAsync(_fixture, scenario.OrderId);
            IReadOnlyList<long> secondChanged = [continuedService];

            Compose(harness, reloaded, secondChanged, SecondQuoteId);

            await harness.Exchange.QuoteAsync(scenario.OrderId, secondChanged);

            var second = await harness.Exchange.ExchangeAsync(new ExchangeExecution(
                scenario.OrderId, secondChanged, SecondQuoteId, NewKey(), reloaded.CommercialVersion));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reissued = (await FindTicketAsync(_fixture, second.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.Equal(successorId, reissued.PredecessorElectronicTicketId);
            Assert.Equal(scenario.TicketId, successor.PredecessorElectronicTicketId);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(successor.Exchanges);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(scenario.CouponServiceIds[1], used.CurrentOrderServiceId);
            Assert.Equal(2, reissued.Coupons.Count);
            Assert.All(
                reissued.Coupons,
                coupon => Assert.Contains(coupon.PredecessorTicketCouponId!.Value, successor.Coupons.Select(candidate => candidate.Id)));
            Assert.DoesNotContain(
                reissued.Coupons,
                coupon => predecessor.Coupons.Select(candidate => candidate.Id).Contains(coupon.PredecessorTicketCouponId!.Value));
            Assert.DoesNotContain(
                Assert.Single(successor.Exchanges).Coupons,
                coupon => coupon.PredecessorTicketCouponId == used.Id);
        }

        // ---------------------------------------------------------------- Q. stale commercial version

        [Fact]
        public async Task Q_a_stale_expected_version_fails_before_acceptance()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, [1], [2]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(
                scenario.Execution(NewKey()) with { ExpectedCommercialVersion = scenario.CommercialVersion + 5 }));

            Assert.Equal(20089, refusal.Code);
            Assert.Empty(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
            Assert.Equal(scenario.CommercialVersion, (await ReloadAsync(_fixture, scenario.OrderId)).CommercialVersion);
        }

        // ---------------------------------------------------------------- support

        private async Task<BusinessException> RefuseAsync(
            OrderSliceHarness harness,
            IssuedTicket issued,
            IReadOnlyList<long> changedOrderServiceIds)
        {
            var order = await ReloadAsync(_fixture, issued.OrderId);

            Compose(harness, order, changedOrderServiceIds);

            return await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(new ExchangeExecution(
                issued.OrderId,
                changedOrderServiceIds,
                ExchangeSourceFactory.QuoteId,
                NewKey(),
                order.CommercialVersion)));
        }

        private async Task<string> AssociateMiscDocumentAsync(
            OrderSliceHarness harness,
            IssuedTicket issued,
            long ticketCouponId)
        {
            var order = await ReloadAsync(_fixture, issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var documentNumber = $"M{harness.Ids.NewId() % 1_000_000:D6}";

            await harness.MiscDocumentRepository.AddAsync(ElectronicMiscDocument.Issue(
                harness.Ids.NewId(),
                order.Id,
                ticket.TravelerId,
                harness.Ids.NewId(),
                documentNumber,
                ElectronicMiscDocumentType.Associated,
                "A",
                OrderSliceHarness.HomeAirlineId,
                null,
                DocumentAuthority.Local,
                order.CurrencyId,
                [
                    new EmdCouponIssuance(
                        EmdCouponPurpose.Fee,
                        "0DF",
                        50_000m,
                        [],
                        PricingLineId: order.PricingLines.First().Id,
                        AssociatedTicketCouponId: ticketCouponId)
                ],
                harness.Ids,
                harness.Clock));

            await harness.UnitOfWork.SaveChangesAsync();

            return documentNumber;
        }

        private async Task<int> ServicingOperationCountAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Set<ServicingOperation>().CountAsync(operation => operation.OrderId == orderId);
        }

        private async Task<int> AcceptedPlanCountAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Set<AcceptedExchangePlanRow>().CountAsync(plan => plan.OrderId == orderId);
        }

        private async Task AssertNothingExternalAsync(OrderSliceHarness harness, IssuedTicket issued)
        {
            var after = await ReloadAsync(_fixture, issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Empty(harness.ExchangeQuotes.ObservedQuoteRequests);
            Assert.Empty(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.All(
                await TicketsAsync(_fixture, issued.OrderId),
                candidate => Assert.Null(candidate.PredecessorElectronicTicketId));
        }

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AirlineUser(7401, $"exc-partial-{Guid.NewGuid():N}"));
    }
}
