using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain._Shared.Documents;
using Microsoft.EntityFrameworkCore;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class DocumentExchangeIdentityTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public DocumentExchangeIdentityTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public void The_document_exchange_contract_carries_no_ordering_internal_coupon_identity()
        {
            var boundary = new[]
            {
                typeof(DocumentExchangeRequest),
                typeof(DocumentExchangeCouponRequest),
                typeof(DocumentExchangeEligibilityRequest),
                typeof(DocumentExchangeResult),
                typeof(DocumentExchangeRecovery),
                typeof(SuccessorDocumentIdentity),
                typeof(SuccessorCouponIdentity)
            };

            var localIdentities = new[] { "TicketCoupon", "OrderService", "OrderSegment", "JourneySegment", "PricingLine", "PricingAllocation" };

            Assert.All(boundary, type => Assert.All(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => Assert.DoesNotContain(
                    localIdentities,
                    local => property.Name.Contains(local, StringComparison.Ordinal))));

            Assert.Equal(
                new[] { "PredecessorCouponNumber", "Disposition", "Segment" },
                typeof(DocumentExchangeCouponRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(property => property.Name)
                    .ToArray());

            Assert.Equal(typeof(TicketedSegmentSnapshot), typeof(DocumentExchangeCouponRequest).GetProperty("Segment")!.PropertyType);
            Assert.Equal(typeof(int), typeof(DocumentExchangeCouponRequest).GetProperty("PredecessorCouponNumber")!.PropertyType);
            Assert.Equal(typeof(int), typeof(SuccessorCouponIdentity).GetProperty("PredecessorCouponNumber")!.PropertyType);
            Assert.Equal(typeof(int), typeof(SuccessorCouponIdentity).GetProperty("CouponNumber")!.PropertyType);
        }

        [Fact]
        public async Task The_document_host_receives_document_coupon_identity_and_answers_with_coupon_numbers()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var request = Assert.Single(harness.DocumentExchanges.ObservedRequests);
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var internalCouponIds = predecessor.Coupons.Select(coupon => coupon.Id).ToList();

            Assert.Equal(predecessor.DocumentNumber, request.PredecessorDocumentNumber);
            Assert.Equal(
                predecessor.Coupons.Select(coupon => coupon.CouponNumber).Order(),
                request.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.All(
                request.Coupons.Select(coupon => (long)coupon.PredecessorCouponNumber),
                number => Assert.DoesNotContain(number, internalCouponIds));

            foreach (var identity in plan.Successor!.Coupons)
            {
                var planned = Assert.Single(plan.Coupons, coupon => coupon.PredecessorCouponNumber == identity.PredecessorCouponNumber);
                var predecessorCoupon = predecessor.Coupons.Single(coupon => coupon.CouponNumber == identity.PredecessorCouponNumber);
                var successorCoupon = successor.Coupons.Single(coupon => coupon.CouponNumber == identity.CouponNumber);

                Assert.Equal(planned.PredecessorTicketCouponId, predecessorCoupon.Id);
                Assert.Equal(planned.SuccessorTicketCouponId, successorCoupon.Id);
                Assert.Equal(predecessorCoupon.Id, successorCoupon.PredecessorTicketCouponId);
                Assert.Equal(identity.CouponNumber, planned.SuccessorCouponNumber);
            }

            Assert.Equal(2, plan.Successor.Coupons.Count);
            Assert.Equal(2, successor.Coupons.Count);
        }

        [Theory]
        [InlineData("unknown-predecessor")]
        [InlineData("duplicate-predecessor")]
        [InlineData("duplicate-successor")]
        [InlineData("missing-coupon")]
        public async Task An_inconsistent_provider_coupon_mapping_fails_closed(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);

            switch (shape)
            {
                case "unknown-predecessor":
                    harness.DocumentExchanges.UnknownPredecessorCouponNumber = 9;
                    break;
                case "duplicate-predecessor":
                    harness.DocumentExchanges.DuplicatePredecessorCouponMapping = true;
                    break;
                case "duplicate-successor":
                    harness.DocumentExchanges.DuplicateSuccessorCouponNumber = true;
                    break;
                default:
                    harness.DocumentExchanges.OmitSuccessorCoupons = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Equal(2, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(ElectronicTicketStatus.Issued, predecessor.StatusSummary);
            Assert.All(predecessor.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(scenario.DocumentVersion, predecessor.DocumentVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task Each_coupon_request_carries_its_own_ticketed_segment_facts()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness, roundTrip: true, changedCouponNumbers: [1]);
            var order = await ReloadAsync(_fixture, scenario.OrderId);
            var continuedService = order.OrderServices.Single(service => service.Id == scenario.CouponServiceIds[2]);
            var continuedSegment = order.Segments.Single(segment => segment.Id == continuedService.SoldSegmentId);
            var replacement = scenario.Accepted.Coupons.Single(coupon => coupon.IsReplaced).Replacement!.Segment;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var request = Assert.Single(harness.DocumentExchanges.ObservedRequests);
            var replaced = Assert.Single(request.Coupons, coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced);
            var continued = Assert.Single(request.Coupons, coupon => coupon.Disposition == ExchangeCouponDisposition.Continued);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;

            Assert.Equal(
                new TicketedSegmentSnapshot(
                    replacement.MarketingAirlineId,
                    replacement.FlightNumber,
                    replacement.OriginAirportId,
                    replacement.DestinationAirportId,
                    replacement.DepartureAt,
                    replacement.ArrivalAt,
                    replacement.BookingClass),
                replaced.Segment);

            Assert.Equal(
                new TicketedSegmentSnapshot(
                    continuedSegment.MarketingAirlineId,
                    continuedSegment.Number,
                    continuedSegment.OriginAirportId,
                    continuedSegment.DestinationAirportId,
                    continuedSegment.DepartureDateTime,
                    continuedSegment.ArrivalDateTime,
                    continuedSegment.BookingClass),
                continued.Segment);

            Assert.NotEqual(replaced.Segment, continued.Segment);
            Assert.NotEqual(replaced.Segment.FlightNumber, continued.Segment.FlightNumber);

            foreach (var coupon in outcome.Coupons)
            {
                var successorCoupon = successor.Coupons.Single(candidate => candidate.Id == coupon.SuccessorTicketCouponId);
                var sent = request.Coupons.Single(candidate => candidate.PredecessorCouponNumber == coupon.PredecessorCouponNumber);

                Assert.Equal(sent.Segment.FlightNumber, successorCoupon.IssuedSegment.FlightNumber);
                Assert.Equal(sent.Segment.DepartureDateTime, successorCoupon.IssuedSegment.DepartureDateTime);
                Assert.Equal(coupon.OrderServiceId, successorCoupon.OrderServiceId);
                Assert.Equal(coupon.OrderServiceId, successorCoupon.CurrentOrderServiceId);
            }
        }

        [Fact]
        public async Task A_restart_rebuilds_the_same_document_request_from_the_plan_even_when_the_order_segment_moves()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-id-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await TicketedAsync(_fixture, setup, roundTrip: true, changedCouponNumbers: [1]);
            var key = NewKey();

            setup.DocumentExchanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Exchange.ExchangeAsync(scenario.Execution(key)));

            var before = Assert.Single(setup.DocumentExchanges.ObservedRequests);
            var plan = (await setup.ExchangePlans.FindAsync(harnessOperationId(setup)))!;

            await MoveContinuedSegmentAsync(scenario);

            await using var resume = NewHarness(caller);
            Register(resume, scenario);

            var outcome = await resume.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = Assert.Single(resume.DocumentExchanges.ObservedRequests);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;

            Assert.Equal(before.PredecessorDocumentNumber, after.PredecessorDocumentNumber);
            Assert.Equal(before.QuotedExchangeId, after.QuotedExchangeId);
            Assert.Equal(before.TargetSelectionRef, after.TargetSelectionRef);
            Assert.Equal(before.OperationKey, after.OperationKey);
            Assert.Equal(before.Coupons, after.Coupons);
            Assert.Equal(
                plan.Coupons.OrderBy(coupon => coupon.PredecessorCouponNumber).Select(coupon => coupon.TicketedSegment),
                after.Coupons.OrderBy(coupon => coupon.PredecessorCouponNumber).Select(coupon => coupon.Segment));
            Assert.DoesNotContain(MovedFlightNumber, after.Coupons.Select(coupon => coupon.Segment.FlightNumber));
            Assert.DoesNotContain(MovedFlightNumber, successor.Coupons.Select(coupon => coupon.IssuedSegment.FlightNumber));
            Assert.Empty(resume.ExchangeQuotes.ObservedSelections);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
        }

        [Fact]
        public async Task A_crash_after_durable_confirmation_finalizes_the_planned_segment_not_the_current_one()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-id-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await TicketedAsync(_fixture, setup, roundTrip: true, changedCouponNumbers: [1]);
            var key = NewKey();

            setup.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;

            var first = await setup.Exchange.ExchangeAsync(scenario.Execution(key));
            var planned = (await setup.ExchangePlans.FindAsync(first.OperationId))!.Coupons
                .ToDictionary(coupon => coupon.PredecessorCouponNumber, coupon => coupon.TicketedSegment);

            await setup.ExchangePlans.RecordDocumentExchangeOutcomeAsync(
                first.OperationId,
                ProviderOperationOutcome.Confirmed,
                "EXCH-RECOVERED",
                new SuccessorDocumentIdentity(
                    $"EXC{first.OperationId}", 1, null, DocumentAuthority.Local, null,
                    [new SuccessorCouponIdentity(1, 1), new SuccessorCouponIdentity(2, 2)]),
                null);
            await setup.UnitOfWork.SaveChangesAsync();

            await MoveContinuedSegmentAsync(scenario);

            await using var resume = NewHarness(caller);
            Register(resume, scenario);

            var finalized = await resume.Exchange.ExchangeAsync(scenario.Execution(key));
            var successor = (await FindTicketAsync(_fixture, finalized.SuccessorElectronicTicketId!.Value))!;

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Empty(resume.DocumentExchanges.ObservedRequests);
            Assert.Empty(resume.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Empty(resume.ReservationChanges.ObservedApplies);
            Assert.Equal(2, successor.Coupons.Count);

            foreach (var coupon in successor.Coupons)
            {
                var segment = planned[coupon.CouponNumber];

                Assert.Equal(segment.FlightNumber, coupon.IssuedSegment.FlightNumber);
                Assert.Equal(segment.DepartureDateTime, coupon.IssuedSegment.DepartureDateTime);
                Assert.NotEqual(MovedFlightNumber, coupon.IssuedSegment.FlightNumber);
            }
        }

        private const string MovedFlightNumber = "W5 9999";

        private static long harnessOperationId(OrderSliceHarness harness)
            => harness.ExchangeQuotes.ObservedSelections[^1].OperationId;

        private async Task MoveContinuedSegmentAsync(ExchangeScenario scenario)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                """
                UPDATE segment
                SET segment.[Number] = {0},
                    segment.[DepartureDateTime] = DATEADD(day, 30, segment.[DepartureDateTime]),
                    segment.[ArrivalDateTime] = DATEADD(day, 30, segment.[ArrivalDateTime])
                FROM [Order].[OrderSegments] segment
                INNER JOIN [Order].[OrderAirTransportServiceDetails] detail ON detail.[OrderSegmentId] = segment.[Id]
                WHERE detail.[OrderServiceId] = {1}
                """,
                MovedFlightNumber,
                scenario.CouponServiceIds[2]);
        }

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AirlineUser(7401, $"exc-id-{Guid.NewGuid():N}"));


    }
}
