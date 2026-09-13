using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
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
    public sealed class ServicingFeeDocumentFlowTests
    {
        private const string PenaltyRef = "EXC:PENALTY";
        private const string DocumentRef = "FEE-DOC-1";
        private const string SecondDocumentRef = "FEE-DOC-2";
        private const string SourceRef = "FEE-SOURCE-1";
        private const string ReasonForIssuanceCode = "C";
        private const string ReasonForIssuanceSubCode = "98A";
        private const decimal PenaltyAmount = ExchangeSourceFactory.AddCollectAmount;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _ancillary;

        public ServicingFeeDocumentFlowTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _ancillary = $"M1{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
        }

        // ---------------------------------------------- 1-2. a fee or penalty alone documents nothing

        [Fact]
        public async Task G6D1_a_penalty_line_without_an_instruction_documents_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Empty(plan!.FeeDocuments);
            Assert.False(plan.RequiresFeeDocumentation);
            Assert.Empty(harness.MiscDocuments.Requests);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            // the penalty is still in the accepted economics, it simply is not documented
            Assert.Contains(
                (await ReloadAsync(_fixture, scenario.OrderId)).PricingLines,
                line => line.SourceLineRef == PenaltyRef);
        }

        [Fact]
        public async Task G6D2_a_fee_line_without_an_instruction_documents_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [1], shapeAccepted: AsServiceFeeComponent);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Empty(plan!.FeeDocuments);
            Assert.Empty(harness.MiscDocuments.Requests);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));
        }

        // ---------------------------------------------- 3-17. the documented penalty and everything it must not touch

        [Fact]
        public async Task G6D3_an_explicit_penalty_instruction_issues_one_standalone_fee_emd()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            var coupon = Assert.Single(document.Coupons);
            var accepted = plan!.FeeDocuments.Single();
            var penaltyLine = CommittedPenaltyLine(after, outcome);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            // 5-9. the document and coupon shape
            Assert.Equal(ElectronicMiscDocumentType.Standalone, document.Type);
            Assert.Equal(EmdCouponPurpose.Fee, coupon.Purpose);
            Assert.Null(coupon.OrderServiceId);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.Null(coupon.ExternalValueReference);
            Assert.False(document.IsAssociated);

            // 10-11. the price binding
            Assert.Equal(penaltyLine.Id, coupon.PricingLineId);
            var link = Assert.Single(document.PriceLinks);
            Assert.Equal(penaltyLine.Id, link.PricingLineId);
            Assert.Equal(PenaltyAmount, link.AttributedValue);
            Assert.Equal(coupon.Id, link.EmdCouponId);
            Assert.Null(link.AllocationId);

            // 16-17. the source facts round-trip exactly
            Assert.Equal(ReasonForIssuanceCode, document.ReasonForIssuanceCode);
            Assert.Equal(ReasonForIssuanceSubCode, coupon.ReasonForIssuanceSubCode);
            Assert.Equal(OrderSliceHarness.HomeAirlineId, document.IssuerCarrierId);
            Assert.Equal(before.CurrencyId, document.CurrencyId);
            Assert.Equal(PenaltyAmount, coupon.IssuanceValue);
            Assert.Equal(PenaltyAmount, document.IssuedTotal);

            // durable checkpoints
            Assert.Equal(document.Id, accepted.ElectronicMiscDocumentId);
            Assert.Equal(document.DocumentNumber, accepted.AllocatedDocumentNumber);
            Assert.Equal(ProviderOperationOutcome.Confirmed, accepted.IssuanceOutcome);
            Assert.NotNull(accepted.SettledAt);
            Assert.Equal($"EMD-{document.DocumentNumber}", document.ProviderReference);

            // the provider saw a standalone fee request with no service and no association
            var request = Assert.Single(harness.MiscDocuments.Requests);

            Assert.Equal(ElectronicMiscDocumentType.Standalone, request.EmdType);
            Assert.Equal(ReasonForIssuanceCode, request.ReasonForIssuanceCode);
            Assert.Equal(PenaltyAmount, request.TotalAmount);
            Assert.All(request.Coupons, candidate =>
            {
                Assert.Equal(EmdCouponPurpose.Fee, candidate.Purpose);
                Assert.Null(candidate.OrderServiceId);
                Assert.Null(candidate.AssociatedTicketDocumentNumber);
                Assert.Null(candidate.AssociatedTicketCouponNumber);
                Assert.Null(candidate.ExternalValueReference);
            });

            AssertNoNewEconomicConsequence(before, after, outcome, harness);
        }

        [Fact]
        public async Task G6D4_an_explicit_fee_instruction_issues_one_standalone_fee_emd()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture,
                setup,
                harness,
                [1],
                shapeAccepted: accepted => WithPenaltyDocument(AsServiceFeeComponent(accepted)));

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, document.Type);
            Assert.Equal(EmdCouponPurpose.Fee, Assert.Single(document.Coupons).Purpose);
        }

        [Fact]
        public async Task G6D18_an_explicit_tax_attribution_keeps_both_links_and_calculates_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [1], shapeAccepted: WithPenaltyAndTaxDocument);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            var coupon = Assert.Single(document.Coupons);
            var penalty = CommittedPenaltyLine(after, outcome);
            var tax = CommittedLine(after, outcome, TaxRef);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            // the coupon documents the sum the source declared, and Ordering derived none of it
            Assert.Equal(PenaltyAmount + TaxAmount, coupon.IssuanceValue);
            Assert.Equal(penalty.Id, coupon.PricingLineId);
            Assert.Equal(2, document.PriceLinks.Count);
            Assert.Equal(
                PenaltyAmount,
                document.PriceLinks.Single(link => link.PricingLineId == penalty.Id).AttributedValue);
            Assert.Equal(
                TaxAmount,
                document.PriceLinks.Single(link => link.PricingLineId == tax.Id).AttributedValue);
        }

        [Fact]
        public async Task G6D28_multiple_instructions_issue_one_document_each_deterministically()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [1], shapeAccepted: WithTwoDocuments);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var documents = (await AncillariesAsync(_fixture, scenario.OrderId))
                .OrderBy(document => document.DocumentNumber, StringComparer.Ordinal)
                .ToList();
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, documents.Count);
            Assert.Equal(2, harness.MiscDocuments.Requests.Count);
            Assert.Equal(2, plan!.FeeDocuments.Count);
            Assert.All(plan.FeeDocuments, document => Assert.NotNull(document.SettledAt));

            // one provider operation key and one allocated number per document reference
            Assert.Equal(2, harness.MiscDocuments.Requests.Select(request => request.OperationKey).Distinct().Count());
            Assert.Equal(2, harness.MiscDocuments.Requests.Select(request => request.DocumentNumber).Distinct().Count());
            Assert.Equal(
                [DocumentRef, SecondDocumentRef],
                plan.FeeDocuments.Select(document => document.DocumentReference).ToList());
        }

        // ---------------------------------------------- 29-38. provider outcomes, crash and replay

        [Fact]
        public async Task G6D30_a_pending_issuance_holds_the_reservation_and_writes_no_local_document()
            => await AssertUnresolvedIssuanceAsync(ProviderOperationOutcome.Pending);

        [Fact]
        public async Task G6D31_an_unknown_issuance_holds_the_reservation_and_writes_no_local_document()
            => await AssertUnresolvedIssuanceAsync(ProviderOperationOutcome.Unknown);

        [Fact]
        public async Task G6D32_a_rejected_issuance_after_ticket_truth_reconciles()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var accepted = plan!.FeeDocuments.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Rejected, accepted.IssuanceOutcome);
            Assert.Null(accepted.SettledAt);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            await AssertTicketTruthHeldAsync(scenario, outcome);
            AssertPricingTruthHeld(await ReloadAsync(_fixture, scenario.OrderId), outcome);
        }

        [Fact]
        public async Task G6D33_a_crash_before_issuance_recovers_on_resume_without_a_duplicate()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);
            var key = NewKey();

            harness.MiscDocuments.ThrowBeforeIssue = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            Assert.Empty(harness.MiscDocuments.Requests);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            await using var resumed = new OrderSliceHarness(_fixture, caller);
            Register(resumed, scenario);
            resumed.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            var plan = await resumed.ExchangePlans.FindAsync(outcome.OperationId);

            // the stock number was reserved before the call, so the resume reads back instead of issuing again
            Assert.Empty(resumed.MiscDocuments.Requests);
            Assert.Single(resumed.MiscDocuments.Recoveries);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(document.DocumentNumber, plan!.FeeDocuments.Single().AllocatedDocumentNumber);
            Assert.Equal(document.DocumentNumber, resumed.MiscDocuments.Recoveries[0].DocumentNumber);
        }

        [Fact]
        public async Task G6D34_a_crash_after_the_provider_side_effect_recovers_into_one_local_document()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);
            var key = NewKey();

            harness.MiscDocuments.ThrowAfterIssue = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            Assert.Single(harness.MiscDocuments.Requests);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            await using var resumed = new OrderSliceHarness(_fixture, caller);
            Register(resumed, scenario);
            resumed.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Empty(resumed.MiscDocuments.Requests);
            Assert.Single(resumed.MiscDocuments.Recoveries);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, document.Type);
        }

        [Fact]
        public async Task G6D35_replaying_a_completed_fee_document_repeats_nothing()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var afterFirst = await ReloadAsync(_fixture, scenario.OrderId);
            var documentAfterFirst = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));

            await using var replay = new OrderSliceHarness(_fixture, caller);
            Register(replay, scenario);

            var second = await replay.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var afterSecond = await ReloadAsync(_fixture, scenario.OrderId);
            var documentAfterSecond = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));

            Assert.Equal(ServicingOperationStatus.Completed, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.True(second.IsReplay);

            Assert.Empty(replay.MiscDocuments.Requests);
            Assert.Empty(replay.MiscDocuments.Recoveries);
            Assert.Equal(documentAfterFirst.Id, documentAfterSecond.Id);
            Assert.Equal(documentAfterFirst.DocumentVersion, documentAfterSecond.DocumentVersion);
            Assert.Equal(afterFirst.CommercialVersion, afterSecond.CommercialVersion);
            Assert.Equal(afterFirst.FinancialSequence, afterSecond.FinancialSequence);
        }

        // ---------------------------------------------- 39-45. mixed with the frozen dispositions

        [Fact]
        public async Task G6D39_a_fee_document_and_a_reassociation_both_settle()
            => await AssertSettlesBesideAsync(
                AncillaryExchangeDisposition.ReassociateExisting,
                async (fixture, scenario, harness) =>
                {
                    var ancillary = await AncillaryAsync(fixture, scenario.OrderId, _ancillary);

                    Assert.NotNull(ancillary.Coupons.Single().AssociatedTicketCouponId);
                    Assert.Single(harness.EmdAssociations.ObservedRequests);
                });

        [Fact]
        public async Task G6D40_a_fee_document_and_an_ancillary_refund_both_settle()
            => await AssertSettlesBesideAsync(
                AncillaryExchangeDisposition.Refund,
                async (fixture, scenario, harness) =>
                {
                    var ancillary = await AncillaryAsync(fixture, scenario.OrderId, _ancillary);

                    Assert.Equal(EmdCouponStatus.Refunded, ancillary.Coupons.Single().Status);
                    Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
                });

        [Fact]
        public async Task G6D41_a_fee_document_and_an_emd_exchange_both_settle()
            => await AssertSettlesBesideAsync(
                AncillaryExchangeDisposition.ExchangeToNewEmd,
                async (fixture, scenario, harness) =>
                {
                    var ancillary = await AncillaryAsync(fixture, scenario.OrderId, _ancillary);

                    Assert.Equal(EmdCouponStatus.Exchanged, ancillary.Coupons.Single().Status);
                    Assert.Single(harness.EmdExchanges.ObservedRequests);
                });

        [Fact]
        public async Task G6D42_a_fee_document_and_a_retention_both_settle()
            => await AssertSettlesBesideAsync(
                AncillaryExchangeDisposition.RetainAsResidual,
                async (fixture, scenario, harness) =>
                {
                    var ancillary = await AncillaryAsync(fixture, scenario.OrderId, _ancillary);

                    Assert.Equal(EmdCouponStatus.OpenForUse, ancillary.Coupons.Single().Status);
                    Assert.Null(ancillary.Coupons.Single().AssociatedTicketCouponId);
                    Assert.Empty(harness.EmdExchanges.ObservedRequests);
                    await Task.CompletedTask;
                });

        [Fact]
        public async Task G6D43_a_fee_document_and_an_ancillary_cancel_both_settle()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture,
                setup,
                harness,
                [1],
                shapeAccepted: WithPenaltyDocument,
                createOrder: async candidate =>
                {
                    var order = await candidate.CreateOrderAsync();

                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)),
                        candidate.Ids,
                        candidate.Clock);

                    await candidate.UnitOfWork.SaveChangesAsync();

                    return order;
                });

            var lounge = (await ReloadAsync(_fixture, scenario.OrderId))
                .OrderServices.Single(service => service.ServiceType == OrderServiceType.LoungeAccess);

            await AttachServiceAncillaryAsync(
                _fixture, setup, scenario.OrderId, _ancillary, [(scenario.CouponId, lounge.Id)]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Cancel;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var cancelled = await AncillaryAsync(_fixture, scenario.OrderId, _ancillary);
            var feeDocument = Assert.Single(
                (await AncillariesAsync(_fixture, scenario.OrderId))
                    .Where(document => document.Coupons.All(coupon => coupon.Purpose == EmdCouponPurpose.Fee
                                                                      && coupon.OrderServiceId is null)));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            // the fee document settled
            Assert.Equal(ElectronicMiscDocumentType.Standalone, feeDocument.Type);
            Assert.NotNull(plan!.FeeDocuments.Single().SettledAt);

            // and so did the cancel, on its own document
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, cancelled.StatusSummary);
            Assert.NotNull(plan.CancelGroups.Single().CancellationSettledAt);
            Assert.Equal(
                OrderServiceCommercialStatus.Cancelled,
                after.OrderServices.Single(service => service.Id == lounge.Id).CommercialStatus);
            Assert.Single(harness.DocumentVoids.ObservedVoidRequests);
        }

        [Fact]
        public async Task G6D44_a_fee_document_settles_and_a_manual_review_still_reconciles()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _ancillary, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var feeDocuments = (await AncillariesAsync(_fixture, scenario.OrderId))
                .Where(document => document.Type == ElectronicMiscDocumentType.Standalone)
                .ToList();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            // the fee document is never starved by the unresolved manual review
            Assert.Single(feeDocuments);
            Assert.NotNull(plan!.FeeDocuments.Single().SettledAt);
            Assert.True(plan.HasUnresolvedManualReview);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _ancillary)).Coupons.Single().Status);
        }

        [Fact]
        public async Task G6D45_a_pending_fee_document_beside_a_manual_review_awaits_and_never_reconciles_early()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _ancillary, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;
            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Pending;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            // provider uncertainty is not silently converted into a manual-review reconciliation
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Pending, plan!.FeeDocuments.Single().IssuanceOutcome);
            Assert.Null(plan.FeeDocuments.Single().SettledAt);
            Assert.Empty(
                (await AncillariesAsync(_fixture, scenario.OrderId))
                    .Where(document => document.Type == ElectronicMiscDocumentType.Standalone));
        }

        // ---------------------------------------------- 46-49. boundary guarantees

        [Fact]
        public async Task G6D46_a_fee_document_is_not_an_ancillary_disposition()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var order = await ReloadAsync(_fixture, scenario.OrderId);
            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            // 46/48. no disposition, no ancillary group, no ancillary provider rail
            Assert.Empty(plan!.Ancillaries);
            Assert.Empty(plan.ExchangeGroups);
            Assert.Empty(plan.CancelGroups);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);

            // 47. no synthetic order service anywhere
            Assert.All(document.Coupons, coupon => Assert.Null(coupon.OrderServiceId));
            Assert.DoesNotContain(
                order.OrderServices,
                service => service.ElectronicMiscDocumentId == document.Id);
            Assert.Equal(
                (await ReloadAsync(_fixture, scenario.OrderId)).OrderServices.Count,
                order.OrderServices.Count);

            // 49. never a value-carrying purpose
            Assert.All(document.Coupons, coupon => Assert.Equal(EmdCouponPurpose.Fee, coupon.Purpose));
            Assert.DoesNotContain(
                document.Coupons,
                coupon => coupon.Purpose is EmdCouponPurpose.ResidualValue or EmdCouponPurpose.Deposit);
        }

        // ---------------------------------------------- 19-27. the pre-ticket fail-closed matrix

        [Fact]
        public async Task G6D19_an_unknown_pricing_line_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20325, accepted => WithDocument(accepted, PrimaryCoupon("EXC:NOT-A-LINE", PenaltyAmount)));

        [Fact]
        public async Task G6D20_a_tax_only_primary_line_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20325,
                accepted => WithDocument(
                    WithTaxLine(accepted), PrimaryCoupon(TaxRef, TaxAmount)));

        [Fact]
        public async Task G6D24_a_currency_mismatch_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20325,
                accepted => WithDocument(
                    accepted,
                    PrimaryCoupon(PenaltyRef, PenaltyAmount),
                    currencyId: 999));

        [Fact]
        public async Task G6D25_a_conservation_mismatch_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20326,
                accepted => WithDocument(
                    accepted,
                    new AcceptedServicingFeeDocumentCoupon(
                        ReasonForIssuanceSubCode,
                        PenaltyRef,
                        PenaltyAmount,
                        [new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount - 1m)])));

        [Fact]
        public async Task G6D26_over_attributing_an_accepted_line_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20326,
                accepted => WithDocument(
                    accepted,
                    new AcceptedServicingFeeDocumentCoupon(
                        ReasonForIssuanceSubCode,
                        PenaltyRef,
                        PenaltyAmount + 1m,
                        [new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount + 1m)])));

        [Fact]
        public async Task G6D27_a_duplicate_document_reference_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20324,
                accepted => accepted with
                {
                    FeeDocuments =
                    [
                        Document(DocumentRef, PrimaryCoupon(PenaltyRef, PenaltyAmount), accepted.SaleCurrencyId),
                        Document(DocumentRef, PrimaryCoupon(PenaltyRef, PenaltyAmount), accepted.SaleCurrencyId)
                    ]
                });

        // ---------------------------------------------- shared assertions

        private async Task AssertUnresolvedIssuanceAsync(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            harness.MiscDocuments.Outcome = unresolved;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var accepted = plan!.FeeDocuments.Single();

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(unresolved, accepted.IssuanceOutcome);
            Assert.NotNull(accepted.AllocatedDocumentNumber);
            Assert.Null(accepted.SettledAt);
            Assert.Null(accepted.ElectronicMiscDocumentId);
            Assert.Single(harness.MiscDocuments.Requests);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            await AssertTicketTruthHeldAsync(scenario, outcome);
        }

        private async Task AssertSettlesBesideAsync(
            AncillaryExchangeDisposition disposition,
            Func<OrderingDatabaseFixture, ExchangeScenario, OrderSliceHarness, Task> assertAncillary)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _ancillary, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = disposition;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var feeDocument = Assert.Single(
                (await AncillariesAsync(_fixture, scenario.OrderId))
                    .Where(document => document.Type == ElectronicMiscDocumentType.Standalone
                                       && document.Coupons.All(coupon => coupon.Purpose == EmdCouponPurpose.Fee
                                                                         && coupon.OrderServiceId is null)));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.NotNull(plan!.FeeDocuments.Single().SettledAt);
            Assert.Equal(feeDocument.Id, plan.FeeDocuments.Single().ElectronicMiscDocumentId);
            Assert.True(plan.IsAncillarySettled);

            await assertAncillary(_fixture, scenario, harness);
        }

        private async Task AssertPreTicketRefusalAsync(
            int expectedCode,
            Func<AcceptedExchange, AcceptedExchange> malform)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: malform);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey())));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var order = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(expectedCode, refusal.Code);

            // no reservation change, no ticket exchange, no stock allocation, no provider call
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.MiscDocuments.Requests);
            Assert.Empty(harness.MiscDocuments.Recoveries);
            Assert.Empty(predecessor.Exchanges);
            Assert.NotEqual(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Equal(scenario.CommercialVersion, order.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, order.FinancialSequence);
        }

        private async Task AssertTicketTruthHeldAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
        {
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        private static void AssertNoNewEconomicConsequence(
            Order before,
            Order after,
            ExchangeOutcome outcome,
            OrderSliceHarness harness)
        {
            var change = Assert.Single(after.Changes.Where(candidate => candidate.OperationId == outcome.OperationId));

            // 13-14. the exchange's own change and price change set, and nothing G6 added
            Assert.Single(after.PriceConsequencesOf(change.Id));
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);

            // 15. no extra value movement of any kind
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
        }

        private static void AssertPricingTruthHeld(Order order, ExchangeOutcome outcome)
        {
            var change = Assert.Single(order.Changes.Where(candidate => candidate.OperationId == outcome.OperationId));

            Assert.Single(order.PriceConsequencesOf(change.Id));
            Assert.Contains(order.PricingLines, line => line.SourceLineRef == PenaltyRef);
        }

        // ---------------------------------------------- source shaping

        private const string TaxRef = "EXC:PENALTY-TAX";
        private const string ServiceFeeRef = "EXC:SERVICE-FEE";
        private const decimal TaxAmount = 45_000m;

        private static AcceptedExchange WithPenaltyDocument(AcceptedExchange accepted)
            => WithDocument(accepted, PrimaryCoupon(PenaltyRef, PenaltyAmount));

        private static AcceptedExchange WithPenaltyAndTaxDocument(AcceptedExchange accepted)
            => WithDocument(
                WithTaxLine(accepted),
                new AcceptedServicingFeeDocumentCoupon(
                    ReasonForIssuanceSubCode,
                    PenaltyRef,
                    PenaltyAmount + TaxAmount,
                    [
                        new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount),
                        new AcceptedServicingFeeAttribution(TaxRef, TaxAmount)
                    ]));

        private static AcceptedExchange WithTwoDocuments(AcceptedExchange accepted)
        {
            var split = accepted with
            {
                PricingLines = accepted.PricingLines
                    .Select(line => line.SourceLineRef == PenaltyRef
                        ? line with { SaleAmount = PenaltyAmount / 2m, OriginalAmount = PenaltyAmount / 2m }
                        : line)
                    .ToList()
            };

            split = split with
            {
                PricingLines =
                [
                    .. split.PricingLines,
                    new AcceptedExchangePricingLine(
                        PricingComponentType.Fee,
                        PricingEffect.CustomerBalance,
                        OrderPricingLineDirection.Debit,
                        PricingLineRole.Original,
                        PenaltyAmount / 2m,
                        split.SaleCurrencyId,
                        PenaltyAmount / 2m,
                        split.SaleCurrencyId,
                        PricingBasisType.Order,
                        RefundabilityRule.NonRefundable,
                        ServiceFeeRef,
                        Code: "SVC")
                ]
            };

            return split with
            {
                FeeDocuments =
                [
                    Document(DocumentRef, PrimaryCoupon(PenaltyRef, PenaltyAmount / 2m), split.SaleCurrencyId),
                    Document(
                        SecondDocumentRef, PrimaryCoupon(ServiceFeeRef, PenaltyAmount / 2m), split.SaleCurrencyId)
                ]
            };
        }

        private static AcceptedExchange WithDocument(
            AcceptedExchange accepted,
            AcceptedServicingFeeDocumentCoupon coupon,
            int? currencyId = null)
            => accepted with
            {
                FeeDocuments = [Document(DocumentRef, coupon, currencyId ?? accepted.SaleCurrencyId)]
            };

        private static AcceptedServicingFeeDocument Document(
            string documentReference,
            AcceptedServicingFeeDocumentCoupon coupon,
            int currencyId)
            => new(
                documentReference,
                SourceRef,
                OrderSliceHarness.HomeAirlineId,
                null,
                ReasonForIssuanceCode,
                currencyId,
                [coupon]);

        private static AcceptedServicingFeeDocumentCoupon PrimaryCoupon(string sourceLineRef, decimal amount)
            => new(
                ReasonForIssuanceSubCode,
                sourceLineRef,
                amount,
                [new AcceptedServicingFeeAttribution(sourceLineRef, amount)]);

        private static AcceptedExchange AsServiceFeeComponent(AcceptedExchange accepted)
            => ReshapePenalty(accepted, line => line with { ComponentType = PricingComponentType.Fee });

        private static AcceptedExchange ReshapePenalty(
            AcceptedExchange accepted,
            Func<AcceptedExchangePricingLine, AcceptedExchangePricingLine> shape)
            => accepted with
            {
                PricingLines = accepted.PricingLines
                    .Select(line => line.SourceLineRef == PenaltyRef ? shape(line) : line)
                    .ToList()
            };

        private static AcceptedExchange WithTaxLine(AcceptedExchange accepted)
            => accepted with
            {
                AddCollect = new AcceptedAddCollect(
                    accepted.AddCollect!.Amount + TaxAmount, accepted.SaleCurrencyId),
                PricingLines =
                [
                    .. accepted.PricingLines,
                    new AcceptedExchangePricingLine(
                        PricingComponentType.Tax,
                        PricingEffect.CustomerBalance,
                        OrderPricingLineDirection.Debit,
                        PricingLineRole.Original,
                        TaxAmount,
                        accepted.SaleCurrencyId,
                        TaxAmount,
                        accepted.SaleCurrencyId,
                        PricingBasisType.Order,
                        RefundabilityRule.NonRefundable,
                        TaxRef,
                        Code: "YQ")
                ]
            };

        private static OrderPricingLine CommittedPenaltyLine(Order order, ExchangeOutcome outcome)
            => CommittedLine(order, outcome, PenaltyRef);

        private static OrderPricingLine CommittedLine(Order order, ExchangeOutcome outcome, string sourceLineRef)
        {
            var change = order.Changes.Single(candidate => candidate.OperationId == outcome.OperationId);
            var changeSet = order.PriceConsequencesOf(change.Id).Single();

            return order.PricingLines.Single(line =>
                line.PriceChangeSetId == changeSet.Id && line.SourceLineRef == sourceLineRef);
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7439, $"feedoc-{Guid.NewGuid():N}");
    }
}
