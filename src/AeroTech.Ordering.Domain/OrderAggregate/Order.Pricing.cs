using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        private IReadOnlyList<PricingLineSnapshot> BuildPricingLines(OrderPricingReason? reason = null)
        {
            var lines = new List<PricingLineSnapshot>();

            foreach (var line in _pricingLines)
            {
                if (reason is not null && line.LineReason != reason)
                    continue;

                var (trafficDocumentId, documentCouponId) = ResolveDocumentLink(line);

                lines.Add(new PricingLineSnapshot(
                    line.Id,
                    line.OriginalPricingLineId,
                    line.Amount,
                    line.CurrencyId,
                    line.EquivalentAmount,
                    line.ExchangeRate?.RateOfExchange,
                    line.ExchangeRate?.NumberOfDecimalPlaces,
                    line.ExchangeRate?.RateOfExchangeId,
                    line.ExchangeRate?.RoundingFactor,
                    line.LineCategory,
                    line.LineDirection,
                    line.Code ?? string.Empty,
                    line.Description,
                    line.Reference,
                    trafficDocumentId,
                    documentCouponId));
            }

            return lines;
        }

        private static decimal NetOf(IEnumerable<PricingLineSnapshot> lines) =>
            lines.Where(line => line.Direction == OrderPricingLineDirection.Credit).Sum(line => line.EquivalentAmount)
          - lines.Where(line => line.Direction == OrderPricingLineDirection.Debit).Sum(line => line.EquivalentAmount);

        private (long? TrafficDocumentId, long? DocumentCouponId) ResolveDocumentLink(OrderPricingLine line)
        {
            var serviceIds = line.Allocations
                .Where(allocation => allocation.OrderServiceId.HasValue)
                .Select(allocation => allocation.OrderServiceId!.Value)
                .Distinct()
                .ToList();

            if (serviceIds.Count != 1)
                return (null, null);

            var service = _orderServices.FirstOrDefault(orderService => orderService.Id == serviceIds[0]);

            return (service?.TrafficDocumentId, service?.DocumentCouponId);
        }
    }
}
