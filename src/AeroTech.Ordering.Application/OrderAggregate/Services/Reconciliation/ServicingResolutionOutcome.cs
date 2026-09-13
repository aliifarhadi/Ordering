using AeroTech.Ordering.Domain.Servicing.Reconciliation;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation
{
    public sealed record ServicingResolutionOutcome(
        ServicingManualResolution Resolution,
        bool IsReplay);
}
