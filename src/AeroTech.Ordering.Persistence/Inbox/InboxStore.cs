using AeroTech.Framework.Core.ServiceContracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Inbox
{
    public sealed class InboxStore : IInboxStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        public InboxStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default)
            => _dbContext.Set<InboxMessage>()
                .AnyAsync(message => message.MessageId == messageId && message.Consumer == consumer, cancellationToken);

        public async Task MarkProcessedAsync(Guid messageId, string consumer, string messageType, CancellationToken cancellationToken = default)
        {
            await _dbContext.Set<InboxMessage>().AddAsync(
                new InboxMessage
                {
                    MessageId = messageId,
                    Consumer = consumer,
                    MessageType = messageType,
                    ReceivedOn = _clock.GetDateTime()
                },
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
