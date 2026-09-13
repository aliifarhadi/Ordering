using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Query.OrderAggregate.View;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingCheckpointParityTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document;

        public ServicingCheckpointParityTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _document = $"CP{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
        }

        [Theory]
        [InlineData(AncillaryExchangeDisposition.ReassociateExisting)]
        [InlineData(AncillaryExchangeDisposition.Refund)]
        [InlineData(AncillaryExchangeDisposition.ExchangeToNewEmd)]
        [InlineData(AncillaryExchangeDisposition.RetainAsResidual)]
        [InlineData(AncillaryExchangeDisposition.ManualReview)]
        public async Task CP1_Query_checkpoints_equal_domain_checkpoints_for_every_disposition(
            AncillaryExchangeDisposition disposition)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, disposition);

            await AssertParityAsync(operationId);
        }

        [Fact]
        public async Task CP1b_Query_checkpoints_equal_domain_checkpoints_for_a_cancel_group()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await CancelExchangeAsync(setup, harness);

            await AssertParityAsync(operationId);
        }

        [Theory]
        [InlineData(ChangeMonetaryOutcome.Refund, "[RefundDueOutcome]")]
        [InlineData(ChangeMonetaryOutcome.Residual, "[ResidualOutcome]")]
        public async Task CP2_A_required_return_of_value_with_no_outcome_is_unsettled_on_both_sides(
            ChangeMonetaryOutcome monetaryOutcome,
            string column)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await NegativeBalanceAsync(_fixture, setup, harness, monetaryOutcome, [1]);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            await AssertRequiredMonetaryLegAsync(outcome.OperationId, column);
        }

        [Fact]
        public async Task CP2b_A_required_add_collect_with_no_capture_is_unsettled_on_both_sides()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            await AssertRequiredMonetaryLegAsync(outcome.OperationId, "[FundingCaptureOutcome]");
        }

        private async Task AssertRequiredMonetaryLegAsync(long operationId, string column)
        {
            await using var before = NewHarness();
            var plan = await before.ExchangePlans.FindAsync(operationId);

            Assert.NotNull(plan);
            Assert.True(plan!.RequiresMonetarySettlement);

            await SetPlanAsync(operationId, $"{column} = NULL");

            var checkpoints = await AssertParityAsync(operationId);

            Assert.True(checkpoints.HasUnsettledMonetary);
        }

        [Fact]
        public async Task CP3_A_confirmed_refund_document_without_refund_value_is_unsettled_on_both_sides()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.Refund);

            await SetAncillaryAsync(
                operationId,
                "[RefundDocumentOutcome] = {0}, [RefundValueOutcome] = NULL",
                (int)ProviderOperationOutcome.Confirmed);

            var checkpoints = await AssertParityAsync(operationId);

            Assert.True(checkpoints.HasUnsettledAncillary);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        [InlineData(ProviderOperationOutcome.Rejected)]
        public async Task CP4_A_confirmed_refund_document_with_an_unconfirmed_value_is_unsettled_on_both_sides(
            ProviderOperationOutcome value)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.Refund);

            await SetAncillaryAsync(
                operationId,
                $"[RefundDocumentOutcome] = {(int)ProviderOperationOutcome.Confirmed}, [RefundValueOutcome] = {{0}}",
                (int)value);

            var checkpoints = await AssertParityAsync(operationId);

            Assert.True(checkpoints.HasUnsettledAncillary);
        }

        [Fact]
        public async Task CP5_An_unsettled_retention_is_unsettled_on_both_sides()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.RetainAsResidual);

            await SetAncillaryAsync(operationId, "[RetentionSettledAt] = NULL");

            var checkpoints = await AssertParityAsync(operationId);

            Assert.True(checkpoints.HasUnsettledAncillary);
        }

        [Fact]
        public async Task CP6_An_unsettled_cancel_group_is_unsettled_on_both_sides()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await CancelExchangeAsync(setup, harness);

            await SetCancelGroupAsync(operationId, "[CancellationSettledAt] = NULL");

            var checkpoints = await AssertParityAsync(operationId);

            Assert.True(checkpoints.HasUnsettledAncillary);
        }

        [Theory]
        [InlineData("[SuccessorElectronicMiscDocumentId] = NULL")]
        [InlineData("[ExchangeOutcome] = NULL")]
        [InlineData("[FundingCaptureOutcome] = NULL")]
        [InlineData("[RefundDueOutcome] = NULL")]
        [InlineData("[ResidualOutcome] = NULL")]
        public async Task CP7_An_unsettled_exchange_group_leg_is_unsettled_on_both_sides(string mutation)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.ExchangeToNewEmd);

            await SetExchangeGroupAsync(operationId, mutation);

            await AssertParityAsync(operationId);
        }

        [Fact]
        public async Task CP8_An_unsettled_fee_document_is_unsettled_on_both_sides()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.ReassociateExisting);

            await SetFeeDocumentAsync(operationId, "[SettledAt] = NULL");

            await AssertParityAsync(operationId);
        }

        [Fact]
        public async Task CP9_The_operator_stage_label_stays_a_stable_semantic_name()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.ManualReview);

            var view = await ViewAsync(operationId);

            Assert.Equal(ServicingPlanCheckpoints.ManualReviewStage, view.UnresolvedStage);
            Assert.Equal("ManualReview", view.UnresolvedStage);

            Assert.DoesNotContain(
                new[]
                {
                    nameof(ServicingPlanCheckpoints.IsEligibilityEstablished),
                    nameof(ServicingPlanCheckpoints.HasUnsettledMonetary),
                    nameof(ServicingPlanCheckpoints.HasUnsettledFeeDocument),
                    nameof(ServicingPlanCheckpoints.HasUnsettledAncillary),
                    nameof(ServicingPlanCheckpoints.HasUnresolvedManualReview)
                },
                name => string.Equals(name, view.UnresolvedStage, StringComparison.Ordinal));
        }

        [Fact]
        public async Task CP10_An_unresolved_external_evidence_outranks_a_manual_review()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ExchangeAsync(setup, harness, AncillaryExchangeDisposition.ManualReview);

            await harness.ServicingEvidence.RecordAsync(
                operationId,
                ServicingEvidenceStage.DocumentRefund,
                ProviderOperationOutcome.Unknown,
                null,
                null);

            await harness.UnitOfWork.SaveChangesAsync();

            var view = await ViewAsync(operationId);

            Assert.Equal(ServicingRecoveryAction.ReplayCommand, view.RecoveryAction);
            Assert.NotEmpty(view.ManualReviewReasons);
        }

        private async Task<ServicingPlanCheckpoints> AssertParityAsync(long operationId)
        {
            await using var reading = NewHarness();

            var plan = await reading.ExchangePlans.FindAsync(operationId);
            var view = await reading.ReconciliationView.FindAsync(operationId);

            Assert.NotNull(plan);
            Assert.NotNull(view);
            Assert.NotNull(view!.ExchangeCheckpoints);

            Assert.Equal(plan!.Checkpoints, view.ExchangeCheckpoints);
            Assert.Equal(plan.Checkpoints.UnresolvedStage, view.ExchangeCheckpoints!.UnresolvedStage);

            if (!ServicingRecoveryPolicy.IsSettled(view.Status))
                Assert.Equal(plan.Checkpoints.UnresolvedStage, view.UnresolvedStage);

            return view.ExchangeCheckpoints;
        }

        private async Task<ServicingReconciliationView> ViewAsync(long operationId)
        {
            await using var reading = NewHarness();
            var view = await reading.ReconciliationView.FindAsync(operationId);

            Assert.NotNull(view);

            return view!;
        }

        private async Task<long> CancelExchangeAsync(OrderSliceHarness setup, OrderSliceHarness harness)
        {
            var issued = await IssuedAsync(
                _fixture,
                setup,
                beforeReservation: async order =>
                {
                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)),
                        setup.Ids,
                        setup.Clock);

                    await setup.UnitOfWork.SaveChangesAsync();
                });

            var lounge = (await ReloadAsync(_fixture, issued.OrderId))
                .OrderServices.Single(service => service.ServiceType == OrderServiceType.LoungeAccess);

            var airCoupon = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId))
                .Coupons.Single(coupon => coupon.CouponNumber == 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Cancel;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            return outcome.OperationId;
        }

        private async Task<long> ExchangeAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            AncillaryExchangeDisposition disposition)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _document, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = disposition;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            return outcome.OperationId;
        }

        private Task SetPlanAsync(long operationId, string assignment, params object[] args)
            => ExecuteAsync($"UPDATE [Order].[AcceptedExchangePlans] SET {assignment} WHERE [OperationId] = {{{args.Length}}}", args, operationId);

        private Task SetAncillaryAsync(long operationId, string assignment, params object[] args)
            => ExecuteAsync($"UPDATE [Order].[AcceptedExchangePlanAncillaries] SET {assignment} WHERE [OperationId] = {{{args.Length}}}", args, operationId);

        private Task SetExchangeGroupAsync(long operationId, string assignment, params object[] args)
            => ExecuteAsync($"UPDATE [Order].[AcceptedExchangePlanAncillaryExchangeGroups] SET {assignment} WHERE [OperationId] = {{{args.Length}}}", args, operationId);

        private Task SetCancelGroupAsync(long operationId, string assignment, params object[] args)
            => ExecuteAsync($"UPDATE [Order].[AcceptedExchangePlanAncillaryCancelGroups] SET {assignment} WHERE [OperationId] = {{{args.Length}}}", args, operationId);

        private Task SetFeeDocumentAsync(long operationId, string assignment, params object[] args)
            => ExecuteAsync($"UPDATE [Order].[AcceptedExchangePlanFeeDocuments] SET {assignment} WHERE [OperationId] = {{{args.Length}}}", args, operationId);

        private async Task ExecuteAsync(string sql, object[] args, long operationId)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(sql, [.. args, operationId]);
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"parity-{Guid.NewGuid():N}");
    }
}
