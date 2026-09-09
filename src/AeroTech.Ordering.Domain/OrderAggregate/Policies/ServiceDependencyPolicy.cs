using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class ServiceDependencyPolicy
    {
        public static IReadOnlyCollection<long> DependenciesOf(OrderService service)
        {
            var dependencies = service.CoveredServices
                .Select(coverage => coverage.CoveredOrderServiceId)
                .ToList();

            if (service.SeatDetail is { } seat)
                dependencies.Add(seat.AssociatedAirOrderServiceId);

            if (service.LoungeDetail?.RelatedAirOrderServiceId is { } lounge)
                dependencies.Add(lounge);

            return dependencies.Distinct().ToList();
        }
    }
}
