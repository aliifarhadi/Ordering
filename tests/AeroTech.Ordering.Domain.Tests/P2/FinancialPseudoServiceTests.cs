using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class FinancialPseudoServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Theory]
        [InlineData(OrderServiceType.Penalty)]
        [InlineData(OrderServiceType.ServiceFee)]
        [InlineData(OrderServiceType.Credit)]
        [InlineData(OrderServiceType.Voucher)]
        [InlineData(OrderServiceType.TaxAdjustment)]
        [InlineData(OrderServiceType.ManualAdjustment)]
        [InlineData(OrderServiceType.Notification)]
        [InlineData(OrderServiceType.TransferRide)]
        public void A_financial_or_retired_pseudo_service_cannot_be_sold(OrderServiceType blocked)
        {
            var pseudo = AncillaryFactory.Service(
                $"PSEUDO-{blocked}",
                blocked,
                "PSEUDO",
                new AcceptedGenericServiceDetail("Priority", "1.0", """{"priorityKind":"Boarding"}"""),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, pseudo));

            Assert.Equal(2820, exception.Code);
        }

        [Fact]
        public void A_penalty_is_carried_by_pricing_not_by_a_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Contains(PricingComponentType.Penalty, Enum.GetValues<PricingComponentType>());
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.Penalty);
        }

        [Fact]
        public void A_fee_is_carried_by_pricing_not_by_a_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Contains(PricingComponentType.Fee, Enum.GetValues<PricingComponentType>());
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.ServiceFee);
        }

        [Fact]
        public void A_tax_adjustment_is_carried_by_pricing_not_by_a_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Contains(PricingComponentType.Adjustment, Enum.GetValues<PricingComponentType>());
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.TaxAdjustment);
        }

        [Fact]
        public void Blocking_a_pseudo_service_does_not_block_a_real_ancillary()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal(), AncillaryFactory.Baggage());

            Assert.Equal(6, order.OrderServices.Count);
        }

        [Fact]
        public void A_created_order_never_holds_a_blocked_service_type()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Seat(),
                AncillaryFactory.Meal(),
                AncillaryFactory.Hotel());

            var blocked = new[]
            {
                OrderServiceType.Penalty,
                OrderServiceType.ServiceFee,
                OrderServiceType.Credit,
                OrderServiceType.Voucher,
                OrderServiceType.TaxAdjustment,
                OrderServiceType.ManualAdjustment,
                OrderServiceType.Notification,
                OrderServiceType.TransferRide
            };

            Assert.All(order.OrderServices, service => Assert.DoesNotContain(service.ServiceType, blocked));
        }
    }
}
