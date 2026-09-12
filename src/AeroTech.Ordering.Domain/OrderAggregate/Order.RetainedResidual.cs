using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public AncillaryRetentionOutcome RetainAncillaryResidual(long? orderServiceId, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(clock);

            if (orderServiceId is not { } serviceId)
                return AncillaryRetentionOutcome.NothingToTransition;

            if (_orderServices.FirstOrDefault(service => service.Id == serviceId) is not { } service)
                return AncillaryRetentionOutcome.Conflicted(
                    $"order service {serviceId} does not belong to order {Id}");

            if (service.ServiceType == OrderServiceType.AirTransportation)
                return AncillaryRetentionOutcome.Conflicted(
                    $"order service {serviceId} carries air transportation and is not a retainable ancillary");

            if (service.RetainedResidualConflict() is { } conflict)
                return AncillaryRetentionOutcome.Conflicted(conflict);

            if (!service.MarkSupersededByRetainedResidual())
                return AncillaryRetentionOutcome.AlreadyApplied;

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            return AncillaryRetentionOutcome.Applied;
        }
    }
}
