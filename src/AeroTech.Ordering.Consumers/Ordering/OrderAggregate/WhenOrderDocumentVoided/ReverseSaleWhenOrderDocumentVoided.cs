using MassTransit;
using Microsoft.Extensions.Logging;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderDocumentVoided;

namespace AeroTech.Ordering.Consumers.Ordering.OrderAggregate.WhenOrderDocumentVoided
{
    public sealed class ReverseSaleWhenOrderDocumentVoided : IConsumer<IntegrationEvent>
    {
        private readonly ILogger<ReverseSaleWhenOrderDocumentVoided> _logger;

        public ReverseSaleWhenOrderDocumentVoided(ILogger<ReverseSaleWhenOrderDocumentVoided> logger)
            => _logger = logger;

        public Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            var @event = context.Message;

            _logger.LogInformation(
                "Accounting: reversing sale for voided document {DocumentNumber} (Id {DocumentId}) on order {OrderId}. Amount {ReversedAmount} currency {CurrencyId}. Reason {Reason}. Voided by {VoidedBy}.",
                @event.DocumentNumber,
                @event.DocumentId,
                @event.OrderId,
                @event.ReversedAmount,
                @event.CurrencyId,
                @event.Reason,
                @event.VoidedBy);

            return Task.CompletedTask;
        }
    }
}
