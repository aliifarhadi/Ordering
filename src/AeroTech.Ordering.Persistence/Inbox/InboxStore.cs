using AeroTech.Framework.Core.ServiceContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AeroTech.Ordering.Persistence.Inbox
{
    public sealed class InboxStore : IInboxStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        private EntityEntry<InboxMessage>? _enlisted;

        public InboxStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default)
            => _dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .AnyAsync(message => message.MessageId == messageId && message.Consumer == consumer, cancellationToken);

        public async Task MarkProcessedAsync(Guid messageId, string consumer, string messageType, CancellationToken cancellationToken = default)
        {
            EnlistProcessed(messageId, consumer, messageType);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public void EnlistProcessed(Guid messageId, string consumer, string messageType)
            => _enlisted = _dbContext.Set<InboxMessage>().Add(
                new InboxMessage
                {
                    MessageId = messageId,
                    Consumer = consumer,
                    MessageType = messageType,
                    ReceivedOn = _clock.GetDateTime()
                });

        public async Task PersistProcessedAsync(CancellationToken cancellationToken = default)
        {
            if (_enlisted is { State: EntityState.Added })
                await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
