using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class AccountableDocumentResolutionTests
    {
        private const string SecondQuoteId = "EXC-QUOTE-2";

        private readonly OrderingDatabaseFixture _fixture;

        public AccountableDocumentResolutionTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_service_continued_by_the_first_reissue_can_be_reissued_again_through_the_successor_document()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);
            var firstChangedService = scenario.CouponServiceIds[1];
            var continuedService = scenario.CouponServiceIds[2];

            // ---------------------------------------------------------------- A -> B

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, first.SuccessorElectronicTicketId!.Value))!;
            var predecessorFacts = Facts(predecessor);
            var replacementOfFirstService = first.Coupons
                .Single(coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced).OrderServiceId;
            var continuedCouponOfA = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 2);
            var continuedCouponOfB = successor.Coupons.Single(coupon => coupon.PredecessorTicketCouponId == continuedCouponOfA.Id);

            Assert.Equal(ServicingOperationStatus.Completed, first.OperationStatus);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, continuedCouponOfA.FinancialStatus);
            Assert.Equal(continuedService, continuedCouponOfA.CurrentOrderServiceId);
            Assert.Equal(continuedService, continuedCouponOfB.CurrentOrderServiceId);
            Assert.Equal(TicketCouponFinancialStatus.Open, continuedCouponOfB.FinancialStatus);

            var applies = harness.ReservationChanges.ObservedApplies.Count;
            var dispatches = harness.DocumentExchanges.ObservedRequests.Count;
            var acceptances = harness.ExchangeQuotes.ObservedSelections.Count;

            // ---------------------------------------------------------------- B -> C, changing the continued service

            var order = await ReloadAsync(_fixture, scenario.OrderId);
            IReadOnlyList<long> changed = [continuedService];

            harness.ExchangeQuotes.Composer = request => ExchangeSourceFactory.Compose(
                request, ExchangeSourceFactory.ReplacementsFor(order, changed), quotedExchangeId: SecondQuoteId);

            var quote = await harness.Exchange.QuoteAsync(scenario.OrderId, changed);

            Assert.Equal(successor.Id, quote.PredecessorElectronicTicketId);
            Assert.Equal(successor.DocumentNumber, quote.PredecessorDocumentNumber);
            Assert.NotEqual(predecessor.Id, quote.PredecessorElectronicTicketId);
            Assert.Equal(
                ExchangeCouponDisposition.Replaced,
                quote.Coupons.Single(coupon => coupon.PredecessorOrderServiceId == continuedService).Disposition);
            Assert.Equal(
                ExchangeCouponDisposition.Continued,
                quote.Coupons.Single(coupon => coupon.PredecessorOrderServiceId == replacementOfFirstService).Disposition);

            var execution = new ExchangeExecution(scenario.OrderId, changed, SecondQuoteId, NewKey(), order.CommercialVersion);
            var second = await harness.Exchange.ExchangeAsync(execution);

            var settledPredecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var settledSuccessor = await TicketAsync(_fixture, scenario.OrderId, successor.Id);
            var reissued = (await FindTicketAsync(_fixture, second.SuccessorElectronicTicketId!.Value))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.NotEqual(first.OperationId, second.OperationId);

            // ---------------------------------------------------------------- document lineage

            Assert.Equal(predecessor.Id, settledSuccessor.PredecessorElectronicTicketId);
            Assert.Equal(successor.Id, reissued.PredecessorElectronicTicketId);
            Assert.Equal(ElectronicTicketStatus.Exchanged, settledPredecessor.StatusSummary);
            Assert.Equal(ElectronicTicketStatus.Exchanged, settledSuccessor.StatusSummary);
            Assert.Equal(ElectronicTicketStatus.Issued, reissued.StatusSummary);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count(ticket => ticket.TravelerId == predecessor.TravelerId));

            var recordOnA = Assert.Single(settledPredecessor.Exchanges);
            var recordOnB = Assert.Single(settledSuccessor.Exchanges);

            Assert.Equal(successor.Id, recordOnA.SuccessorElectronicTicketId);
            Assert.Equal(reissued.Id, recordOnB.SuccessorElectronicTicketId);
            Assert.Equal(first.OperationId, recordOnA.OperationId);
            Assert.Equal(second.OperationId, recordOnB.OperationId);

            // ---------------------------------------------------------------- coupon lineage is immediate

            var predecessorCouponIds = settledPredecessor.Coupons.Select(coupon => coupon.Id).ToList();
            var successorCouponIds = settledSuccessor.Coupons.Select(coupon => coupon.Id).ToList();

            Assert.Equal(2, reissued.Coupons.Count);
            Assert.All(reissued.Coupons, coupon => Assert.Contains(coupon.PredecessorTicketCouponId!.Value, successorCouponIds));
            Assert.All(reissued.Coupons, coupon => Assert.DoesNotContain(coupon.PredecessorTicketCouponId!.Value, predecessorCouponIds));

            var reissuedFromContinued = Assert.Single(
                reissued.Coupons, coupon => coupon.PredecessorTicketCouponId == continuedCouponOfB.Id);

            Assert.Equal(continuedCouponOfA.Id, continuedCouponOfB.PredecessorTicketCouponId);
            Assert.Equal(continuedCouponOfB.Id, reissuedFromContinued.PredecessorTicketCouponId);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, settledSuccessor.Coupons.Single(coupon => coupon.Id == continuedCouponOfB.Id).FinancialStatus);
            Assert.All(settledSuccessor.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));

            // ---------------------------------------------------------------- A is historical truth

            Assert.Equal(predecessorFacts, Facts(settledPredecessor));

            // ---------------------------------------------------------------- provider boundaries of the second operation

            var acceptance = harness.ExchangeQuotes.ObservedSelections[^1];
            var dispatch = harness.DocumentExchanges.ObservedRequests[^1];
            var applied = harness.ReservationChanges.ObservedApplies[^1];

            Assert.Equal(acceptances + 1, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Equal(dispatches + 1, harness.DocumentExchanges.ObservedRequests.Count);
            Assert.Equal(applies + 1, harness.ReservationChanges.ObservedApplies.Count);

            Assert.Equal(successor.Id, acceptance.PredecessorElectronicTicketId);
            Assert.Equal(changed, acceptance.ChangedOrderServiceIds);
            Assert.Equal(successor.DocumentNumber, dispatch.PredecessorDocumentNumber);
            Assert.NotEqual(predecessor.DocumentNumber, dispatch.PredecessorDocumentNumber);
            Assert.Equal(
                settledSuccessor.Coupons.Select(coupon => coupon.CouponNumber).Order(),
                dispatch.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());

            var item = Assert.Single(applied.Items);

            Assert.Equal(continuedService, item.ReplacedOrderServiceId);
            Assert.DoesNotContain(replacementOfFirstService, applied.Items.Select(candidate => candidate.ReplacedOrderServiceId));

            // ---------------------------------------------------------------- commercial history

            var changes = after.Changes.Where(change => change.ChangeType == OrderChangeType.Exchange).ToList();
            var sets = after.PriceChangeSets.Where(set => set.Reason == PriceChangeReason.Exchange).ToList();

            Assert.Equal(2, changes.Count);
            Assert.Equal(2, sets.Count);
            Assert.Equal(new[] { first.OperationId, second.OperationId }.Order(), changes.Select(change => change.OperationId!.Value).Order());
            Assert.Equal(changes.Select(change => change.Id).Order(), sets.Select(set => set.ChangeId).Order());
            Assert.Equal(scenario.CommercialVersion + 2, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence + 2, after.FinancialSequence);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);

            // ---------------------------------------------------------------- replay of the second operation

            var ticketsAfterSecond = (await TicketsAsync(_fixture, scenario.OrderId)).Count;
            var replay = await harness.Exchange.ExchangeAsync(execution);
            var replayed = await ReloadAsync(_fixture, scenario.OrderId);
            var replayedReissued = await TicketAsync(_fixture, scenario.OrderId, reissued.Id);

            Assert.True(replay.IsReplay);
            Assert.Equal(second.OperationId, replay.OperationId);
            Assert.Equal(reissued.Id, replay.SuccessorElectronicTicketId);
            Assert.Equal(second.Coupons, replay.Coupons);
            Assert.Equal(ticketsAfterSecond, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(acceptances + 1, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Equal(dispatches + 1, harness.DocumentExchanges.ObservedRequests.Count);
            Assert.Equal(applies + 1, harness.ReservationChanges.ObservedApplies.Count);
            Assert.Single((await TicketAsync(_fixture, scenario.OrderId, successor.Id)).Exchanges);
            Assert.Single(settledPredecessor.Exchanges);
            Assert.Equal(2, replayed.Changes.Count(change => change.ChangeType == OrderChangeType.Exchange));
            Assert.Equal(2, replayed.PriceChangeSets.Count(set => set.Reason == PriceChangeReason.Exchange));
            Assert.Equal(after.CommercialVersion, replayed.CommercialVersion);
            Assert.Equal(after.FinancialSequence, replayed.FinancialSequence);
            Assert.Equal(reissued.DocumentVersion, replayedReissued.DocumentVersion);
            Assert.Equal(predecessorFacts, Facts(await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)));
        }

        [Fact]
        public async Task Two_currently_serviceable_documents_covering_one_service_remain_ambiguous()
        {
            await using var setup = NewHarness();
            var scenario = await TicketedAsync(_fixture, setup);
            var unrelated = (await TicketsAsync(_fixture, scenario.OrderId)).Single(ticket => ticket.Id != scenario.TicketId);
            var unrelatedCoupon = unrelated.Coupons.Single();

            await using (var command = _fixture.NewCommandContext())
                await command.Database.ExecuteSqlRawAsync(
                    "UPDATE [Order].[TicketCoupons] SET [CurrentOrderServiceId] = {0} WHERE [Id] = {1}",
                    scenario.ServiceId, unrelatedCoupon.Id);

            await using var harness = NewHarness();
            Register(harness, scenario);

            var covering = (await TicketsAsync(_fixture, scenario.OrderId))
                .Where(ticket => ticket.Coupons.Any(coupon =>
                    coupon.CurrentOrderServiceId == scenario.ServiceId
                    && coupon.FinancialStatus == TicketCouponFinancialStatus.Open))
                .ToList();

            Assert.Equal(2, covering.Count);

            var quoteRefusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.QuoteAsync(scenario.OrderId, scenario.ChangedOrderServiceIds));
            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20284, quoteRefusal.Code);
            Assert.Equal(20284, refusal.Code);
            Assert.Empty(harness.ExchangeQuotes.ObservedQuoteRequests);
            Assert.Empty(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
        }

        // ---------------------------------------------------------------- support

        private static object Facts(ElectronicTicket ticket)
            => new
            {
                ticket.DocumentNumber,
                ticket.StatusSummary,
                ticket.DocumentVersion,
                ticket.IssuedAt,
                ticket.IssuedTotal,
                ticket.PredecessorElectronicTicketId,
                ticket.PredecessorExchangeOperationId,
                Coupons = string.Join(
                    "|",
                    ticket.Coupons
                        .OrderBy(coupon => coupon.CouponNumber)
                        .Select(coupon =>
                            $"{coupon.Id}:{coupon.CouponNumber}:{coupon.FinancialStatus}:{coupon.ControlStatus}:{coupon.CurrentOrderServiceId}:{coupon.PredecessorTicketCouponId}:{coupon.IssuedSegment.FlightNumber}:{coupon.IssuanceValue}")),
                Exchanges = string.Join(
                    "|",
                    ticket.Exchanges
                        .OrderBy(record => record.OperationId)
                        .Select(record =>
                            $"{record.Id}:{record.OperationId}:{record.SuccessorElectronicTicketId}:{record.SuccessorDocumentNumber}:" +
                            string.Join(
                                ",",
                                record.Coupons
                                    .OrderBy(coupon => coupon.PredecessorCouponNumber)
                                    .Select(coupon =>
                                        $"{coupon.PredecessorTicketCouponId}>{coupon.SuccessorTicketCouponId}:{coupon.PredecessorCouponNumber}>{coupon.SuccessorCouponNumber}:{coupon.PreviousOrderServiceId}>{coupon.SuccessorOrderServiceId}")))),
                PriceLinks = string.Join(
                    "|",
                    ticket.PriceLinks
                        .OrderBy(link => link.Id)
                        .Select(link => $"{link.Id}:{link.CouponId}:{link.PricingLineId}:{link.AttributedValue}"))
            };

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-res-{Guid.NewGuid():N}"));
    }
}
