using System.Reflection;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Operations
{
    internal static class OperationsWriteBoundary
    {
        private static readonly Assembly DomainAssembly = typeof(Domain.OrderAggregate.Order).Assembly;

        public static async Task<int> SaveAsync(OrderingDbContext dbContext, CancellationToken cancellationToken)
        {
            EnsureNoPendingDomainState(dbContext);

            return await dbContext.SaveChangesAsync(cancellationToken);
        }

        public static void EnsureNoPendingDomainState(OrderingDbContext dbContext)
        {
            dbContext.ChangeTracker.DetectChanges();

            var pending = dbContext.ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => entry.Entity.GetType())
                .Where(type => type.Assembly == DomainAssembly)
                .Select(type => type.Name)
                .Distinct()
                .Order()
                .ToList();

            if (pending.Count > 0)
                throw ExceptionFactory.OperationsWriteBoundaryViolated(string.Join(", ", pending));
        }
    }
}
