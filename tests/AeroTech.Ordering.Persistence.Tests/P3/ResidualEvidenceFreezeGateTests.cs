using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ResidualEvidenceFreezeGateTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public ResidualEvidenceFreezeGateTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- FFC1, FFC7. a residual nobody asked for

        [Fact]
        public async Task FFC1_FFC7_an_unrequested_residual_document_reconciles_and_is_never_materialized()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            harness.DocumentExchanges.ReturnUnrequestedResidualDocument = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            await AssertReissueIsAuthoritativeAsync(scenario, outcome);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.True(plan.IsResidualRejected);
            Assert.False(string.IsNullOrWhiteSpace(plan.ResidualDetail));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------- FFC2. the double fulfilment guard

        [Fact]
        public async Task FFC2_an_unrequested_residual_never_fulfils_an_external_residual_twice()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1]);

            harness.DocumentExchanges.ReturnUnrequestedResidualDocument = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            await AssertReissueIsAuthoritativeAsync(scenario, outcome);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRecoveryKeys);
            Assert.True(plan.IsResidualRejected);
            Assert.False(plan.IsResidualSettled);
            Assert.Null(Assert.Single(harness.DocumentExchanges.ObservedRequests).Residual);
        }

        // ---------------------------------------------- FFC3. the untouched external path

        [Fact]
        public async Task FFC3_an_external_residual_still_settles_downstream_exactly_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Null(Assert.Single(harness.DocumentExchanges.ObservedRequests).Residual);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.False(string.IsNullOrWhiteSpace(outcome.ResidualInstrumentReference));
        }

        // ---------------------------------------------- FFC4. the coupled path is unchanged

        [Fact]
        public async Task FFC4_a_matching_coupled_residual_settles_inside_the_exchange()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1], shapeAccepted: CoupledEmd);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var residual = Assert.Single(await ResidualDocumentsAsync(scenario));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.NotNull(Assert.Single(harness.DocumentExchanges.ObservedRequests).Residual);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Equal(residual.DocumentNumber, outcome.ResidualInstrumentReference);
            Assert.Single(residual.Coupons, coupon => coupon.Purpose == EmdCouponPurpose.ResidualValue);
        }

        // ---------------------------------------------- FFC5, FFC6. only an EMD-S is executable

        [Theory]
        [InlineData(ResidualInstrumentKind.Mco)]
        [InlineData(ResidualInstrumentKind.Voucher)]
        [InlineData(ResidualInstrumentKind.TravelCredit)]
        [InlineData(ResidualInstrumentKind.Other)]
        [InlineData(ResidualInstrumentKind.Unknown)]
        public async Task FFC5_FFC6_a_coupled_residual_that_is_not_an_emd_fails_before_any_irreversible_work(
            ResidualInstrumentKind instrument)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1],
                shapeAccepted: accepted => accepted with
                {
                    Residual = accepted.Residual! with
                    {
                        ExpectedInstrument = instrument,
                        Fulfillment = ResidualFulfillment.DocumentCoupled
                    }
                });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(20305, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Issued, ticket.StatusSummary);
        }

        // ---------------------------------------------- FFC8. replay after a contradiction

        [Fact]
        public async Task FFC8_a_contradiction_replays_without_a_second_dispatch_of_anything()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1]);
            var key = NewKey();

            harness.DocumentExchanges.ReturnUnrequestedResidualDocument = true;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task FFC8_a_missing_coupled_residual_replays_without_a_second_dispatch_of_anything()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1], shapeAccepted: CoupledEmd);
            var key = NewKey();

            harness.DocumentExchanges.OmitCoupledResidualDocument = true;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            await AssertReissueIsAuthoritativeAsync(scenario, replay);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
        }

        // ---------------------------------------------- an impostor document never satisfies the residual

        [Fact]
        public async Task An_existing_document_that_is_not_the_reported_residual_never_satisfies_it()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1], shapeAccepted: CoupledEmd);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            var impostor = $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, impostor, [ticket.Coupons.First().Id]);
            harness.DocumentExchanges.ResidualDocumentNumberOverride = impostor;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            await AssertReissueIsAuthoritativeAsync(scenario, outcome);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(plan.IsResidualRejected);
            Assert.False(string.IsNullOrWhiteSpace(plan.ResidualDetail));
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        private async Task AssertReissueIsAuthoritativeAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
        {
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.DocumentExchangeOutcome);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.Id == outcome.SuccessorElectronicTicketId);
        }

        private static AcceptedExchange CoupledEmd(AcceptedExchange accepted)
            => accepted with
            {
                Residual = accepted.Residual! with
                {
                    ExpectedInstrument = ResidualInstrumentKind.Emd,
                    Fulfillment = ResidualFulfillment.DocumentCoupled
                }
            };

        private async Task<IReadOnlyList<ElectronicMiscDocument>> ResidualDocumentsAsync(ExchangeScenario scenario)
            => (await AncillariesAsync(_fixture, scenario.OrderId))
                .Where(document => document.Coupons.Any(coupon => coupon.Purpose == EmdCouponPurpose.ResidualValue))
                .ToList();

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7435, $"resevi-{Guid.NewGuid():N}");
    }
}
