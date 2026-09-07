using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder
{
    public sealed class IssueOrderCommandHandler : IRequestHandler<IssueOrderCommand, IssueOrderResult>
    {
        private readonly IOrderIssuanceService _issuanceService;

        public IssueOrderCommandHandler(IOrderIssuanceService issuanceService) => _issuanceService = issuanceService;

        public async Task<IssueOrderResult> Handle(IssueOrderCommand command, CancellationToken cancellationToken)
        {
            var outcome = await _issuanceService.IssueAsync(command.OrderId, cancellationToken);
            return new IssueOrderResult(outcome.OrderId, outcome.OrderStatus, outcome.Tasks.Select(task => task.TaskId).ToList());
        }
    }
}
