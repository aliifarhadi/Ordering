using AeroTech.Framework.Core.ServiceContracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Framework.Infrastructure.Persistence
{
    public abstract class CommandDbContext : DbContext
    {
        private readonly IIdentityService _identityService;
        private readonly IClock _clock;
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        protected CommandDbContext(
            DbContextOptions options,
            IIdentityService identityService,
            IClock clock,
            IDomainEventDispatcher domainEventDispatcher)
            : base(options)
        {
            _identityService = identityService;
            _clock = clock;
            _domainEventDispatcher = domainEventDispatcher;
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampAudit();
            DispatchDomainEventsAsync().GetAwaiter().GetResult();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampAudit();
            await DispatchDomainEventsAsync(cancellationToken);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void StampAudit()
        {
            var userId = _identityService.CurrentUserId;
            var dateTime = _clock.GetDateTime();

            foreach (var aggregate in ChangeTracker.GetChangedAggregates())
                aggregate.SetLastUpdated(userId, dateTime);
        }

        private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default)
        {
            var aggregates = ChangeTracker.GetAggregatesWithEvents();
            if (aggregates.Count == 0)
                return;

            var domainEvents = aggregates.SelectMany(aggregate => aggregate.GetEvents()).ToList();
            aggregates.ForEach(aggregate => aggregate.ClearEvents());

            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        }
    }
}
