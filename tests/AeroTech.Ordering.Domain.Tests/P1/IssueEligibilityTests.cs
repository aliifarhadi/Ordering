using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P1
{
    public sealed class IssueEligibilityTests
    {
        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void Confirmed_reservation_and_confirmed_funding_permit_issue()
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(order, Evidence(order, FundingCoverageOutcome.Confirmed));

            Assert.True(decision.IsAllowed);
            Assert.Equal(order.OrderServices.Count, decision.EffectiveScopeServiceIds.Count);
        }

        [Theory]
        [InlineData(FundingCoverageOutcome.Insufficient, EligibilityReasonCodes.FundingInsufficient)]
        [InlineData(FundingCoverageOutcome.Pending, EligibilityReasonCodes.FundingPending)]
        [InlineData(FundingCoverageOutcome.Unknown, EligibilityReasonCodes.FundingUnknown)]
        public void Funding_that_is_not_confirmed_never_authorizes_issue(FundingCoverageOutcome outcome, string reason)
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(order, Evidence(order, outcome));

            Assert.False(decision.IsAllowed);
            Assert.Contains(reason, decision.ReasonCodes);
        }

        [Fact]
        public void Unverified_funding_never_authorizes_issue()
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(order, Evidence(order, null));

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.FundingNotVerified, decision.ReasonCodes);
        }

        [Fact]
        public void Confirmed_funding_below_the_customer_total_is_insufficient()
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                Evidence(order, FundingCoverageOutcome.Confirmed) with { ConfirmedFundingAmount = order.Amount.GrandTotal - 1m });

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.FundingInsufficient, decision.ReasonCodes);
        }

        [Fact]
        public void A_waitlisted_or_rejected_reservation_blocks_issue()
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                Evidence(order, FundingCoverageOutcome.Confirmed) with { ConfirmedReservationServiceIds = [] });

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.ReservationNotConfirmed, decision.ReasonCodes);
        }

        [Fact]
        public void An_unknown_reservation_outcome_blocks_issue()
        {
            var order = ReservedOrder();
            var ids = order.OrderServices.Select(service => service.Id).ToList();

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                Evidence(order, FundingCoverageOutcome.Confirmed) with
                {
                    ConfirmedReservationServiceIds = [],
                    UnknownReservationServiceIds = ids
                });

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.ReservationUnknown, decision.ReasonCodes);
        }

        [Fact]
        public void A_missing_reservation_blocks_issue()
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                Evidence(order, FundingCoverageOutcome.Confirmed) with
                {
                    ConfirmedReservationServiceIds = [],
                    KnownReservationServiceIds = []
                });

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.ReservationMissing, decision.ReasonCodes);
        }

        [Fact]
        public void Unavailable_document_stock_blocks_issue()
        {
            var order = ReservedOrder();

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                Evidence(order, FundingCoverageOutcome.Confirmed) with { DocumentStockAvailable = false });

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.DocumentStockUnavailable, decision.ReasonCodes);
        }

        [Fact]
        public void An_already_documented_order_is_not_issued_again()
        {
            var order = ReservedOrder();
            var ids = order.OrderServices.Select(service => service.Id).ToList();

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                Evidence(order, FundingCoverageOutcome.Confirmed) with { AlreadyDocumentedServiceIds = ids });

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.TicketAlreadyIssued, decision.ReasonCodes);
        }

        [Fact]
        public void A_cancelled_order_is_never_issuable()
        {
            var order = ReservedOrder();
            order.WithdrawBeforeTicketing(
                order.OrderServices.Select(service => service.Id).ToList(),
                VoidReason.CustomerRequest,
                7,
                _ids,
                _clock);

            var decision = IssueEligibilityPolicy.Evaluate(order, Evidence(order, FundingCoverageOutcome.Confirmed));

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.OrderNotCommerciallyActive, decision.ReasonCodes);
        }

        [Fact]
        public void Withdrawal_is_refused_once_a_document_exists()
        {
            var order = ReservedOrder();

            var decision = WithdrawEligibilityPolicy.Evaluate(order, [order.OrderServices.First().Id]);

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.AlreadyIssued, decision.ReasonCodes);
        }

        [Fact]
        public void Reservation_is_refused_when_every_service_is_already_reserved()
        {
            var order = ReservedOrder();

            var decision = ReserveEligibilityPolicy.Evaluate(
                order,
                order.OrderServices.Select(service => service.Id).ToList());

            Assert.False(decision.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.AlreadyReserved, decision.ReasonCodes);
        }

        private Order ReservedOrder()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            order.ApplyReservationOutcome(
                order.OrderServices.Select(service => service.Id).ToList(),
                "PNR-1",
                null,
                _ids,
                _clock);

            return order;
        }

        private static IssueEvidence Evidence(Order order, FundingCoverageOutcome? outcome)
        {
            var ids = order.OrderServices.Select(service => service.Id).ToList();

            return new IssueEvidence(
                DocumentStockAvailable: true,
                FundingOutcome: outcome,
                ConfirmedFundingAmount: order.Amount.GrandTotal,
                ConfirmedReservationServiceIds: ids,
                KnownReservationServiceIds: ids,
                UnknownReservationServiceIds: [],
                AlreadyDocumentedServiceIds: []);
        }
    }
}
