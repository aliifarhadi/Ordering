using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder.Backoffice
{
    public sealed class BackofficeIssueOrderCommandHandler : IRequestHandler<BackofficeIssueOrderCommand, IssueOrderOutcome>
    {
        private readonly IIssueOrderService _issueOrderService;

        public BackofficeIssueOrderCommandHandler(IIssueOrderService issueOrderService) => _issueOrderService = issueOrderService;

        public Task<IssueOrderOutcome> Handle(BackofficeIssueOrderCommand command, CancellationToken cancellationToken)
            => _issueOrderService.IssueAsync(command.OrderId, command.IdempotencyKey, command.ExpectedCommercialVersion, cancellationToken);
    }
}
