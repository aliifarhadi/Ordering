using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class RetainAsResidualFreezeCorrectionTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document;
        private readonly string _secondDocument;

        public RetainAsResidualFreezeCorrectionTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;

            var seed = Random.Shared.NextInt64(10_000_000, 99_999_999);

            _document = $"M1{seed}";
            _secondDocument = $"M9{seed}";
        }

        // ---------------------------------------------- 1. a real non-air ancillary service is retained

        [Fact]
        public async Task C1_a_retained_lounge_ancillary_is_cancelled_while_the_air_service_stays_exchanged()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var airCoupon = ticket.Coupons.First();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [airCoupon.Id],
                deliveringOrderServiceId: lounge.Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpRetention(harness);

            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var beforeLounge = before.OrderServices.Single(service => service.Id == lounge.Id);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var retainedService = after.OrderServices.Single(service => service.Id == lounge.Id);
            var airService = after.OrderServices.Single(service => service.Id == airCoupon.CurrentOrderServiceId);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var exchangeSet = Assert.Single(
                after.PriceConsequencesOf(
                    after.Changes.Single(change => change.OperationId == outcome.OperationId).Id));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            // the frozen ticket-exchange truth on the air service is untouched
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, airService.CommercialStatus);

            // the ancillary service is commercially withdrawn, and nothing else about it is rewritten
            Assert.Equal(OrderServiceStatus.Cancelled, retainedService.Status);
            Assert.Equal(OrderServiceCommercialStatus.Cancelled, retainedService.CommercialStatus);
            Assert.Equal(beforeLounge.DeliveryStatus, retainedService.DeliveryStatus);
            Assert.Equal(beforeLounge.DocumentStatus, retainedService.DocumentStatus);
            Assert.Equal(beforeLounge.FinancialStatus, retainedService.FinancialStatus);

            // exactly one G4 commercial version advance on top of the ticket exchange's own
            Assert.Equal(exchangeSet.ExpectedCommercialVersion + 2, after.CommercialVersion);

            Assert.NotNull(plan!.AncillaryRetentions.Single().RetentionSettledAt);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- 2. the exchanged air service is never overwritten

        [Fact]
        public async Task C2_an_emd_pointing_at_the_exchanged_air_service_reconciles_without_overwriting_it()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var airCoupon = ticket.Coupons.First();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [airCoupon.Id],
                deliveringOrderServiceId: airCoupon.CurrentOrderServiceId);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpRetention(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var airService = after.OrderServices.Single(service => service.Id == airCoupon.CurrentOrderServiceId);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var exchangeSet = Assert.Single(
                after.PriceConsequencesOf(
                    after.Changes.Single(change => change.OperationId == outcome.OperationId).Id));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, airService.CommercialStatus);
            Assert.Null(plan!.AncillaryRetentions.Single().RetentionSettledAt);
            Assert.Equal(exchangeSet.ExpectedCommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- 3. a foreign order service is never silently ignored

        [Fact]
        public async Task C3_an_order_service_owned_by_another_order_reconciles()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var foreign = await LoungeOrderAsync(setup);
            var foreignLounge = await LoungeServiceAsync(foreign.OrderId);

            var issued = await IssuedAsync(_fixture, setup);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [ticket.Coupons.First().Id],
                deliveringOrderServiceId: foreignLounge.Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpRetention(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var foreignAfter = (await ReloadAsync(_fixture, foreign.OrderId))
                .OrderServices.Single(service => service.Id == foreignLounge.Id);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Null(plan!.AncillaryRetentions.Single().RetentionSettledAt);
            Assert.NotEqual(OrderServiceCommercialStatus.Cancelled, foreignAfter.CommercialStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
        }

        // ---------------------------------------------- 4. delivery status is observation, not ours to rewrite

        [Theory]
        [InlineData(OrderServiceDeliveryStatus.Delivered)]
        [InlineData(OrderServiceDeliveryStatus.Consumed)]
        [InlineData(OrderServiceDeliveryStatus.NoShow)]
        public async Task C4_a_non_default_delivery_status_survives_retention_exactly(
            OrderServiceDeliveryStatus delivery)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [ticket.Coupons.First().Id],
                deliveringOrderServiceId: lounge.Id);

            await SetOrderServiceDeliveryStatusAsync(_fixture, lounge.Id, delivery);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpRetention(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var retainedService = (await ReloadAsync(_fixture, scenario.OrderId))
                .OrderServices.Single(service => service.Id == lounge.Id);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(delivery, retainedService.DeliveryStatus);
            Assert.Equal(OrderServiceCommercialStatus.Cancelled, retainedService.CommercialStatus);
        }

        // ---------------------------------------------- 5. replay never transitions or bumps again

        [Fact]
        public async Task C5_a_completed_retention_replays_without_a_second_transition_or_version()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [ticket.Coupons.First().Id],
                deliveringOrderServiceId: lounge.Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpRetention(harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var settledAt = (await harness.ExchangePlans.FindAsync(first.OperationId))!
                .AncillaryRetentions.Single().RetentionSettledAt;

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = await harness.ExchangePlans.FindAsync(first.OperationId);

            Assert.True(replay.IsReplay);
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(settledAt, plan!.AncillaryRetentions.Single().RetentionSettledAt);
            Assert.Equal(
                OrderServiceCommercialStatus.Cancelled,
                after.OrderServices.Single(service => service.Id == lounge.Id).CommercialStatus);
        }

        // ---------------------------------------------- 6/7. association conflicts before and after the document

        [Fact]
        public async Task C6_a_post_ticket_moved_association_reconciles_instead_of_refusing()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter();

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await TwoCouponRetentionScenarioAsync(crashed);
            var key = NewKey();

            associations.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var document = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);
            var successor = (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
            var moved = successor.Coupons.First().Id;

            await AssociateAncillaryCouponAsync(_fixture, document.Coupons.Single().Id, moved);

            associations.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            Register(resumed, scenario);
            ComposeTwoCouponRetention(resumed);

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);
            var plan = await resumed.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(successor.Id, outcome.SuccessorElectronicTicketId);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.Null(plan!.AncillaryRetentions.Single().RetentionSettledAt);
            Assert.Equal(moved, after.Coupons.Single().AssociatedTicketCouponId);
            Assert.Single(
                after.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task C7_a_pre_document_association_mismatch_still_fails_closed()
        {
            var caller = Caller();
            var documentExchanges = new DeterministicDocumentExchangeAdapter
            {
                ExchangeOutcome = ProviderOperationOutcome.Pending,
                RecoveryOutcome = ProviderOperationOutcome.Pending
            };

            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();

            var document = await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [coupons[0].Id]);

            await using var held = new OrderSliceHarness(
                _fixture, caller, documentExchanges: documentExchanges);

            var scenario = await QuotedAsync(_fixture, held, issued, [1]);
            SetUpRetention(held);
            var key = NewKey();

            // the reissue is accepted but the document exchange never resolves, so nothing is materialized
            var pending = await held.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, pending.OperationStatus);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);

            // the coupon moves onto a real coupon this reissue never touched, before any document truth exists
            await AssociateAncillaryCouponAsync(_fixture, document.Coupons.Single().Id, coupons[1].Id);

            documentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(
                _fixture, caller, documentExchanges: documentExchanges);
            Register(resumed, scenario);
            SetUpRetention(resumed);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => resumed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var after = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(20302, refusal.Code);
            Assert.Equal(409, refusal.HttpStatus);
            Assert.Equal(coupons[1].Id, after.Coupons.Single().AssociatedTicketCouponId);
            Assert.DoesNotContain(
                after.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);
        }

        // ---------------------------------------------- helpers

        private async Task<IssuedTicket> LoungeOrderAsync(OrderSliceHarness harness)
            => await IssuedAsync(
                _fixture,
                harness,
                beforeReservation: async order =>
                {
                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)),
                        harness.Ids,
                        harness.Clock);

                    await harness.UnitOfWork.SaveChangesAsync();
                });

        private async Task<OrderService> LoungeServiceAsync(long orderId)
            => (await ReloadAsync(_fixture, orderId))
                .OrderServices.Single(service => service.ServiceType == OrderServiceType.LoungeAccess);

        private async Task<ExchangeScenario> TwoCouponRetentionScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            ComposeTwoCouponRetention(harness);

            return scenario;
        }

        private void ComposeTwoCouponRetention(OrderSliceHarness harness)
        {
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.ReassociateExisting;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_secondDocument, 1)] =
                AncillaryExchangeDisposition.RetainAsResidual;
        }

        private static void SetUpRetention(OrderSliceHarness harness)
            => harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.RetainAsResidual;

        private async Task<ElectronicTicket> PredecessorAsync(ExchangeScenario scenario)
            => await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7436, $"ancretc-{Guid.NewGuid():N}");
    }
}
