using AeroTech.Framework.Core.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AeroTech.Framework.Infrastructure.Persistence
{
    public static class ChangeTrackerExtensions
    {
        public static IEnumerable<IAggregateRoot> GetChangedAggregates(this ChangeTracker changeTracker)
        {
            return changeTracker
                .Entries<IAggregateRoot>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity);
        }

        public static List<IAggregateRoot> GetAggregatesWithEvents(this ChangeTracker changeTracker)
        {
            return changeTracker
                .Entries<IAggregateRoot>()
                .Where(entry => entry.State != EntityState.Detached)
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.GetEvents().Any())
                .ToList();
        }
    }
}
