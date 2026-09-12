using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public bool RetainAncillaryResidual(IReadOnlyCollection<long> orderServiceIds, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(orderServiceIds);
            ArgumentNullException.ThrowIfNull(clock);

            var transitioned = false;

            foreach (var service in _orderServices.Where(service => orderServiceIds.Contains(service.Id)))
                transitioned |= service.MarkSupersededByRetainedResidual();

            if (!transitioned)
                return false;

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            return true;
        }
    }
}
