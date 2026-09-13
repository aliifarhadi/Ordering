using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public AncillaryCancellationOutcome CancelAncillariesByDocumentVoid(
            IReadOnlyCollection<long> orderServiceIds,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(orderServiceIds);
            ArgumentNullException.ThrowIfNull(clock);

            if (orderServiceIds.Count == 0)
                return AncillaryCancellationOutcome.NothingToTransition;

            var targets = new List<OrderService>();

            foreach (var serviceId in orderServiceIds.Distinct())
            {
                if (_orderServices.FirstOrDefault(service => service.Id == serviceId) is not { } service)
                    return AncillaryCancellationOutcome.Conflicted(
                        $"order service {serviceId} does not belong to order {Id}");

                if (service.ServiceType == OrderServiceType.AirTransportation)
                    return AncillaryCancellationOutcome.Conflicted(
                        $"order service {serviceId} carries air transportation and is not a cancellable ancillary");

                if (service.AncillaryCancellationConflict() is { } conflict)
                    return AncillaryCancellationOutcome.Conflicted(conflict);

                targets.Add(service);
            }

            foreach (var service in targets)
                service.MarkCancelledByAncillaryDocumentVoid();

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            return AncillaryCancellationOutcome.Applied;
        }
    }
}
