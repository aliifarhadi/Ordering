using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptQuotedChange
{
    public sealed record AcceptQuotedChangeCommand(
        long OrderId,
        long OrderServiceId,
        string QuotedChangeId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<VoluntaryChangeOutcome>;
}
