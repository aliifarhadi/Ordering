using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class EmdExchangeFreezeGuardTests
    {
        private const decimal SourceValue = 50_000m;
        private const decimal ResidualAmount = 10_000m;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();

        public EmdExchangeFreezeGuardTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- guard 1. the refund must be an original refundable source

        [Theory]
        [InlineData("Wallet")]
        [InlineData("Voucher")]
        [InlineData("TravelBank")]
        [InlineData("CreditShell")]
        public async Task H1_a_refund_that_is_not_an_original_refundable_source_fails_before_any_irreversible_work(
            string disposition)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeRefundDue = ResidualAmount;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - ResidualAmount;
            harness.AncillaryDispositions.ExchangeRefundDispositionOverride = disposition;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20312, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        [Fact]
        public async Task H2_an_original_form_of_payment_refund_still_settles()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeRefundDue = ResidualAmount;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - ResidualAmount;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var request = Assert.Single(harness.RefundValues.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(AcceptedRefundDue.OriginalFormOfPayment, request.ApprovedDisposition);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- guard 2. beneficiary evidence is never silently absent

        [Fact]
        public async Task H3_a_confirmed_successor_without_a_beneficiary_reconciles_and_materializes_nothing()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.EmdExchanges.OmitSuccessorBeneficiary = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await PredecessorAsync(scenario)).StatusSummary);
        }

        [Fact]
        public async Task H4_a_confirmed_successor_naming_another_beneficiary_still_reconciles()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.EmdExchanges.BeneficiaryOverride = 4242L;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- guard 3. the coupled residual collision is exact identity

        [Theory]
        [InlineData("wrong-issuer")]
        [InlineData("wrong-issuing-office")]
        [InlineData("wrong-authority")]
        [InlineData("wrong-reason-for-issuance")]
        [InlineData("wrong-reason-for-issuance-sub-code")]
        [InlineData("wrong-operation")]
        [InlineData("wrong-beneficiary")]
        [InlineData("wrong-currency")]
        public async Task H5_an_existing_residual_document_that_is_not_this_one_reconciles(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = ResidualAmount;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - ResidualAmount;

            await AttachResidualAncillaryAsync(
                _fixture,
                setup,
                scenario.OrderId,
                ResidualNumberFor(_document),
                ResidualAmount,
                travelerId: shape == "wrong-beneficiary" ? 4242L : null,
                operationId: shape == "wrong-operation" ? 987_654_321L : null,
                issuerCarrierId: shape == "wrong-issuer" ? 999L : OrderSliceHarness.HomeAirlineId,
                issuingOfficeId: shape == "wrong-issuing-office" ? 777L : null,
                authority: shape == "wrong-authority" ? DocumentAuthority.External : DocumentAuthority.Local,
                reasonForIssuanceCode: shape == "wrong-reason-for-issuance" ? "Z" : "D",
                reasonForIssuanceSubCode: shape == "wrong-reason-for-issuance-sub-code" ? "99Z" : "98R",
                currencyId: shape == "wrong-currency" ? 77 : null);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var documents = await AncillariesAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(2, documents.Count);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.DoesNotContain(documents, document => document.DocumentNumber == SuccessorOf(_document));
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await PredecessorAsync(scenario)).StatusSummary);
        }

        [Fact]
        public async Task H6_an_exact_coupled_residual_replays_without_a_duplicate_document()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var key = NewKey();

            harness.AncillaryDispositions.ExchangeResidual = ResidualAmount;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - ResidualAmount;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var documents = await AncillariesAsync(_fixture, scenario.OrderId);
            var residual = documents.Single(
                document => document.DocumentNumber == ResidualNumberFor(_document));

            Assert.Equal(ServicingOperationStatus.Completed, first.OperationStatus);
            Assert.True(replay.IsReplay);
            Assert.Equal(3, documents.Count);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, residual.Type);
            Assert.Equal(first.OperationId, residual.OperationId);
            Assert.Equal(ResidualAmount, residual.Coupons.Single().IssuanceValue);
            Assert.Equal(EmdCouponPurpose.ResidualValue, residual.Coupons.Single().Purpose);
            Assert.Null(residual.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(1, residual.DocumentVersion);
        }

        // ---------------------------------------------- helpers

        private async Task<ExchangeScenario> ExchangeScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;

            return scenario;
        }

        private async Task<ElectronicTicket> PredecessorAsync(ExchangeScenario scenario)
            => await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

        private static string SuccessorOf(string documentNumber) => $"{documentNumber}X";

        private static string ResidualNumberFor(string documentNumber) => $"{documentNumber}XR";

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7436, $"emdxg-{Guid.NewGuid():N}");
    }
}
