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
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ResidualDocumentCouplingTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public ResidualDocumentCouplingTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- the settled coupled residual

        [Fact]
        public async Task RD1_a_confirmed_exchange_returns_the_successor_and_its_residual_document_together()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledResidualAsync(setup, harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await SuccessorAsync(scenario, outcome);
            var residual = await ResidualDocumentAsync(scenario);
            var coupon = residual.Coupons.Single();
            var request = Assert.Single(harness.DocumentExchanges.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ChangeMonetaryOutcome.Residual, outcome.MonetaryOutcome);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);

            Assert.NotNull(request.Residual);
            Assert.Equal(ExchangeSourceFactory.NegativeBalanceAmount, request.Residual!.Amount);
            Assert.Equal(ResidualInstrumentKind.Emd, request.Residual.ExpectedInstrument);

            Assert.Equal(ElectronicMiscDocumentType.Standalone, residual.Type);
            Assert.Equal(EmdCouponPurpose.ResidualValue, coupon.Purpose);
            Assert.Equal(ExchangeSourceFactory.NegativeBalanceAmount, coupon.IssuanceValue);
            Assert.Equal(residual.DocumentNumber, coupon.ExternalValueReference);
            Assert.Null(coupon.OrderServiceId);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.Equal(residual.DocumentNumber, outcome.ResidualInstrumentReference);
            Assert.Equal(ResidualInstrumentKind.Emd, outcome.ResidualInstrument);

            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        [Fact]
        public async Task RD2_exactly_one_successor_and_one_residual_document_exist_after_a_replay()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledResidualAsync(setup, harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.True(replay.IsReplay);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
            Assert.Single(
                await AncillariesAsync(_fixture, scenario.OrderId),
                document => document.Coupons.Any(coupon => coupon.Purpose == EmdCouponPurpose.ResidualValue));
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        // ---------------------------------------------- the host has not confirmed

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task RD3_an_unconfirmed_exchange_materializes_neither_the_ticket_nor_the_residual(
            ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledResidualAsync(setup, harness);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = unresolved;
            harness.DocumentExchanges.RecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Null(replay.SuccessorElectronicTicketId);
            Assert.Null(replay.ResidualInstrumentReference);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.NotEmpty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        [Fact]
        public async Task RD4_a_refused_exchange_creates_neither_the_successor_nor_the_residual()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledResidualAsync(setup, harness);

            harness.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        // ---------------------------------------------- the host confirmed but proved nothing

        [Fact]
        public async Task RD5_a_confirmation_without_the_residual_document_reconciles_and_invents_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledResidualAsync(setup, harness);
            var key = NewKey();

            harness.DocumentExchanges.OmitCoupledResidualDocument = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).StatusSummary);

            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.False(plan.IsResidualSettled);
            Assert.False(string.IsNullOrWhiteSpace(plan.ResidualDetail));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("wrong-instrument")]
        public async Task RD6_a_contradictory_residual_document_reconciles_without_undoing_the_reissue(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledResidualAsync(setup, harness);

            switch (shape)
            {
                case "wrong-amount":
                    harness.DocumentExchanges.ResidualAmountOverride =
                        ExchangeSourceFactory.NegativeBalanceAmount + 1m;
                    break;
                case "wrong-currency":
                    harness.DocumentExchanges.ResidualCurrencyOverride = 77;
                    break;
                default:
                    harness.DocumentExchanges.ResidualInstrumentOverride = ResidualInstrumentKind.Voucher;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.False(plan.IsResidualSettled);
            Assert.False(string.IsNullOrWhiteSpace(plan.ResidualDetail));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        // ---------------------------------------------- crash boundaries

        [Fact]
        public async Task RD7_an_exchange_the_caller_never_saw_recovers_the_ticket_and_the_residual_together()
        {
            var caller = Caller();
            var documents = new DeterministicDocumentExchangeAdapter { ThrowAfterDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, documentExchanges: documents);
            var scenario = await CoupledResidualAsync(setup, crashed);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Empty(await ResidualDocumentsAsync(scenario));

            documents.ThrowAfterDispatch = false;
            documents.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentExchanges: documents);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));
            var residual = await ResidualDocumentAsync(scenario);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(documents.ObservedRequests);
            Assert.Single(documents.ObservedRecoveryKeys);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
            Assert.Equal(residual.DocumentNumber, finalized.ResidualInstrumentReference);
            Assert.Single(residual.Coupons);
            Assert.Empty(resumed.ExchangeResiduals.ObservedRequests);
        }

        // ---------------------------------------------- mixed collection plus coupled residual

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Rejected)]
        public async Task RD8_a_coupled_residual_stays_authoritative_when_the_collection_does_not_settle(
            ProviderOperationOutcome capture)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledMixedResidualAsync(setup, harness);

            harness.ExchangeFunding.CaptureOutcome = capture;
            harness.ExchangeFunding.CaptureRecoveryOutcome = capture;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var residual = await ResidualDocumentAsync(scenario);

            Assert.Equal(
                capture == ProviderOperationOutcome.Rejected
                    ? ServicingOperationStatus.NeedsReconciliation
                    : ServicingOperationStatus.AwaitingExternal,
                outcome.OperationStatus);

            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(residual.DocumentNumber, outcome.ResidualInstrumentReference);
            Assert.Single(residual.Coupons, coupon => coupon.Purpose == EmdCouponPurpose.ResidualValue);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedGuarantees);
        }

        [Fact]
        public async Task RD9_a_coupled_residual_is_issued_before_the_collection_is_captured()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await CoupledMixedResidualAsync(setup, harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));
            var residual = await ResidualDocumentAsync(scenario);
            var capture = Assert.Single(harness.ExchangeFunding.ObservedCaptures);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, outcome.FundingState);
            Assert.Equal(residual.DocumentNumber, outcome.ResidualInstrumentReference);
            Assert.Equal(outcome.SuccessorDocumentNumber, capture.SuccessorDocumentNumber);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------- the external residual is untouched

        [Fact]
        public async Task RD10_an_external_residual_still_settles_through_the_downstream_port()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var request = Assert.Single(harness.DocumentExchanges.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Null(request.Residual);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.False(string.IsNullOrWhiteSpace(outcome.ResidualInstrumentReference));
        }

        // ---------------------------------------------- the accepted fulfilment must be coherent

        [Fact]
        public async Task RD11_a_document_coupled_residual_naming_an_external_instrument_fails_closed()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1],
                shapeAccepted: accepted => accepted with
                {
                    Residual = accepted.Residual! with
                    {
                        ExpectedInstrument = ResidualInstrumentKind.Voucher,
                        Fulfillment = ResidualFulfillment.DocumentCoupled
                    }
                });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20305, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(await ResidualDocumentsAsync(scenario));
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
        }

        private async Task<ExchangeScenario> CoupledResidualAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness)
            => await NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, [1],
                shapeAccepted: DocumentCoupled);

        private async Task<ExchangeScenario> CoupledMixedResidualAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness)
            => await MixedAsync(
                _fixture, setup, harness, ExchangeMonetaryLegKind.Residual, [1],
                shapeAccepted: DocumentCoupled);

        private static AcceptedExchange DocumentCoupled(AcceptedExchange accepted)
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

        private async Task<ElectronicMiscDocument> ResidualDocumentAsync(ExchangeScenario scenario)
            => Assert.Single(await ResidualDocumentsAsync(scenario));

        private async Task<ElectronicTicket> SuccessorAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
            => (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7434, $"resdoc-{Guid.NewGuid():N}");
    }
}
