using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
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

            Assert.All(boundary, type => Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name.Contains("TicketCoupon", StringComparison.Ordinal)));

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

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-id-{Guid.NewGuid():N}"));
    }
}
