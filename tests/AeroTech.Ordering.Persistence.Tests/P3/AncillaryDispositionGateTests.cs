using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Unconfigured;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class AncillaryDispositionGateTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();
        private readonly string _secondDocument = NewDocumentNumber();

        public AncillaryDispositionGateTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A-F. what the affected scope is

        [Fact]
        public async Task A_an_exchange_with_no_ancillary_never_consults_the_disposition_source()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.NotRequired, outcome.AncillaryState);
            Assert.Empty(outcome.Ancillaries);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task B_a_standalone_ancillary_is_outside_the_affected_scope()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(
                _fixture, harness, scenario.OrderId, _document, [null], ElectronicMiscDocumentType.Standalone);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.NotRequired, outcome.AncillaryState);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
        }

        [Fact]
        public async Task C_a_voided_ancillary_document_is_outside_the_affected_scope()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            var attached = await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [ticket.Coupons.First().Id]);

            await VoidAncillaryAsync(_fixture, attached.Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.NotRequired, outcome.AncillaryState);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
        }

        [Fact]
        public async Task D_a_voided_ancillary_coupon_is_outside_the_affected_scope()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            var attached = await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [ticket.Coupons.First().Id]);

            await VoidAncillaryCouponAsync(_fixture, attached.Coupons.Single().Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.NotRequired, outcome.AncillaryState);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
        }

        [Fact]
        public async Task E_an_ancillary_on_a_flown_coupon_outside_the_reissue_scope_is_not_affected()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, [1], [2]);

            await AttachAncillaryAsync(
                _fixture, harness, scenario.OrderId, _document, [scenario.CouponIds[1]]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.NotRequired, outcome.AncillaryState);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
        }

        [Fact]
        public async Task F_the_affected_scope_is_described_to_the_disposition_source_in_accountable_terms()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var predecessorCoupon = ticket.Coupons.Single(coupon => coupon.Id == scenario.CouponId);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var request = Assert.Single(harness.AncillaryDispositions.ObservedRequests);
            var affected = Assert.Single(request.AffectedCoupons);

            Assert.Equal(scenario.OrderId, request.OrderId);
            Assert.Equal(ExchangeSourceFactory.QuoteId, request.QuotedExchangeId);
            Assert.Equal(ticket.DocumentNumber, request.PredecessorDocumentNumber);
            Assert.Equal([predecessorCoupon.CouponNumber], request.ReissueScopeCouponNumbers);
            Assert.Equal(_document, affected.EmdDocumentNumber);
            Assert.Equal(1, affected.EmdCouponNumber);
            Assert.Equal(ElectronicMiscDocumentType.Associated, affected.EmdType);
            Assert.Equal(EmdCouponPurpose.Fee, affected.Purpose);
            Assert.Equal("0DF", affected.ReasonForIssuanceSubCode);
            Assert.Equal(ticket.DocumentNumber, affected.PredecessorDocumentNumber);
            Assert.Equal(predecessorCoupon.CouponNumber, affected.PredecessorCouponNumber);
        }

        // ---------------------------------------------------------------- G-N. a decision that cannot be trusted

        [Fact]
        public async Task G_a_missing_decision_fails_closed_before_any_irreversible_work()
        {
            var refusal = await RefusedAsync(harness =>
                harness.AncillaryDispositions.OmittedCoupons.Add(AncillaryKey(_document, 1)));

            Assert.Equal(20296, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
        }

        [Fact]
        public async Task H_a_duplicated_decision_fails_closed()
        {
            var refusal = await RefusedAsync(harness => harness.AncillaryDispositions.DuplicateFirstDecision = true);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task I_a_decision_for_an_ancillary_outside_the_affected_scope_fails_closed()
        {
            var refusal = await RefusedAsync(harness => harness.AncillaryDispositions.AddUnrelatedDecision = true);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task J_a_decision_naming_another_predecessor_coupon_fails_closed()
        {
            var refusal = await RefusedAsync(
                harness => harness.AncillaryDispositions.ReportUnrelatedPredecessorCoupon = true);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task K_a_decision_naming_another_predecessor_document_fails_closed()
        {
            var refusal = await RefusedAsync(
                harness => harness.AncillaryDispositions.ReportUnrelatedPredecessorDocument = true);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task L_a_reassociation_without_a_target_coupon_fails_closed()
        {
            var refusal = await RefusedAsync(harness => harness.AncillaryDispositions.OmitTargetCoupon = true);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task M_a_target_coupon_outside_the_accepted_successor_scope_fails_closed()
        {
            var refusal = await RefusedAsync(
                harness => harness.AncillaryDispositions.TargetByCoupon[AncillaryKey(_document, 1)] = 9);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task N_a_decision_without_an_identity_of_its_own_fails_closed()
        {
            var refusal = await RefusedAsync(harness => harness.AncillaryDispositions.OmitDecisionReference = true);

            Assert.Equal(20297, refusal.Code);
        }

        [Fact]
        public async Task O_a_target_coupon_that_is_historical_used_context_fails_closed()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, [1], [2]);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponIds[2]]);
            harness.AncillaryDispositions.TargetByCoupon[AncillaryKey(_document, 1)] = 1;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20297, refusal.Code);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(await AncillaryHistoryAsync(scenario.OrderId));
        }

        // ---------------------------------------------------------------- P-R. a disposition this capability cannot run

        [Theory]
        [InlineData(AncillaryExchangeDisposition.ExchangeToNewEmd)]
        [InlineData(AncillaryExchangeDisposition.RetainAsResidual)]
        [InlineData(AncillaryExchangeDisposition.Cancel)]
        [InlineData(AncillaryExchangeDisposition.ManualReview)]
        public async Task P_an_unsupported_disposition_stops_before_the_first_irreversible_operation(
            AncillaryExchangeDisposition unsupported)
        {
            var refusal = await RefusedAsync(harness =>
                harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] = unsupported);

            Assert.Equal(20298, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
        }

        [Fact]
        public async Task Q_an_unsupported_disposition_on_one_coupon_stops_the_whole_exchange()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_secondDocument, 1)] =
                AncillaryExchangeDisposition.ManualReview;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20298, refusal.Code);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(await AncillaryHistoryAsync(scenario.OrderId));
        }

        [Fact]
        public async Task R_a_refused_decision_replays_terminally_under_the_same_command()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.ManualReview;

            var first = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            var replay = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(20298, first.Code);
            Assert.Equal(20298, replay.Code);
            Assert.Equal(ServicingOperationStatus.Rejected, await StatusAsync(scenario.OrderId));
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
        }

        // ---------------------------------------------------------------- S-T. a source that cannot answer

        [Fact]
        public async Task S_an_unreachable_disposition_source_leaves_the_operation_resumable_and_nothing_mutated()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.AncillaryDispositions.Throw = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, await StatusAsync(scenario.OrderId));
            Assert.Null(await PlanAsync(scenario.OrderId));
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.Empty(await AncillaryHistoryAsync(scenario.OrderId));
        }

        [Fact]
        public async Task T_an_unconfigured_disposition_source_refuses_instead_of_carrying_the_ancillary_forward()
        {
            await using var harness = NewHarness(new UnconfiguredAncillaryDispositionProvider());
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20294, refusal.Code);
            Assert.Equal(501, refusal.HttpStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, await StatusAsync(scenario.OrderId));
            Assert.Null(await PlanAsync(scenario.OrderId));
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
        }

        private async Task<BusinessException> RefusedAsync(Action<OrderSliceHarness> arrange)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            arrange(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(scenario.DocumentVersion, ticket.DocumentVersion);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);
            Assert.Equal(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(EmdCouponStatus.OpenForUse, ancillary.Coupons.Single().Status);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.Id == scenario.TicketId);
            Assert.All(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => Assert.Null(candidate.PredecessorElectronicTicketId));

            return refusal;
        }

        private async Task<ServicingOperationStatus?> StatusAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Set<ServicingOperation>()
                .Where(operation => operation.OrderId == orderId
                                    && operation.Kind == ServicingOperationKind.Exchange)
                .Select(operation => (ServicingOperationStatus?)operation.Status)
                .FirstOrDefaultAsync();
        }

        private async Task<AcceptedExchangePlanRow?> PlanAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Set<AcceptedExchangePlanRow>()
                .FirstOrDefaultAsync(plan => plan.OrderId == orderId);
        }

        private async Task<IReadOnlyList<long>> AncillaryHistoryAsync(long orderId)
        {
            var documents = await AncillariesAsync(_fixture, orderId);

            return documents
                .SelectMany(document => document.Coupons)
                .SelectMany(coupon => coupon.AssociationChanges)
                .Where(change => change.Kind != EmdCouponAssociationChangeKind.Associated)
                .Select(change => change.Id)
                .ToList();
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private OrderSliceHarness NewHarness(UnconfiguredAncillaryDispositionProvider dispositions)
            => new(_fixture, Caller(), unconfiguredAncillaryDispositions: dispositions);

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7431, $"anc-{Guid.NewGuid():N}");
    }
}
