using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder.Backoffice
{
    public sealed record BackofficeIssueOrderCommand(
        long OrderId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<IssueOrderOutcome>;
}
