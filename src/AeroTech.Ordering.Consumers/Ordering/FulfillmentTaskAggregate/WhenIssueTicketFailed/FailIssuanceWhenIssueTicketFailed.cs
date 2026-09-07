using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Messages.Ordering.Enums;
using MassTransit;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.FulfillmentTaskFailed;

namespace AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenIssueTicketFailed
{
    public sealed class FailIssuanceWhenIssueTicketFailed : IConsumer<IntegrationEvent>
    {
        private readonly IOrderIssuanceService _issuanceService;

        public FailIssuanceWhenIssueTicketFailed(IOrderIssuanceService issuanceService)
            => _issuanceService = issuanceService;

        public async Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            if (context.Message.TaskType != OrderFulfillmentTaskType.IssueTicket)
                return;

            await _issuanceService.FailIssuanceAsync(context.Message.OrderId, context.Message.Error, context.CancellationToken);
        }
    }
}
