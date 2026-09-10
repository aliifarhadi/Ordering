using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.RemoveOrderServices
{
    public sealed record RemoveOrderServicesCommand(
        long OrderId,
        IReadOnlyList<long> OrderServiceIds,
        string QuotedCancellationId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<ScopeCancellationOutcome>;
}
