using AeroTech.Framework.Core.Domain.Repository;

namespace AeroTech.Ordering.Persistence.Tests._Shared
{
    public sealed class InterruptibleUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;

        public InterruptibleUnitOfWork(IUnitOfWork inner) => _inner = inner;

        public bool FailSaves { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => FailSaves
                ? throw new InvalidOperationException("The local servicing commit was interrupted.")
                : _inner.SaveChangesAsync(cancellationToken);
    }
}
