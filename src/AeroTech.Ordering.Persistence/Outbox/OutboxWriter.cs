using System.Text.Json;
using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Persistence.Outbox
{
    public sealed class OutboxWriter : IOutboxWriter
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;
        private readonly IIdentityService _identityService;
        private readonly IntegrationEventOptions _options;

        public OutboxWriter(
            OrderingDbContext dbContext,
            IClock clock,
            IIdentityService identityService,
            IOptions<IntegrationEventOptions> options)
        {
            _dbContext = dbContext;
            _clock = clock;
            _identityService = identityService;
            _options = options.Value;
        }

        public Task WriteAsync(object message, IDomainEvent source, CancellationToken cancellationToken = default)
        {
            var type = message.GetType();

            if (message is BaseIntegrationEvent integrationEvent)
            {
                integrationEvent.EventId = source.EventId;
                integrationEvent.AggregateId = source.AggregateId;
                integrationEvent.TimeOfOccurrence = source.TimeOfOccurrence;
                integrationEvent.TenantId = _options.TenantId;
                integrationEvent.SourceSystem = _options.SourceSystem;
                integrationEvent.Actor = _identityService.CurrentUserId?.ToString();
            }

            _dbContext.OutboxMessages.Add(new OutboxMessage
            {
                MessageType = $"{type.FullName}, {type.Assembly.GetName().Name}",
                Payload = JsonSerializer.Serialize(message, type),
                OccurredOn = _clock.GetDateTime()
            });

            return Task.CompletedTask;
        }
    }
}
