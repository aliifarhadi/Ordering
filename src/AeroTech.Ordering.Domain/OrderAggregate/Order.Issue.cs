using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void RequestIssue()
        {
            EnsureCanIssue();
            TransitionTo(OrderStatus.Ticketing);
        }

        private void EnsureCanIssue()
        {
            if (Status != OrderStatus.Paid)
                throw ExceptionFactory.OrderCannotBeIssued(Id, Status);
        }

        public void CompleteIssue(IReadOnlyCollection<IssuedServiceLink> issuedServices, IIdGenerator idGenerator, IClock clock)
        {
            var linksByService = issuedServices.ToDictionary(link => link.OrderServiceId);

            foreach (var service in _orderServices)
                if (linksByService.TryGetValue(service.Id, out var link))
                    service.MarkIssued(link.TrafficDocumentId, link.DocumentCouponId);

            TimeToLive = null;

            TransitionTo(OrderStatus.Ticketed);

            var issuedAt = clock.GetDateTime();

            Causes(new OrderIssued(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                issuedAt,
                Id,
                AirlineOfficeId,
                RecordLocator?.Value,
                UniqueIdentifierId,
                CommercialVersion,
                NextEventOrdinal(),
                Status,
                Type,
                Channel,
                CustomerId,
                issuedAt,
                Amount.GrandTotal,
                CurrencyId,
                BuildPricingLines()));
        }

        public void FailIssue(string reason, IIdGenerator idGenerator, IClock clock)
        {
            TransitionTo(OrderStatus.TicketingFailed);

            Causes(new OrderIssueFailed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                Status,
                reason));
        }

        public void MarkTicketingUnconfirmed(string detail, IIdGenerator idGenerator, IClock clock)
        {
            TransitionTo(OrderStatus.TicketingUnconfirmed);

            Causes(new OrderTicketingUnconfirmed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                Status,
                detail));
        }

        public OrderIssuancePlan BuildIssuancePlan()
        {
            var travellerPlans = _orderServices
                .OfType<OrderAirTransportService>()
                .Where(service => service.RequiresFulfillment)
                .GroupBy(service => service.TravellerId)
                .Select(group =>
                {
                    var coupons = group
                        .Select(service => new ServiceCouponPlan(service.Id, service.OrderSegmentId, ComputeServiceAmounts(service.Id)))
                        .ToList();

                    return new TravellerTicketPlan(group.Key, SumAmounts(coupons.Select(coupon => coupon.Amounts)), coupons);
                })
                .ToList();

            return new OrderIssuancePlan(travellerPlans);
        }

        private TicketAmountBreakdown ComputeServiceAmounts(long serviceId)
        {
            var allocations = _pricingLines
                .SelectMany(line => line.Allocations
                    .Where(allocation => allocation.OrderServiceId == serviceId)
                    .Select(allocation => new { line.LineCategory, line.LineDirection, allocation.EquivalentAmount, allocation.EquivalentCurrencyId }))
                .ToList();

            decimal Net(params OrderPricingLineCategory[] categories)
            {
                var scope = categories.Length == 0
                    ? allocations
                    : allocations.Where(entry => categories.Contains(entry.LineCategory)).ToList();

                return scope.Where(entry => entry.LineDirection == OrderPricingLineDirection.Credit).Sum(entry => entry.EquivalentAmount)
                     - scope.Where(entry => entry.LineDirection == OrderPricingLineDirection.Debit).Sum(entry => entry.EquivalentAmount);
            }

            var currencyId = allocations.Select(entry => entry.EquivalentCurrencyId).DefaultIfEmpty(CurrencyId).First();

            return new TicketAmountBreakdown(
                Net(OrderPricingLineCategory.Fare),
                Net(OrderPricingLineCategory.Tax),
                Net(OrderPricingLineCategory.Fee),
                0m,
                Net(),
                currencyId);
        }

        private TicketAmountBreakdown SumAmounts(IEnumerable<TicketAmountBreakdown> amounts)
        {
            var list = amounts.ToList();

            return new TicketAmountBreakdown(
                list.Sum(amount => amount.Fare),
                list.Sum(amount => amount.TaxesTotal),
                list.Sum(amount => amount.FeesTotal),
                list.Sum(amount => amount.Commission),
                list.Sum(amount => amount.TotalAmount),
                list.Select(amount => amount.CurrencyId).DefaultIfEmpty(CurrencyId).First());
        }
    }
}
