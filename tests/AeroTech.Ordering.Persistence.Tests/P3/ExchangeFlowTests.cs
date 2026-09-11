using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ExchangeFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public ExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A. quote

        [Fact]
        public async Task A1_A2_an_exchange_quote_is_free_of_side_effects()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var operationsBefore = await OperationCountAsync(scenario.OrderId);

            var quote = await harness.Exchange.QuoteAsync(scenario.OrderId, scenario.ChangedOrderServiceIds);

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ExchangeSourceFactory.QuoteId, quote.QuotedExchangeId);
            Assert.Equal(ChangeMonetaryOutcome.Even, quote.MonetaryOutcome);
            Assert.Equal(ExchangeSourceFactory.TargetRef, quote.TargetSelectionRef);
            Assert.Equal(scenario.TicketId, quote.PredecessorElectronicTicketId);
            Assert.Equal(scenario.ChangedOrderServiceIds, quote.ChangedOrderServiceIds);
            Assert.NotEmpty(quote.PricingLines);
            Assert.Single(quote.Coupons);

            Assert.Equal(operationsBefore, await OperationCountAsync(scenario.OrderId));
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.DocumentVersion, ticket.DocumentVersion);
            Assert.Empty(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.Id == scenario.TicketId);
        }

        [Fact]
        public async Task The_quote_request_correlates_predecessor_pricing_without_local_pricing_line_ids()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var order = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var request = Assert.Single(harness.ExchangeQuotes.ObservedQuoteRequests);
            var localIds = order.PricingLines.Select(line => line.Id.ToString()).ToList();

            Assert.Equal(predecessor.PriceLinks.Count, request.PredecessorPricing.Count);
            Assert.All(request.PredecessorPricing, evidence => Assert.StartsWith("XPL-", evidence.CorrelationRef));
            Assert.All(request.PredecessorPricing, evidence => Assert.DoesNotContain(localIds, id => evidence.CorrelationRef.Contains(id)));
            Assert.All(request.PredecessorPricing, evidence => Assert.False(string.IsNullOrWhiteSpace(evidence.SourceLineRef)));
            Assert.Equal(request.PredecessorPricing.Count, request.PredecessorPricing.Select(evidence => evidence.CorrelationRef).Distinct().Count());
            Assert.All(scenario.Accepted.PricingLines, line => Assert.Contains(request.PredecessorPricing, evidence => evidence.CorrelationRef == line.PredecessorCorrelationRef));

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var lines = after.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();

            Assert.All(lines, line => Assert.Contains(line.OriginalPricingLineId!.Value, predecessor.CarriedPricingLineIds()));
            Assert.Equal(request.PredecessorPricing.Count * 2, lines.Count);
        }

        // ---------------------------------------------------------------- happy path: J, K, L, §71

        [Fact]
        public async Task A_successful_even_exchange_issues_a_distinct_successor_with_full_lineage()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var predecessorBefore = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value);
            var couponOutcome = Assert.Single(outcome.Coupons);

            Assert.NotNull(successor);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(OrderChangeType.Exchange, outcome.CommercialResult);
            Assert.Equal(ServicingOperationKind.Exchange, outcome.TechnicalOperation);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.False(outcome.DeferredToExpandedExchange);
            Assert.False(outcome.IsReplay);

            Assert.Equal(predecessorBefore.Id, predecessor.Id);
            Assert.Equal(predecessorBefore.DocumentNumber, predecessor.DocumentNumber);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(scenario.DocumentVersion + 1, predecessor.DocumentVersion);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, Assert.Single(predecessor.Coupons).FinancialStatus);

            Assert.NotEqual(predecessor.Id, successor!.Id);
            Assert.NotEqual(predecessor.DocumentNumber, successor.DocumentNumber);
            Assert.Equal(ElectronicTicketStatus.Issued, successor.StatusSummary);
            Assert.Equal(1, successor.DocumentVersion);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);
            Assert.Equal(outcome.OperationId, successor.PredecessorExchangeOperationId);
            Assert.Equal(predecessor.TravelerId, successor.TravelerId);
            Assert.Equal(scenario.OrderId, successor.CurrentServicingOrderId);
            Assert.Equal(outcome.OperationId, successor.OperationId);

            var successorCoupon = Assert.Single(successor.Coupons);

            Assert.Equal(scenario.CouponId, successorCoupon.PredecessorTicketCouponId);
            Assert.Equal(couponOutcome.SuccessorTicketCouponId, successorCoupon.Id);
            Assert.Equal(1, successorCoupon.CouponNumber);
            Assert.Equal(couponOutcome.SuccessorCouponNumber, successorCoupon.CouponNumber);
            Assert.Equal(TicketCouponFinancialStatus.Open, successorCoupon.FinancialStatus);
            Assert.Equal(TicketCouponControlStatus.Local, successorCoupon.ControlStatus);
            Assert.Equal(couponOutcome.OrderServiceId, successorCoupon.OrderServiceId);
            Assert.Equal(couponOutcome.OrderServiceId, successorCoupon.CurrentOrderServiceId);
            Assert.Equal(ExchangeSourceFactory.SuccessorFareBasis, successorCoupon.FareBasisSnapshot);
            Assert.Equal(scenario.Accepted.Coupons.Single().Successor.IssuanceValue, successorCoupon.IssuanceValue);
            Assert.Equal(ExchangeSourceFactory.ReplacementFlightNumber, successorCoupon.IssuedSegment.FlightNumber);

            var record = Assert.Single(predecessor.Exchanges);

            Assert.Equal(successor.Id, record.SuccessorElectronicTicketId);
            Assert.Equal(successor.DocumentNumber, record.SuccessorDocumentNumber);
            Assert.Equal(outcome.OperationId, record.OperationId);
            Assert.Equal(ExchangeSourceFactory.QuoteId, record.QuotedExchangeId);
            Assert.Equal(ExchangeSourceFactory.TargetRef, record.TargetSelectionRef);
            Assert.Equal(ExchangeSourceFactory.PricingReference, record.SourcePricingReference);
            Assert.Equal(outcome.ProviderExchangeReference, record.ProviderReference);
            Assert.NotNull(record.ProviderReference);
            Assert.NotNull(record.ActorId);
            Assert.False(string.IsNullOrWhiteSpace(record.ActorScope));
            Assert.NotEqual(default, record.ExchangedAt);

            var mapping = Assert.Single(record.Coupons);

            Assert.Equal(scenario.CouponId, mapping.PredecessorTicketCouponId);
            Assert.Equal(1, mapping.PredecessorCouponNumber);
            Assert.Equal(successorCoupon.Id, mapping.SuccessorTicketCouponId);
            Assert.Equal(1, mapping.SuccessorCouponNumber);
            Assert.Equal(scenario.ServiceId, mapping.PreviousOrderServiceId);
            Assert.Equal(couponOutcome.OrderServiceId, mapping.SuccessorOrderServiceId);

            Assert.Equal(scenario.TicketId, outcome.PredecessorElectronicTicketId);
            Assert.Equal(predecessor.DocumentNumber, outcome.PredecessorDocumentNumber);
            Assert.Equal(predecessor.DocumentVersion, outcome.PredecessorDocumentVersion);
            Assert.Equal(scenario.CouponId, couponOutcome.PredecessorTicketCouponId);
            Assert.Equal(successor.DocumentNumber, outcome.SuccessorDocumentNumber);
            Assert.Equal(1, outcome.SuccessorDocumentVersion);
            Assert.Equal(scenario.ServiceId, couponOutcome.ReplacedOrderServiceId);
            Assert.Equal(ExchangeCouponDisposition.Replaced, couponOutcome.Disposition);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.ReservationChangeOutcome);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.DocumentExchangeOutcome);
            Assert.Equal(DocumentExchangeEligibilityOutcome.Eligible, outcome.EligibilityOutcome);
        }

        [Fact]
        public async Task K_the_old_service_records_exchange_history_and_the_replacement_points_at_the_successor()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var untouchedIds = before.OrderServices.Where(service => service.Id != scenario.ServiceId).Select(service => service.Id).ToList();
            var statusBefore = before.OrderServices.Single(service => service.Id == scenario.ServiceId).Status;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var replacementId = Assert.Single(outcome.Coupons).OrderServiceId;
            var replaced = after.OrderServices.Single(service => service.Id == scenario.ServiceId);
            var replacement = after.OrderServices.Single(service => service.Id == replacementId);

            Assert.Equal(OrderServiceDocumentStatus.Exchanged, replaced.DocumentStatus);
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, replaced.CommercialStatus);
            Assert.NotEqual(OrderServiceFinancialStatus.Refunded, replaced.FinancialStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Voided, replaced.DocumentStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Refunded, replaced.DocumentStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Cancelled, replaced.DocumentStatus);
            Assert.Equal(scenario.TicketId, replaced.ElectronicTicketId);
            Assert.Equal(scenario.CouponId, replaced.TicketCouponId);
            Assert.Equal(OrderServiceStatus.Cancelled, replaced.Status);

            Assert.NotEqual(scenario.ServiceId, replacement.Id);
            Assert.Equal(OrderServiceDocumentStatus.Issued, replacement.DocumentStatus);
            Assert.Equal(outcome.SuccessorElectronicTicketId, replacement.ElectronicTicketId);
            Assert.Equal(Assert.Single(outcome.Coupons).SuccessorTicketCouponId, replacement.TicketCouponId);
            Assert.Equal(replaced.OrderItemId, replacement.OrderItemId);
            Assert.True(replacement.IsAirTransport);
            Assert.Equal(statusBefore, replacement.Status);
            Assert.NotEqual(OrderServiceStatus.Cancelled, replacement.Status);
            Assert.Equal(
                replaced.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).Order(),
                replacement.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).Order());

            var replacementSegment = after.Segments.Single(segment => segment.Id == replacement.SoldSegmentId);

            Assert.Equal(ExchangeSourceFactory.ReplacementFlightNumber, replacementSegment.Number);
            Assert.Equal(ExchangeSourceFactory.ReplacementBookingClass, replacementSegment.BookingClass);

            foreach (var untouched in untouchedIds)
                Assert.Contains(after.OrderServices, service => service.Id == untouched);
        }

        [Fact]
        public async Task L_the_exchange_appends_exactly_one_transfer_history_with_zero_customer_effect()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var eventsBefore = harness.Events.Dispatched.OfType<OrderPricingChanged>().Count();

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.ChangeType == OrderChangeType.Exchange);
            var changeSet = Assert.Single(after.PriceChangeSets, candidate => candidate.ChangeId == change.Id);
            var lines = after.PricingLines.Where(line => line.PriceChangeSetId == changeSet.Id).ToList();
            var carried = (await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).CarriedPricingLineIds();

            Assert.Equal(outcome.OperationId, change.OperationId);
            Assert.Equal(change.Id, outcome.OrderChangeId);
            Assert.Equal(changeSet.Id, outcome.PriceChangeSetId);
            Assert.Equal(PriceChangeReason.Exchange, changeSet.Reason);
            Assert.Equal(PricingSource.PricingEngine, changeSet.Source);
            Assert.Equal(ExchangeSourceFactory.PricingReference, changeSet.SourcePricingRef);
            Assert.Equal(scenario.FinancialSequence + 1, changeSet.FinancialSequence);
            Assert.Equal(scenario.FinancialSequence + 1, after.FinancialSequence);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);
            Assert.Equal(after.CommercialVersion, outcome.CommercialVersion);
            Assert.Equal(after.FinancialSequence, outcome.FinancialSequence);

            Assert.Equal(scenario.Accepted.PricingLines.Count, lines.Count);
            Assert.All(lines, line => Assert.Equal(PricingLineRole.Transfer, line.LineRole));
            Assert.All(lines, line => Assert.Equal(ExchangeSourceFactory.TransferGroup, line.TransferGroupId));
            Assert.All(lines, line => Assert.Contains(line.OriginalPricingLineId!.Value, carried));
            Assert.Equal(0m, lines.Where(line => line.Effect == PricingEffect.CustomerBalance).Sum(line => line.SignedSaleAmount));
            Assert.DoesNotContain(after.PriceChangeSets, set => set.Source == PricingSource.OrderingDerived && set.ChangeId == change.Id);

            Assert.Equal(eventsBefore + 1, harness.Events.Dispatched.OfType<OrderPricingChanged>().Count());
        }

        [Fact]
        public async Task L16_L18_successor_price_links_bind_to_the_new_transfer_lines_by_source_identity()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var predecessorBefore = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var linksBefore = predecessorBefore.PriceLinks.Select(link => (link.Id, link.PricingLineId, link.AttributedValue)).OrderBy(link => link.Id).ToList();

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var successorCoupon = Assert.Single(successor.Coupons);
            var exchangeLines = after.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();
            var attributions = scenario.Accepted.Coupons.Single().Successor.PriceLinks;

            Assert.Equal(attributions.Count, successor.PriceLinks.Count);

            foreach (var attribution in attributions)
            {
                var line = Assert.Single(exchangeLines, candidate => candidate.SourceLineRef == attribution.SourceLineRef);
                var link = Assert.Single(successor.PriceLinks, candidate => candidate.PricingLineId == line.Id);

                Assert.Equal(OrderPricingLineDirection.Debit, line.Direction);
                Assert.Equal(PricingLineRole.Transfer, line.LineRole);
                Assert.Equal(attribution.AttributedValue, link.AttributedValue);
                Assert.Equal(successorCoupon.Id, link.CouponId);
                Assert.Null(link.AllocationId);
                Assert.DoesNotContain(link.PricingLineId, predecessorBefore.CarriedPricingLineIds());
            }

            Assert.Equal(
                linksBefore,
                predecessor.PriceLinks.Select(link => (link.Id, link.PricingLineId, link.AttributedValue)).OrderBy(link => link.Id).ToList());
        }

        [Fact]
        public async Task J13_the_predecessor_document_history_is_immutable()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var before = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var couponBefore = Assert.Single(before.Coupons);

            await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var couponAfter = Assert.Single(after.Coupons);

            Assert.Equal(before.DocumentNumber, after.DocumentNumber);
            Assert.Equal(before.IssuedAt, after.IssuedAt);
            Assert.Equal(before.IssuedTotal, after.IssuedTotal);
            Assert.Equal(before.IssuerCarrierId, after.IssuerCarrierId);
            Assert.Equal(couponBefore.Id, couponAfter.Id);
            Assert.Equal(couponBefore.IssuedSegment, couponAfter.IssuedSegment);
            Assert.Equal(couponBefore.FareBasisSnapshot, couponAfter.FareBasisSnapshot);
            Assert.Equal(couponBefore.IssuanceValue, couponAfter.IssuanceValue);
            Assert.Equal(couponBefore.CurrentOrderServiceId, couponAfter.CurrentOrderServiceId);
            Assert.Equal(before.PriceLinks.Count, after.PriceLinks.Count);
            Assert.Empty(after.Refunds);
            Assert.Empty(after.RefundCorrections);
            Assert.Empty(after.Revalidations);
            Assert.Null(after.VoidRecord);
        }

        // ---------------------------------------------------------------- B. deterministic preflight

        [Fact]
        public async Task B1_a_stale_expected_version_fails_before_acceptance()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()) with { ExpectedCommercialVersion = scenario.CommercialVersion + 5 }));

            Assert.Equal(20089, refusal.Code);
            await AssertNothingHappenedAsync(harness, scenario);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task B2_a_used_coupon_on_the_changed_service_is_reported_as_not_exchangeable()
        {
            await using var setup = NewHarness();
            var scenario = await TicketedAsync(_fixture, setup);

            await SetCouponAsync(scenario.CouponId, "FinancialStatus", (int)TicketCouponFinancialStatus.Used);

            await using var harness = NewHarness();
            Register(harness, scenario);

            var quoteRefusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.QuoteAsync(scenario.OrderId, scenario.ChangedOrderServiceIds));
            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20292, quoteRefusal.Code);
            Assert.Equal(20292, refusal.Code);
            Assert.Empty(harness.ExchangeQuotes.ObservedQuoteRequests);
            await AssertNothingHappenedAsync(harness, scenario);
        }

        [Theory]
        [InlineData("empty", 20289)]
        [InlineData("duplicate", 20289)]
        [InlineData("two-documents", 20290)]
        public async Task B3_an_invalid_changed_scope_fails_before_acceptance(string shape, int code)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true);
            var other = (await TicketsAsync(_fixture, scenario.OrderId)).Single(ticket => ticket.Id != scenario.TicketId);
            IReadOnlyList<long> changed = shape switch
            {
                "empty" => [],
                "duplicate" => [scenario.ServiceId, scenario.ServiceId],
                _ => [scenario.ServiceId, other.Coupons.First().CurrentOrderServiceId]
            };

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()) with { ChangedOrderServiceIds = changed }));

            Assert.Equal(code, refusal.Code);
            await AssertNothingHappenedAsync(harness, scenario);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task B4_a_coupon_under_external_control_fails_before_acceptance()
        {
            await using var setup = NewHarness();
            var scenario = await TicketedAsync(_fixture, setup);

            await SetCouponAsync(scenario.CouponId, "ControlStatus", (int)TicketCouponControlStatus.External);

            await using var harness = NewHarness();
            Register(harness, scenario);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20285, refusal.Code);
            await AssertNothingHappenedAsync(harness, scenario);
        }

        [Fact]
        public async Task B5_an_active_dependent_ancillary_fails_before_acceptance()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();
            var created = await harness.CreateOneWayOrderAsync();

            created.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(created)), harness.Ids, harness.Clock);
            await harness.UnitOfWork.SaveChangesAsync();
            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            var order = await ReloadAsync(_fixture, created.Id);
            var ticket = (await TicketsAsync(_fixture, order.Id)).OrderBy(candidate => candidate.Id).First();
            var changed = new[] { ticket.Coupons.Single().CurrentOrderServiceId };

            Compose(harness, order, changed);

            var quoteRefusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.QuoteAsync(order.Id, changed));
            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(
                new Application.OrderAggregate.Services.Exchange.ExchangeExecution(order.Id, changed, ExchangeSourceFactory.QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(20258, quoteRefusal.Code);
            Assert.Equal(20258, refusal.Code);
            Assert.Empty(harness.ExchangeQuotes.ObservedQuoteRequests);
            Assert.Empty(harness.ExchangeQuotes.ObservedSelections);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty((await TicketAsync(_fixture, order.Id, ticket.Id)).Exchanges);
        }

        [Fact]
        public async Task B6_an_associated_misc_document_fails_before_acceptance()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var order = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            var associated = ElectronicMiscDocument.Issue(
                harness.Ids.NewId(),
                order.Id,
                ticket.TravelerId,
                harness.Ids.NewId(),
                $"M{harness.Ids.NewId() % 1_000_000:D6}",
                ElectronicMiscDocumentType.Associated,
                "A",
                OrderSliceHarness.HomeAirlineId,
                null,
                DocumentAuthority.Local,
                order.CurrencyId,
                [new EmdCouponIssuance(EmdCouponPurpose.Fee, "0DF", 50_000m, [], PricingLineId: order.PricingLines.First().Id, AssociatedTicketCouponId: scenario.CouponId)],
                harness.Ids,
                harness.Clock);

            await harness.MiscDocumentRepository.AddAsync(associated);
            await harness.UnitOfWork.SaveChangesAsync();

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20272, refusal.Code);
            await AssertNothingHappenedAsync(harness, scenario);
        }

        // ---------------------------------------------------------------- C. acceptance binding and pricing consistency

        [Fact]
        public async Task C2_an_accepted_result_naming_another_service_fails_before_inventory_and_replays_terminally()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var order = await ReloadAsync(_fixture, scenario.OrderId);
            var other = order.OrderServices.First(service => service.Id != scenario.ServiceId).Id;
            var key = NewKey();

            harness.ExchangeQuotes.Reshape(ExchangeSourceFactory.QuoteId, accepted => accepted with { ChangedOrderServiceIds = [other] });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(20273, refusal.Code);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);

            var replay = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(20273, replay.Code);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task C3_an_expired_accepted_result_fails_before_inventory()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, accepted => accepted with { ExpiresAt = DateTimeOffset.UnixEpoch });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20274, refusal.Code);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1);
        }

        [Fact]
        public async Task C4_an_unsupported_monetary_outcome_is_deferred_before_inventory_and_releases_the_claim()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, accepted => accepted with { MonetaryOutcome = (ChangeMonetaryOutcome)99 });
            var key = NewKey();

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.True(outcome.DeferredToExpandedExchange);
            Assert.Equal("99", outcome.DeferralReason);
            Assert.Equal((ChangeMonetaryOutcome)99, outcome.MonetaryOutcome);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.True(replay.DeferredToExpandedExchange);
            Assert.True(replay.IsReplay);
            Assert.Equal(outcome.OperationId, replay.OperationId);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Theory]
        [InlineData("penalty")]
        [InlineData("unbalanced")]
        public async Task C4_C5_an_unsupported_monetary_shape_is_deferred_before_inventory(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, accepted => shape switch
            {
                "penalty" => accepted with { PricingLines = [.. accepted.PricingLines, ExchangeSourceFactory.PenaltyLine(accepted.SaleCurrencyId)] },
                _ => accepted with { PricingLines = accepted.PricingLines.Where(line => line.Direction == OrderPricingLineDirection.Debit).ToList() }
            });

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.True(outcome.DeferredToExpandedExchange);
            Assert.Equal(shape == "penalty" ? nameof(PricingComponentType.Penalty) : "NonZeroCustomerBalance", outcome.DeferralReason);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1);
        }

        [Theory]
        [InlineData("no-transfer", 20275)]
        [InlineData("no-group", 20275)]
        [InlineData("duplicate-ref", 20275)]
        [InlineData("ordering-derived", 20275)]
        [InlineData("unresolved-attribution", 20278)]
        [InlineData("foreign-lineage", 20277)]
        public async Task C5_C6_malformed_or_foreign_pricing_fails_before_inventory(string shape, int code)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, accepted => Malformed(accepted, shape));

            if (shape == "foreign-lineage")
            {
                var foreignTicket = (await TicketsAsync(_fixture, scenario.OrderId)).Single(ticket => ticket.Id != scenario.TicketId);
                var foreign = ExchangePricingCorrelation.CorrelationRef(foreignTicket.Id, foreignTicket.CarriedPricingLineIds().First());

                harness.ExchangeQuotes.Reshape(ExchangeSourceFactory.QuoteId, accepted => accepted with
                {
                    PricingLines = accepted.PricingLines
                        .Select((line, index) => index == 0 ? line with { PredecessorCorrelationRef = foreign } : line)
                        .ToList()
                });
            }

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(code, refusal.Code);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1);
        }

        [Fact]
        public async Task A_replaced_coupon_without_an_accepted_replacement_is_rejected_deterministically_and_replays()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, accepted => accepted with
            {
                Coupons = accepted.Coupons.Select(coupon => coupon with { Replacement = null }).ToList()
            });
            var key = NewKey();

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));
            var operationId = harness.ExchangeQuotes.ObservedSelections.Single().OperationId;
            var plan = await harness.ExchangePlans.FindAsync(operationId);

            Assert.Equal(20275, refusal.Code);
            Assert.NotNull(plan);
            Assert.Equal(AcceptedExchangeDisposition.Rejected, plan!.Disposition);
            Assert.Equal(20275, plan.RejectionCode);
            Assert.Equal(ExchangeCouponDisposition.Replaced, Assert.Single(plan.Coupons).Disposition);
            Assert.Null(Assert.Single(plan.Coupons).ReplacementOrderServiceId);
            Assert.Null(Assert.Single(plan.Coupons).ReplacementOrderSegmentId);
            Assert.True(Assert.Single(plan.Coupons).TicketedSegment.IsComplete);

            var replay = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(refusal.Code, replay.Code);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- D. durable plan

        [Fact]
        public async Task D1_D7_the_plan_and_its_preallocated_identities_are_durable_before_inventory_and_survive_replay()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            var operationId = harness.ExchangeQuotes.ObservedSelections.Single().OperationId;
            var plan = await harness.ExchangePlans.FindAsync(operationId);
            var planCoupon = Assert.Single(plan!.Coupons);

            Assert.Equal(ExchangeSourceFactory.QuoteId, plan.QuotedExchangeId);
            Assert.Equal(scenario.ChangedOrderServiceIds, plan.ChangedOrderServiceIds);
            Assert.Equal(scenario.TicketId, plan.PredecessorElectronicTicketId);
            Assert.Equal(scenario.CouponId, planCoupon.PredecessorTicketCouponId);
            Assert.Equal(scenario.ServiceId, planCoupon.PredecessorOrderServiceId);
            Assert.Equal(ExchangeCouponDisposition.Replaced, planCoupon.Disposition);
            Assert.Equal(PricingSource.PricingEngine, plan.PricingSource);
            Assert.Equal(DocumentExchangeEligibilityOutcome.Eligible, plan.EligibilityOutcome);
            Assert.NotEqual(0, planCoupon.ReplacementOrderServiceId);
            Assert.NotEqual(0, planCoupon.ReplacementOrderSegmentId);
            Assert.NotEqual(0, plan.SuccessorElectronicTicketId);
            Assert.NotEqual(0, planCoupon.SuccessorTicketCouponId);
            Assert.Equal(AcceptedExchangeDisposition.Executable, plan.Disposition);
            Assert.Empty(harness.ReservationChanges.DispatchedKeys);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.ReservationChanges.ThrowBeforeDispatch = false;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var successor = (await FindTicketAsync(_fixture, recovered.SuccessorElectronicTicketId!.Value))!;
            var item = harness.ReservationChanges.ObservedApplies[^1].Items.Single();

            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(2, harness.ReservationChanges.ObservedApplies.Count);
            Assert.Equal(planCoupon.ReplacementOrderServiceId, item.ReplacementOrderServiceId);
            Assert.Equal(planCoupon.ReplacementOrderSegmentId, item.ReplacementOrderSegmentId);
            Assert.Equal(planCoupon.ReplacementOrderServiceId, Assert.Single(recovered.Coupons).OrderServiceId);
            Assert.Equal(plan.SuccessorElectronicTicketId, successor.Id);
            Assert.Equal(planCoupon.SuccessorTicketCouponId, Assert.Single(successor.Coupons).Id);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
        }

        // ---------------------------------------------------------------- E. document eligibility

        [Fact]
        public async Task E1_E6_a_denied_eligibility_persists_terminally_and_never_reaches_inventory()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.EligibilityOutcome = DocumentExchangeEligibilityOutcome.Denied;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var plan = await harness.ExchangePlans.FindAsync(first.OperationId);

            Assert.Equal(ServicingOperationStatus.Rejected, first.OperationStatus);
            Assert.Equal(ExchangeDocumentOutcome.Denied, first.DocumentOutcome);
            Assert.Equal(DocumentExchangeEligibilityOutcome.Denied, plan!.EligibilityOutcome);
            Assert.True(plan.IsEligibilityTerminal);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ExchangeDocumentOutcome.Denied, replay.DocumentOutcome);
            Assert.Single(harness.DocumentExchanges.ObservedEligibilityRequests);
            await AssertNothingHappenedAsync(harness, scenario, acceptCalls: 1, eligibilityCalls: 1);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task E2_E4_pending_evidence_holds_before_inventory_then_persists_eligible_before_the_first_apply()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.EligibilityOutcome = DocumentExchangeEligibilityOutcome.PendingEvidence;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var pendingPlan = await harness.ExchangePlans.FindAsync(first.OperationId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ExchangeDocumentOutcome.Pending, first.DocumentOutcome);
            Assert.Equal(DocumentExchangeEligibilityOutcome.PendingEvidence, pendingPlan!.EligibilityOutcome);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            var second = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(first.OperationId, second.OperationId);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Equal(2, harness.DocumentExchanges.ObservedEligibilityRequests.Count);
            Assert.Equal(
                harness.DocumentExchanges.ObservedEligibilityRequests[0].OperationKey,
                harness.DocumentExchanges.ObservedEligibilityRequests[1].OperationKey);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);

            harness.DocumentExchanges.EligibilityOutcome = DocumentExchangeEligibilityOutcome.Eligible;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var plan = await harness.ExchangePlans.FindAsync(first.OperationId);
            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies).Items.Single();
            var planCoupon = Assert.Single(pendingPlan.Coupons);

            Assert.Equal(DocumentExchangeEligibilityOutcome.Eligible, plan!.EligibilityOutcome);
            Assert.Equal(planCoupon.ReplacementOrderServiceId, applied.ReplacementOrderServiceId);
            Assert.Equal(planCoupon.ReplacementOrderSegmentId, applied.ReplacementOrderSegmentId);
            Assert.Equal(pendingPlan.SuccessorElectronicTicketId, resumed.SuccessorElectronicTicketId);
            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
        }

        [Fact]
        public async Task E5_a_durable_eligible_replay_never_re_evaluates_eligibility()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var eligibilityCalls = harness.DocumentExchanges.ObservedEligibilityRequests.Count;

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(1, eligibilityCalls);
            Assert.Equal(eligibilityCalls, harness.DocumentExchanges.ObservedEligibilityRequests.Count);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
        }

        [Fact]
        public async Task Inventory_and_the_document_host_receive_only_the_accepted_scope_under_plan_level_keys()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);
            var item = Assert.Single(applied.Items);
            var replacementId = Assert.Single(outcome.Coupons).OrderServiceId;

            Assert.Equal(scenario.ServiceId, item.ReplacedOrderServiceId);
            Assert.Equal(replacementId, item.ReplacementOrderServiceId);
            Assert.Equal(ExchangeSourceFactory.ReplacementCapacityReference, item.ReplacementFlightCapacityId);
            Assert.Equal(ExchangeSourceFactory.ReplacementBookingClass, item.ReplacementBookingClass);
            Assert.Equal((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).TravelerId, item.TravelerId);
            Assert.Equal($"exchange-reservation:{scenario.TicketId}:{outcome.OperationId}", applied.OperationKey);

            var exchanged = Assert.Single(harness.DocumentExchanges.ObservedRequests);
            var couponRequest = Assert.Single(exchanged.Coupons);

            Assert.Equal($"document-exchange:{scenario.TicketId}:{outcome.OperationId}", exchanged.OperationKey);
            Assert.Equal(ExchangeSourceFactory.QuoteId, exchanged.QuotedExchangeId);
            Assert.Equal(ExchangeSourceFactory.TargetRef, exchanged.TargetSelectionRef);
            Assert.Equal(ExchangeSourceFactory.PricingReference, exchanged.SourcePricingReference);
            Assert.Equal(outcome.PredecessorDocumentNumber, exchanged.PredecessorDocumentNumber);
            Assert.Equal(1, couponRequest.PredecessorCouponNumber);
            Assert.DoesNotContain(scenario.CouponId.ToString(), string.Join('|', exchanged.Coupons.Select(coupon => coupon.PredecessorCouponNumber)));
            Assert.Equal(ExchangeCouponDisposition.Replaced, couponRequest.Disposition);
            Assert.Equal(ExchangeSourceFactory.ReplacementFlightNumber, couponRequest.Segment.FlightNumber);
            Assert.Equal(ExchangeSourceFactory.ReplacementBookingClass, couponRequest.Segment.BookingClass);
            Assert.True(couponRequest.Segment.IsComplete);
            Assert.NotEqual(0, replacementId);

            var selection = Assert.Single(harness.ExchangeQuotes.ObservedSelections);

            Assert.Equal($"exchange-quote:{outcome.OperationId}", selection.OperationKey);
            Assert.Equal(scenario.ChangedOrderServiceIds, selection.ChangedOrderServiceIds);
        }

        // ---------------------------------------------------------------- support

        private static AcceptedExchange Malformed(AcceptedExchange accepted, string shape)
        {
            var coupon = accepted.Coupons.Single();

            return shape switch
            {
                "no-transfer" => accepted with
                {
                    PricingLines = accepted.PricingLines.Select(line => line with { LineRole = PricingLineRole.Original, TransferGroupId = null, PredecessorCorrelationRef = null }).ToList()
                },
                "no-group" => accepted with
                {
                    PricingLines = accepted.PricingLines.Select(line => line with { TransferGroupId = null }).ToList()
                },
                "duplicate-ref" => accepted with
                {
                    PricingLines = accepted.PricingLines.Select(line => line with { SourceLineRef = "EXC:SAME" }).ToList(),
                    Coupons = [coupon with { Successor = coupon.Successor with { PriceLinks = [new SuccessorDocumentPriceLink("EXC:SAME", 1m, accepted.SaleCurrencyId)] } }]
                },
                "ordering-derived" => accepted with { PricingSource = PricingSource.OrderingDerived },
                "unresolved-attribution" => accepted with
                {
                    Coupons = [coupon with { Successor = coupon.Successor with { PriceLinks = [.. coupon.Successor.PriceLinks, new SuccessorDocumentPriceLink("EXC:MISSING", 1m, accepted.SaleCurrencyId)] } }]
                },
                _ => accepted
            };
        }

        private async Task AssertNothingHappenedAsync(
            OrderSliceHarness harness,
            ExchangeScenario scenario,
            int acceptCalls = 0,
            int eligibilityCalls = 0)
        {
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(acceptCalls, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Equal(eligibilityCalls, harness.DocumentExchanges.ObservedEligibilityRequests.Count);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Issued, ticket.StatusSummary);
            Assert.Equal(scenario.DocumentVersion, ticket.DocumentVersion);

            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.Id == scenario.TicketId);
            Assert.All(await TicketsAsync(_fixture, scenario.OrderId), candidate => Assert.Null(candidate.PredecessorElectronicTicketId));
        }

        private async Task SetCouponAsync(long couponId, string column, int value)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                $"UPDATE [Order].[TicketCoupons] SET [{column}] = {{0}} WHERE [Id] = {{1}}", value, couponId);
        }

        private async Task<int> OperationCountAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Set<ServicingOperation>().CountAsync(operation => operation.OrderId == orderId);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-{Guid.NewGuid():N}"));
    }
}
