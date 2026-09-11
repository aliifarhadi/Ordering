using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class MultiCouponExchangeFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public MultiCouponExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- 1, 3, 4, 5, 7. first coupon changed, second continued

        [Fact]
        public async Task A_two_coupon_ticket_with_one_changed_coupon_reissues_the_complete_document_and_keeps_the_continued_service()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);
            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var continuedId = scenario.CouponServiceIds[2];
            var continuedBefore = before.OrderServices.Single(service => service.Id == continuedId);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var replacedOutcome = outcome.Coupons.Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced);
            var continuedOutcome = outcome.Coupons.Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Continued);
            var continued = after.OrderServices.Single(service => service.Id == continuedId);
            var replaced = after.OrderServices.Single(service => service.Id == scenario.ServiceId);
            var replacement = after.OrderServices.Single(service => service.Id == replacedOutcome.OrderServiceId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, outcome.Coupons.Count);
            Assert.Equal(before.OrderServices.Count + 1, after.OrderServices.Count);
            Assert.Equal(before.Segments.Count + 1, after.Segments.Count);

            Assert.Equal(continuedId, continuedOutcome.OrderServiceId);
            Assert.Null(continuedOutcome.ReplacedOrderServiceId);
            Assert.Equal(continuedBefore.Status, continued.Status);
            Assert.Equal(continuedBefore.SoldSegmentId, continued.SoldSegmentId);
            Assert.Equal(OrderServiceDocumentStatus.Issued, continued.DocumentStatus);
            Assert.NotEqual(OrderServiceCommercialStatus.Exchanged, continued.CommercialStatus);
            Assert.Equal(successor.Id, continued.ElectronicTicketId);
            Assert.Equal(continuedOutcome.SuccessorTicketCouponId, continued.TicketCouponId);

            Assert.Equal(scenario.ServiceId, replacedOutcome.ReplacedOrderServiceId);
            Assert.Equal(OrderServiceDocumentStatus.Exchanged, replaced.DocumentStatus);
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, replaced.CommercialStatus);
            Assert.Equal(scenario.TicketId, replaced.ElectronicTicketId);
            Assert.Equal(successor.Id, replacement.ElectronicTicketId);
            Assert.Equal(replacedOutcome.SuccessorTicketCouponId, replacement.TicketCouponId);
            Assert.Equal(ExchangeSourceFactory.ReplacementFlightNumber, after.Segments.Single(segment => segment.Id == replacement.SoldSegmentId).Number);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(scenario.DocumentVersion + 1, predecessor.DocumentVersion);
            Assert.Equal(2, predecessor.Coupons.Count);
            Assert.All(predecessor.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));

            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(ElectronicTicketStatus.Issued, successor.StatusSummary);
            Assert.Equal(1, successor.DocumentVersion);
            Assert.Equal(new[] { 1, 2 }, successor.Coupons.Select(coupon => coupon.CouponNumber).Order());

            foreach (var couponOutcome in outcome.Coupons)
            {
                var successorCoupon = Assert.Single(successor.Coupons, coupon => coupon.Id == couponOutcome.SuccessorTicketCouponId);
                var predecessorCoupon = predecessor.Coupons.Single(coupon => coupon.Id == couponOutcome.PredecessorTicketCouponId);
                var mapping = Assert.Single(Assert.Single(predecessor.Exchanges).Coupons, coupon => coupon.PredecessorTicketCouponId == couponOutcome.PredecessorTicketCouponId);

                Assert.Equal(couponOutcome.PredecessorTicketCouponId, successorCoupon.PredecessorTicketCouponId);
                Assert.Equal(couponOutcome.SuccessorCouponNumber, successorCoupon.CouponNumber);
                Assert.Equal(predecessorCoupon.CouponNumber, successorCoupon.CouponNumber);
                Assert.Equal(couponOutcome.OrderServiceId, successorCoupon.OrderServiceId);
                Assert.Equal(couponOutcome.OrderServiceId, successorCoupon.CurrentOrderServiceId);
                Assert.Equal(TicketCouponFinancialStatus.Open, successorCoupon.FinancialStatus);
                Assert.Equal(couponOutcome.SuccessorTicketCouponId, mapping.SuccessorTicketCouponId);
                Assert.Equal(successorCoupon.CouponNumber, mapping.SuccessorCouponNumber);
                Assert.Equal(predecessorCoupon.CurrentOrderServiceId, mapping.PreviousOrderServiceId);
                Assert.Equal(couponOutcome.OrderServiceId, mapping.SuccessorOrderServiceId);
            }

            var continuedSuccessorCoupon = successor.Coupons.Single(coupon => coupon.Id == continuedOutcome.SuccessorTicketCouponId);

            Assert.Equal(continuedBefore.SoldSegmentId, continuedSuccessorCoupon.JourneySegmentId);
            Assert.Equal(predecessor.Coupons.Single(coupon => coupon.CouponNumber == 2).IssuedSegment.FlightNumber, continuedSuccessorCoupon.IssuedSegment.FlightNumber);
        }

        [Fact]
        public async Task Inventory_receives_one_plan_level_change_with_only_the_replaced_service_and_the_host_receives_every_coupon()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);
            var item = Assert.Single(applied.Items);
            var exchanged = Assert.Single(harness.DocumentExchanges.ObservedRequests);

            Assert.Equal(scenario.ServiceId, item.ReplacedOrderServiceId);
            Assert.Equal(outcome.Coupons.Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced).OrderServiceId, item.ReplacementOrderServiceId);
            Assert.Equal(1, Assert.Single(harness.ReservationChanges.DispatchedKeys).Split(':').Count(part => part == "exchange-reservation"));
            Assert.Equal(2, exchanged.Coupons.Count);
            Assert.Single(exchanged.Coupons, coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced);
            Assert.Single(exchanged.Coupons, coupon => coupon.Disposition == ExchangeCouponDisposition.Continued && coupon.PredecessorCouponNumber == 2);
            Assert.Equal(new[] { 1, 2 }, Assert.Single(harness.DocumentExchanges.ObservedEligibilityRequests).PredecessorCouponNumbers.Order());
        }

        // ---------------------------------------------------------------- 2, 6. both coupons changed

        [Fact]
        public async Task A_two_coupon_ticket_with_both_coupons_changed_supersedes_each_service_exactly_once()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1, 2]);
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, outcome.Coupons.Count);
            Assert.All(outcome.Coupons, coupon => Assert.Equal(ExchangeCouponDisposition.Replaced, coupon.Disposition));
            Assert.Equal(before.OrderServices.Count + 2, after.OrderServices.Count);
            Assert.Equal(2, applied.Items.Count);
            Assert.Equal(scenario.ChangedOrderServiceIds, applied.Items.Select(item => item.ReplacedOrderServiceId).Order());
            Assert.Equal(2, after.OrderServices.Count(service => service.DocumentStatus == OrderServiceDocumentStatus.Exchanged));
            Assert.Equal(2, after.OrderServices.Count(service => service.ElectronicTicketId == successor.Id));
            Assert.Equal(2, successor.Coupons.Count);

            foreach (var couponOutcome in outcome.Coupons)
            {
                var replacement = after.OrderServices.Single(service => service.Id == couponOutcome.OrderServiceId);
                var replaced = after.OrderServices.Single(service => service.Id == couponOutcome.ReplacedOrderServiceId);
                var successorCoupon = successor.Coupons.Single(coupon => coupon.Id == couponOutcome.SuccessorTicketCouponId);

                Assert.Equal(OrderServiceDocumentStatus.Exchanged, replaced.DocumentStatus);
                Assert.Equal(OrderServiceDocumentStatus.Issued, replacement.DocumentStatus);
                Assert.Equal(successorCoupon.Id, replacement.TicketCouponId);
                Assert.Equal(replacement.Id, successorCoupon.OrderServiceId);
                Assert.Equal(couponOutcome.PredecessorTicketCouponId, successorCoupon.PredecessorTicketCouponId);
            }

            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence + 1, after.FinancialSequence);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
        }

        [Fact]
        public async Task Successor_price_links_attribute_each_coupon_to_its_own_transfer_lines()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var exchangeLines = after.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();

            Assert.Equal(scenario.Accepted.Coupons.Sum(coupon => coupon.Successor.PriceLinks.Count), successor.PriceLinks.Count);
            Assert.Equal(scenario.Accepted.Coupons.Sum(coupon => coupon.Successor.IssuanceValue), successor.IssuedTotal);
            Assert.Equal(0m, exchangeLines.Sum(line => line.SignedSaleAmount));
            Assert.All(exchangeLines, line => Assert.Contains(line.OriginalPricingLineId!.Value, predecessor.CarriedPricingLineIds()));

            foreach (var accepted in scenario.Accepted.Coupons)
            {
                var couponOutcome = outcome.Coupons.Single(coupon => coupon.PredecessorTicketCouponId == accepted.PredecessorTicketCouponId);
                var successorCoupon = successor.Coupons.Single(coupon => coupon.Id == couponOutcome.SuccessorTicketCouponId);

                Assert.Equal(accepted.Successor.IssuanceValue, successorCoupon.IssuanceValue);

                foreach (var attribution in accepted.Successor.PriceLinks)
                {
                    var line = Assert.Single(exchangeLines, candidate => candidate.SourceLineRef == attribution.SourceLineRef);
                    var link = Assert.Single(successor.PriceLinks, candidate => candidate.PricingLineId == line.Id);

                    Assert.Equal(successorCoupon.Id, link.CouponId);
                    Assert.Equal(attribution.AttributedValue, link.AttributedValue);
                    Assert.Null(link.AllocationId);
                }
            }
        }

        // ---------------------------------------------------------------- 8, 9, 19. deterministic refusals

        [Theory]
        [InlineData("missing")]
        [InlineData("duplicate")]
        public async Task An_incomplete_or_duplicated_coupon_disposition_fails_before_inventory(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1], shapeAccepted: accepted => accepted with
            {
                Coupons = shape == "missing" ? [accepted.Coupons[0]] : [accepted.Coupons[0], accepted.Coupons[0]]
            });

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20273, refusal.Code);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            await AssertNoLocalExchangeAsync(harness, scenario);
        }

        [Fact]
        public async Task An_unsupported_monetary_outcome_on_a_multi_coupon_ticket_is_deferred_without_partial_execution()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1, 2],
                shapeAccepted: accepted => accepted with { MonetaryOutcome = ChangeMonetaryOutcome.Refund });

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.True(outcome.DeferredToExpandedExchange);
            Assert.Equal(nameof(ChangeMonetaryOutcome.Refund), outcome.DeferralReason);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            await AssertNoLocalExchangeAsync(harness, scenario);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- 12–17. recovery on a multi-coupon plan

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_multi_item_reservation_recovers_under_one_key_without_a_second_apply(ProviderOperationOutcome applyOutcome)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1, 2]);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = applyOutcome;

            var pending = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, pending.OperationStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Equal(2, harness.ReservationChanges.ObservedApplies.Single().Items.Count);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal(2, recovered.Coupons.Count);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_document_exchange_recovers_the_complete_coupon_scope_without_a_second_dispatch(ProviderOperationOutcome exchangeOutcome)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = exchangeOutcome;

            var pending = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, pending.OperationStatus);
            await AssertNoLocalExchangeAsync(harness, scenario);

            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var successor = (await FindTicketAsync(_fixture, recovered.SuccessorElectronicTicketId!.Value))!;

            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(new[] { 1, 2 }, successor.Coupons.Select(coupon => coupon.CouponNumber).Order());
        }

        [Fact]
        public async Task A_crash_after_inventory_confirmation_never_reapplies_and_recovers_the_document_first()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1, 2]);
            var key = NewKey();

            harness.DocumentExchanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            var plan = (await harness.ExchangePlans.FindAsync(harness.ExchangeQuotes.ObservedSelections.Single().OperationId))!;

            Assert.True(plan.IsReservationConfirmed);
            Assert.Equal(2, plan.ReplacedCoupons.Count);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.DocumentExchanges.ThrowBeforeDispatch = false;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
        }

        [Fact]
        public async Task A_crash_after_document_dispatch_never_redispatches_the_reissue()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);
            var key = NewKey();

            harness.DocumentExchanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.DocumentExchanges.ThrowAfterDispatch = false;
            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var successor = (await FindTicketAsync(_fixture, recovered.SuccessorElectronicTicketId!.Value))!;

            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
        }

        [Fact]
        public async Task A_crash_after_durable_document_confirmation_finalizes_every_coupon_from_evidence_with_zero_external_calls()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-multi-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await TicketedAsync(_fixture, setup, roundTrip: true, changedCouponNumbers: [1]);
            var key = NewKey();

            setup.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;

            var first = await setup.Exchange.ExchangeAsync(scenario.Execution(key));

            await setup.ExchangePlans.RecordDocumentExchangeOutcomeAsync(
                first.OperationId,
                ProviderOperationOutcome.Confirmed,
                "EXCH-RECOVERED",
                new SuccessorDocumentIdentity(
                    $"EXC{first.OperationId}", 1, null, DocumentAuthority.Local, null,
                    [new SuccessorCouponIdentity(1, 1), new SuccessorCouponIdentity(2, 2)]),
                null);
            await setup.UnitOfWork.SaveChangesAsync();

            await AssertNoLocalExchangeAsync(setup, scenario);

            await using var resume = NewHarness(caller);
            Register(resume, scenario);

            var finalized = await resume.Exchange.ExchangeAsync(scenario.Execution(key));
            var successor = (await FindTicketAsync(_fixture, finalized.SuccessorElectronicTicketId!.Value))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Empty(resume.ExchangeQuotes.ObservedSelections);
            Assert.Empty(resume.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(resume.ReservationChanges.ObservedApplies);
            Assert.Empty(resume.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resume.DocumentExchanges.ObservedRequests);
            Assert.Empty(resume.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(2, Assert.Single(predecessor.Exchanges).Coupons.Count);
            Assert.All(predecessor.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(resume, scenario));
        }

        [Fact]
        public async Task A_completed_multi_coupon_replay_creates_no_additional_ticket_coupon_service_pricing_or_change()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1, 2]);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var settled = await ReloadAsync(_fixture, scenario.OrderId);
            var ticketsSettled = (await TicketsAsync(_fixture, scenario.OrderId)).Count;
            var couponsSettled = (await TicketsAsync(_fixture, scenario.OrderId)).Sum(ticket => ticket.Coupons.Count);
            var externalCalls = harness.ExchangeQuotes.ObservedSelections.Count
                                + harness.DocumentExchanges.ObservedEligibilityRequests.Count
                                + harness.ReservationChanges.ObservedApplies.Count
                                + harness.DocumentExchanges.ObservedRequests.Count;

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.Coupons, replay.Coupons);
            Assert.Equal(externalCalls,
                harness.ExchangeQuotes.ObservedSelections.Count
                + harness.DocumentExchanges.ObservedEligibilityRequests.Count
                + harness.ReservationChanges.ObservedApplies.Count
                + harness.DocumentExchanges.ObservedRequests.Count);
            Assert.Equal(ticketsSettled, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(couponsSettled, (await TicketsAsync(_fixture, scenario.OrderId)).Sum(ticket => ticket.Coupons.Count));
            Assert.Equal(settled.OrderServices.Count, after.OrderServices.Count);
            Assert.Equal(settled.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(settled.Changes.Count, after.Changes.Count);
            Assert.Equal(settled.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Equal(settled.CommercialVersion, after.CommercialVersion);
            Assert.Equal(settled.FinancialSequence, after.FinancialSequence);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task A_commercial_version_mismatch_on_a_multi_coupon_resume_fails_closed()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-multi-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await TicketedAsync(_fixture, setup, roundTrip: true, changedCouponNumbers: [1, 2]);
            var key = NewKey();

            setup.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await setup.Exchange.ExchangeAsync(scenario.Execution(key));

            await using (var command = _fixture.NewCommandContext())
                await command.Database.ExecuteSqlRawAsync(
                    "UPDATE [Order].[Orders] SET [CommercialVersion] = [CommercialVersion] + 1 WHERE [Id] = {0}", scenario.OrderId);

            await using var resume = NewHarness(caller);
            Register(resume, scenario);

            var resumed = await resume.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, resumed.OperationStatus);
            Assert.Empty(resume.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resume.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(resume, scenario));
        }

        // ---------------------------------------------------------------- support

        private async Task AssertNoLocalExchangeAsync(OrderSliceHarness harness, ExchangeScenario scenario)
        {
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Issued, predecessor.StatusSummary);
            Assert.All(predecessor.Coupons, coupon => Assert.NotEqual(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));
            Assert.Equal(2, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AirlineUser(7401, $"exc-multi-{Guid.NewGuid():N}"));
    }
}
