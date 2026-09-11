using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class MixedExchangeFlowTests
    {
        private const decimal Collect = ExchangeSourceFactory.MixedCollectionAmount;
        private const decimal Refund = ExchangeSourceFactory.MixedRefundAmount;
        private const decimal ResidualCollect = ExchangeSourceFactory.MixedResidualCollectionAmount;
        private const decimal Residual = ExchangeSourceFactory.MixedResidualAmount;
        private const decimal NettedRefund = Collect - Refund;
        private const decimal NettedResidual = ResidualCollect - Residual;

        private readonly OrderingDatabaseFixture _fixture;

        public MixedExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A, B. the accepted plan

        [Fact]
        public async Task A_a_mixed_collection_and_refund_plan_preserves_both_authoritative_amounts()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var accepted = scenario.Accepted;
            var legs = accepted.MonetaryLegs();

            Assert.Equal(ChangeMonetaryOutcome.Mixed, accepted.MonetaryOutcome);
            Assert.Equal(Collect, accepted.AddCollect!.Amount);
            Assert.Equal(Refund, accepted.RefundDue!.Amount);
            Assert.Null(accepted.Residual);
            Assert.Equal(2, legs.Count);
            Assert.Single(legs, leg => leg.Kind == ExchangeMonetaryLegKind.Collection && leg.Amount == Collect);
            Assert.Single(legs, leg => leg.Kind == ExchangeMonetaryLegKind.RefundDue && leg.Amount == Refund);
            Assert.Equal(legs.Count, legs.Select(leg => leg.LegIdentity).Distinct().Count());
        }

        [Fact]
        public async Task B_a_mixed_collection_and_residual_plan_preserves_both_authoritative_amounts()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(setup, harness, [1]);
            var accepted = scenario.Accepted;
            var legs = accepted.MonetaryLegs();

            Assert.Equal(ChangeMonetaryOutcome.Mixed, accepted.MonetaryOutcome);
            Assert.Equal(ResidualCollect, accepted.AddCollect!.Amount);
            Assert.Equal(Residual, accepted.Residual!.Amount);
            Assert.Null(accepted.RefundDue);
            Assert.Equal(2, legs.Count);
            Assert.Single(legs, leg => leg.Kind == ExchangeMonetaryLegKind.Collection && leg.Amount == ResidualCollect);
            Assert.Single(legs, leg => leg.Kind == ExchangeMonetaryLegKind.Residual && leg.Amount == Residual);
            Assert.Equal(legs.Count, legs.Select(leg => leg.LegIdentity).Distinct().Count());
        }

        // ---------------------------------------------------------------- C, D. no netting

        [Fact]
        public async Task C_a_mixed_refund_exchange_collects_and_refunds_the_gross_amounts()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var guarantee = Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            var capture = Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            var refund = Assert.Single(harness.RefundValues.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(Collect, guarantee.Amount);
            Assert.Equal(Collect, capture.Amount);
            Assert.Equal(Refund, refund.ApprovedAmount);
            Assert.NotEqual(NettedRefund, guarantee.Amount);
            Assert.NotEqual(NettedRefund, capture.Amount);
            Assert.NotEqual(NettedRefund, refund.ApprovedAmount);
        }

        [Fact]
        public async Task D_a_mixed_residual_exchange_collects_and_issues_the_gross_amounts()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var capture = Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            var residual = Assert.Single(harness.ExchangeResiduals.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ResidualCollect, capture.Amount);
            Assert.Equal(Residual, residual.Amount);
            Assert.NotEqual(NettedResidual, capture.Amount);
            Assert.NotEqual(NettedResidual, residual.Amount);
        }

        // ---------------------------------------------------------------- E to K. rejected shapes

        [Theory]
        [InlineData("one-leg")]
        [InlineData("three-legs")]
        [InlineData("return-only")]
        [InlineData("both-returns")]
        [InlineData("zero-legs")]
        [InlineData("single-outcome-with-two-legs")]
        public async Task E_to_K_an_unsupported_mixed_shape_is_refused_before_any_external_mutation(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1], shapeAccepted: accepted => shape switch
            {
                "one-leg" => accepted with { RefundDue = null },
                "three-legs" => accepted with
                {
                    Residual = new AcceptedResidual(Residual, accepted.SaleCurrencyId, "ResidualCredit")
                },
                "return-only" => accepted with { AddCollect = null },
                "both-returns" => accepted with
                {
                    AddCollect = null,
                    Residual = new AcceptedResidual(Residual, accepted.SaleCurrencyId, "ResidualCredit")
                },
                "zero-legs" => accepted with { AddCollect = null, RefundDue = null },
                _ => accepted with { MonetaryOutcome = ChangeMonetaryOutcome.AddCollect }
            });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey())));

            Assert.Equal(20275, refusal.Code);
            await AssertNothingExternalAsync(harness, scenario, acceptCalls: 1);
        }

        [Theory]
        [InlineData("zero-collection")]
        [InlineData("negative-return")]
        [InlineData("foreign-collection-currency")]
        [InlineData("foreign-return-currency")]
        public async Task J_a_malformed_mixed_leg_is_refused_before_any_external_mutation(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1], shapeAccepted: accepted => shape switch
            {
                "zero-collection" => accepted with { AddCollect = new AcceptedAddCollect(0m, accepted.SaleCurrencyId) },
                "negative-return" => accepted with
                {
                    RefundDue = new AcceptedRefundDue(-Refund, accepted.SaleCurrencyId, AcceptedRefundDue.OriginalFormOfPayment)
                },
                "foreign-collection-currency" => accepted with
                {
                    AddCollect = new AcceptedAddCollect(Collect, accepted.SaleCurrencyId + 7)
                },
                _ => accepted with
                {
                    RefundDue = new AcceptedRefundDue(Refund, accepted.SaleCurrencyId + 7, AcceptedRefundDue.OriginalFormOfPayment)
                }
            });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey())));

            Assert.Equal(20275, refusal.Code);
            await AssertNothingExternalAsync(harness, scenario, acceptCalls: 1);
        }

        [Fact]
        public async Task H_I_a_monetary_leg_kind_can_never_appear_twice()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var refundScenario = await MixedRefundAsync(setup, harness, [1]);

            await using var residualSetup = NewHarness();
            await using var residualHarness = NewHarness();
            var residualScenario = await MixedResidualAsync(residualSetup, residualHarness, [1]);

            foreach (var accepted in new[] { refundScenario.Accepted, residualScenario.Accepted })
            {
                var legs = accepted.MonetaryLegs();

                Assert.Equal(legs.Count, legs.Select(leg => leg.Kind).Distinct().Count());
                Assert.Equal(legs.Count, legs.Select(leg => leg.LegIdentity).Distinct().Count());
                Assert.Single(legs, leg => leg.IsCollection);
                Assert.Single(legs, leg => leg.IsReturnOfValue);
            }
        }

        [Fact]
        public async Task L_a_stale_expected_version_dispatches_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(
                scenario.FundedExecution(NewKey()) with { ExpectedCommercialVersion = scenario.CommercialVersion + 5 }));

            Assert.Equal(20089, refusal.Code);
            await AssertNothingExternalAsync(harness, scenario, acceptCalls: 0);
        }

        // ---------------------------------------------------------------- M, N. collection and refund happy paths

        [Fact]
        public async Task M_a_fully_unused_mixed_refund_exchange_runs_protect_inventory_document_capture_refund()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);

            Assert.True(plan.IsFundingCaptured);
            Assert.True(plan.IsRefundDueSettled);
            Assert.True(plan.IsMonetarySettled);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.Equal(2, outcome.MonetaryLegs.Count);
            Assert.All(outcome.MonetaryLegs, leg => Assert.Equal(ExchangeMonetaryState.Settled, leg.State));

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(scenario.CustomerTotal + Collect - Refund, after.CustomerTotal);
        }

        [Fact]
        public async Task N_a_partially_used_mixed_refund_exchange_keeps_used_coupons_historical()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(
                setup, harness, [2], [1], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.Equal(
                scenario.CouponServiceIds[2],
                Assert.Single(Assert.Single(harness.ReservationChanges.ObservedApplies).Items).ReplacedOrderServiceId);
            Assert.Equal([2, 3], Assert.Single(harness.DocumentExchanges.ObservedRequests).Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.Single(harness.RefundValues.ObservedRequests);
        }

        // ---------------------------------------------------------------- O to S. stages before capture

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task O_an_unresolved_protection_stops_before_inventory_document_capture_and_refund(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            harness.ExchangeFunding.GuaranteeOutcome = unresolved;
            harness.ExchangeFunding.GuaranteeRecoveryOutcome = unresolved;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task P_an_unresolved_protection_that_resolves_on_read_back_protects_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.GuaranteeOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeFunding.GuaranteeRecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            harness.ExchangeFunding.GuaranteeRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task Q_an_unresolved_inventory_stops_before_document_capture_and_refund(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            harness.ReservationChanges.ApplyOutcome = unresolved;
            harness.ReservationChanges.RecoveryOutcome = unresolved;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
        }

        [Fact]
        public async Task Q_a_refused_inventory_releases_the_protection_and_never_refunds()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task R_an_unresolved_document_stops_before_capture_and_refund(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            harness.DocumentExchanges.ExchangeOutcome = unresolved;
            harness.DocumentExchanges.RecoveryOutcome = unresolved;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.RefundValues.ObservedRequests);
        }

        [Fact]
        public async Task S_a_document_that_resolves_on_read_back_captures_once_then_refunds_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;
            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        // ---------------------------------------------------------------- T to W. the capture gate

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task T_an_unresolved_capture_dispatches_no_refund(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = unresolved;
            harness.ExchangeFunding.CaptureRecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptureRecoveryKeys);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task U_a_capture_that_resolves_on_read_back_refunds_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        [Fact]
        public async Task V_a_refused_capture_needs_reconciliation_and_dispatches_no_refund()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Rejected;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.True(replay.RequiresReconciliation);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.True(plan.IsFundingCaptureRejected);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(predecessor.Exchanges);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
        }

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        public async Task W_a_contradictory_capture_needs_reconciliation_and_dispatches_no_refund(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            if (shape == "wrong-amount")
                harness.ExchangeFunding.CaptureAmountOverride = Collect - 1m;
            else
                harness.ExchangeFunding.CaptureCurrencyOverride = scenario.Accepted.SaleCurrencyId + 7;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.False(string.IsNullOrWhiteSpace(plan.FundingCaptureDetail));
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
        }

        // ---------------------------------------------------------------- X to AB. the refund leg

        [Fact]
        public async Task X_an_unresolved_refund_keeps_the_document_and_capture_confirmed()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Pending;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.True(plan.IsFundingCaptured);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.NotEmpty(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ExchangeMonetaryState.Pending, replay.MonetaryState);
            Assert.Equal(
                ExchangeMonetaryState.Settled,
                Assert.Single(replay.MonetaryLegs, leg => leg.IsCollectionLeg()).State);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task Y_an_unresolved_refund_that_resolves_on_read_back_finalizes_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Unknown;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task Z_AA_a_refused_or_contradictory_refund_needs_reconciliation_without_reversing_the_capture()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Rejected;

            var refused = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));
            var refusedPlan = (await harness.ExchangePlans.FindAsync(refused.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, refused.OperationStatus);
            Assert.True(refusedPlan.IsFundingCaptured);
            Assert.True(refusedPlan.IsRefundDueRejected);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Null(await FindTicketAsync(_fixture, refusedPlan.SuccessorElectronicTicketId));

            await using var contradictorySetup = NewHarness();
            await using var contradictory = NewHarness();
            var second = await MixedRefundAsync(contradictorySetup, contradictory, [1]);

            contradictory.RefundValues.AmountOverride = Refund + 1m;

            var mismatch = await contradictory.Exchange.ExchangeAsync(second.FundedExecution(NewKey()));
            var mismatchPlan = (await contradictory.ExchangePlans.FindAsync(mismatch.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, mismatch.OperationStatus);
            Assert.True(mismatchPlan.IsFundingCaptured);
            Assert.False(string.IsNullOrWhiteSpace(mismatchPlan.RefundDueDetail));
            Assert.Empty(contradictory.ExchangeFunding.ObservedReleases);
        }

        [Fact]
        public async Task AB_a_completed_mixed_replay_moves_no_money_and_creates_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.True(replay.IsReplay);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        // ---------------------------------------------------------------- AC to AO. the residual shape

        [Fact]
        public async Task AC_a_fully_unused_mixed_residual_exchange_runs_protect_inventory_document_capture_residual()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.True(plan.IsFundingCaptured);
            Assert.True(plan.IsResidualSettled);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.False(string.IsNullOrWhiteSpace(outcome.ResidualInstrumentReference));
            Assert.Equal(scenario.CustomerTotal + ResidualCollect - Residual, after.CustomerTotal);
        }

        [Fact]
        public async Task AD_a_partially_used_mixed_residual_exchange_keeps_used_coupons_historical()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(
                setup, harness, [2], [1], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
        }

        [Fact]
        public async Task AE_AF_AG_AH_an_unresolved_stage_never_reaches_the_residual_leg()
        {
            await using var protectionSetup = NewHarness();
            await using var protection = NewHarness();
            var protectionScenario = await MixedResidualAsync(protectionSetup, protection, [1]);

            protection.ExchangeFunding.GuaranteeOutcome = ProviderOperationOutcome.Pending;
            protection.ExchangeFunding.GuaranteeRecoveryOutcome = ProviderOperationOutcome.Pending;

            await protection.Exchange.ExchangeAsync(protectionScenario.FundedExecution(NewKey()));

            Assert.Empty(protection.ReservationChanges.ObservedApplies);
            Assert.Empty(protection.DocumentExchanges.ObservedRequests);
            Assert.Empty(protection.ExchangeFunding.ObservedCaptures);
            Assert.Empty(protection.ExchangeResiduals.ObservedRequests);

            await using var documentSetup = NewHarness();
            await using var document = NewHarness();
            var documentScenario = await MixedResidualAsync(documentSetup, document, [1]);

            document.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;
            document.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await document.Exchange.ExchangeAsync(documentScenario.FundedExecution(NewKey()));

            Assert.Empty(document.ExchangeFunding.ObservedCaptures);
            Assert.Empty(document.ExchangeResiduals.ObservedRequests);

            await using var captureSetup = NewHarness();
            await using var capture = NewHarness();
            var captureScenario = await MixedResidualAsync(captureSetup, capture, [1]);

            capture.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Pending;
            capture.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Pending;

            var pendingCapture = await capture.Exchange.ExchangeAsync(captureScenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, pendingCapture.OperationStatus);
            Assert.Empty(capture.ExchangeResiduals.ObservedRequests);

            await using var refusedSetup = NewHarness();
            await using var refused = NewHarness();
            var refusedScenario = await MixedResidualAsync(refusedSetup, refused, [1]);

            refused.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Rejected;

            var refusedCapture = await refused.Exchange.ExchangeAsync(refusedScenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, refusedCapture.OperationStatus);
            Assert.Empty(refused.ExchangeResiduals.ObservedRequests);
        }

        [Fact]
        public async Task AI_a_capture_confirmed_but_never_recorded_is_recovered_then_the_residual_runs_once()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}");
            var funding = new DeterministicExchangeFundingAdapter { ThrowAfterCaptureDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, funding);
            var scenario = await MixedResidualAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            Assert.Single(funding.ObservedCaptures);
            Assert.Empty(crashed.ExchangeResiduals.ObservedRequests);

            funding.ThrowAfterCaptureDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, funding);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(funding.ObservedCaptures);
            Assert.Single(funding.ObservedCaptureRecoveryKeys);
            Assert.Single(resumed.ExchangeResiduals.ObservedRequests);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task AJ_AK_an_unresolved_residual_holds_then_resolves_without_a_duplicate()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            var pending = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, pending.OperationStatus);
            Assert.False(pending.RequiresReconciliation);

            harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.False(string.IsNullOrWhiteSpace(finalized.ResidualInstrumentReference));
        }

        [Theory]
        [InlineData("rejected")]
        [InlineData("wrong-amount")]
        [InlineData("no-instrument")]
        public async Task AL_AM_AN_a_failed_residual_needs_reconciliation_without_reversing_the_capture(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(setup, harness, [1]);

            switch (shape)
            {
                case "rejected":
                    harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Rejected;
                    break;
                case "wrong-amount":
                    harness.ExchangeResiduals.AmountOverride = Residual + 2m;
                    break;
                default:
                    harness.ExchangeResiduals.OmitInstrumentReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(plan.IsFundingCaptured);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
        }

        [Fact]
        public async Task AO_a_completed_mixed_residual_replay_calls_no_provider()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedResidualAsync(setup, harness, [1]);
            var key = NewKey();

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.True(replay.IsReplay);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
        }

        // ---------------------------------------------------------------- AP to AT. crash boundaries

        [Fact]
        public async Task AP_a_protection_confirmed_but_never_recorded_is_recovered_without_a_duplicate()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}");
            var funding = new DeterministicExchangeFundingAdapter { ThrowAfterGuaranteeDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, funding);
            var scenario = await MixedRefundAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            funding.ThrowAfterGuaranteeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, funding);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(funding.ObservedGuarantees);
            Assert.Single(funding.ObservedGuaranteeRecoveryKeys);
            Assert.Single(funding.ObservedCaptures);
            Assert.Single(resumed.RefundValues.ObservedRequests);
        }

        [Fact]
        public async Task AQ_a_document_confirmed_but_never_recorded_yields_one_successor()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}");

            await using var setup = NewHarness();
            await using var crashed = NewHarness(caller);
            var scenario = await MixedRefundAsync(setup, crashed, [1]);
            var key = NewKey();

            crashed.DocumentExchanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            crashed.DocumentExchanges.ThrowAfterDispatch = false;
            crashed.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(crashed.DocumentExchanges.ObservedRequests);
            Assert.Single(crashed.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
            Assert.Single(crashed.ExchangeFunding.ObservedCaptures);
            Assert.Single(crashed.RefundValues.ObservedRequests);
        }

        [Fact]
        public async Task AR_a_capture_confirmed_but_never_recorded_runs_the_refund_once()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}");
            var funding = new DeterministicExchangeFundingAdapter { ThrowAfterCaptureDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, funding);
            var scenario = await MixedRefundAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            Assert.Empty(crashed.RefundValues.ObservedRequests);

            funding.ThrowAfterCaptureDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, funding);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(funding.ObservedCaptures);
            Assert.Single(resumed.RefundValues.ObservedRequests);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
        }

        [Fact]
        public async Task AS_a_refund_confirmed_but_never_recorded_pays_once()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}");
            var refunds = new DeterministicRefundValueAdapter { ThrowAfterDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, refundValues: refunds);
            var scenario = await MixedRefundAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            Assert.Single(refunds.ObservedRequests);

            refunds.ThrowAfterDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, refundValues: refunds);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(refunds.ObservedRequests);
            Assert.Single(refunds.ObservedRecoveryKeys);
            Assert.Empty(resumed.ExchangeFunding.ObservedCaptures);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task AT_a_residual_confirmed_but_never_recorded_issues_one_instrument()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}");
            var residuals = new DeterministicExchangeResidualAdapter { ThrowAfterDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, exchangeResiduals: residuals);
            var scenario = await MixedResidualAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            var dispatched = Assert.Single(residuals.ObservedRequests);

            residuals.ThrowAfterDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, exchangeResiduals: residuals);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var plan = (await resumed.ExchangePlans.FindAsync(finalized.OperationId))!;

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(residuals.ObservedRequests);
            Assert.Equal(dispatched.OperationKey, Assert.Single(residuals.ObservedRecoveryKeys));
            Assert.Equal($"INSTR-{plan.Successor!.DocumentNumber}", plan.ResidualInstrumentReference);
            Assert.Empty(resumed.ExchangeFunding.ObservedCaptures);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------------------------- AX. distinct leg identities

        [Fact]
        public async Task AX_every_monetary_leg_owns_a_distinct_economic_operation_key()
        {
            await using var refundSetup = NewHarness();
            await using var refundHarness = NewHarness();
            var refundScenario = await MixedRefundAsync(refundSetup, refundHarness, [1]);

            await refundHarness.Exchange.ExchangeAsync(refundScenario.FundedExecution(NewKey()));

            var guaranteeKey = Assert.Single(refundHarness.ExchangeFunding.ObservedGuarantees).OperationKey;
            var captureKey = Assert.Single(refundHarness.ExchangeFunding.ObservedCaptures).OperationKey;
            var refundKey = Assert.Single(refundHarness.RefundValues.ObservedRequests).OperationKey;

            await using var residualSetup = NewHarness();
            await using var residualHarness = NewHarness();
            var residualScenario = await MixedResidualAsync(residualSetup, residualHarness, [1]);

            await residualHarness.Exchange.ExchangeAsync(residualScenario.FundedExecution(NewKey()));

            var residualKey = Assert.Single(residualHarness.ExchangeResiduals.ObservedRequests).OperationKey;
            var residualCaptureKey = Assert.Single(residualHarness.ExchangeFunding.ObservedCaptures).OperationKey;

            Assert.Equal(3, new[] { guaranteeKey, captureKey, refundKey }.Distinct(StringComparer.Ordinal).Count());
            Assert.NotEqual(residualCaptureKey, residualKey);
            Assert.Contains("exchange-funding-guarantee", guaranteeKey);
            Assert.Contains("exchange-funding-capture", captureKey);
            Assert.Contains("exchange-refund-value", refundKey);
            Assert.Contains("exchange-residual", residualKey);
        }

        // ---------------------------------------------------------------- AY. repeated exchange

        [Fact]
        public async Task AY_a_repeated_mixed_exchange_settles_against_the_current_accountable_predecessor()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedRefundAsync(setup, harness, [1]);

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));
            var successorId = first.SuccessorElectronicTicketId!.Value;
            var successor = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reloaded = await ReloadAsync(_fixture, scenario.OrderId);
            IReadOnlyList<long> secondChanged = [successor.Coupons.First().CurrentOrderServiceId];

            ComposeMixed(
                harness, reloaded, secondChanged, ExchangeMonetaryLegKind.RefundDue, Collect, Refund, "EXC-QUOTE-2");

            await harness.Exchange.QuoteAsync(scenario.OrderId, secondChanged);

            var second = await harness.Exchange.ExchangeAsync(new ExchangeExecution(
                scenario.OrderId, secondChanged, "EXC-QUOTE-2", NewKey(), reloaded.CommercialVersion,
                ExchangeSourceFactory.FundingMethodRef));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var settled = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reissued = (await FindTicketAsync(_fixture, second.SuccessorElectronicTicketId!.Value))!;
            var refunds = harness.RefundValues.ObservedRequests;

            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.Equal(successorId, reissued.PredecessorElectronicTicketId);
            Assert.Equal(scenario.TicketId, settled.PredecessorElectronicTicketId);
            Assert.Equal(2, refunds.Count);
            Assert.Equal(predecessor.DocumentNumber, refunds[0].DocumentNumber);
            Assert.Equal(settled.DocumentNumber, refunds[1].DocumentNumber);
            Assert.NotEqual(refunds[0].OperationKey, refunds[1].OperationKey);
            Assert.Equal(2, harness.ExchangeFunding.ObservedCaptures.Count);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(settled.Exchanges);
            Assert.Equal(4, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        // ---------------------------------------------------------------- support

        private Task<ExchangeScenario> MixedRefundAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            int[] changedCouponNumbers,
            int[]? flownCouponNumbers = null,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
            => MixedAsync(
                _fixture, setup, harness, ExchangeMonetaryLegKind.RefundDue, changedCouponNumbers, flownCouponNumbers,
                Collect, Refund, shapeAccepted, createOrder);

        private Task<ExchangeScenario> MixedResidualAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            int[] changedCouponNumbers,
            int[]? flownCouponNumbers = null,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
            => MixedAsync(
                _fixture, setup, harness, ExchangeMonetaryLegKind.Residual, changedCouponNumbers, flownCouponNumbers,
                ResidualCollect, Residual, shapeAccepted, createOrder);

        private async Task AssertNothingExternalAsync(
            OrderSliceHarness harness,
            ExchangeScenario scenario,
            int acceptCalls)
        {
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(acceptCalls, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
        }

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AirlineUser(7401, $"exc-mixed-{Guid.NewGuid():N}"));
    }
}
