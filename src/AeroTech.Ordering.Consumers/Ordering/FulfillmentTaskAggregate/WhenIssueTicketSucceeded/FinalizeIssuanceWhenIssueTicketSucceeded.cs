using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Messages.Ordering.Enums;
using MassTransit;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.FulfillmentTaskSucceeded;

namespace AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenIssueTicketSucceeded
{
    public sealed class FinalizeIssuanceWhenIssueTicketSucceeded : IConsumer<IntegrationEvent>
    {
        private readonly IOrderIssuanceService _issuanceService;

        public FinalizeIssuanceWhenIssueTicketSucceeded(IOrderIssuanceService issuanceService)
            => _issuanceService = issuanceService;

        public async Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            if (context.Message.TaskType != OrderFulfillmentTaskType.IssueTicket)
                return;

            await _issuanceService.FinalizeIssuanceAsync(context.Message.OrderId, context.CancellationToken);
        }
    }
}
