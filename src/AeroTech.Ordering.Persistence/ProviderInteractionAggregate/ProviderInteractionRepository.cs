using AeroTech.Ordering.Domain.ProviderInteractionAggregate;
using AeroTech.Ordering.Domain.ProviderInteractionAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.ProviderInteractionAggregate
{
    public sealed class ProviderInteractionRepository : IProviderInteractionRepository
    {
        private readonly OrderingDbContext _dbContext;

        public ProviderInteractionRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(ProviderInteraction interaction, CancellationToken cancellationToken = default)
            => await _dbContext.Set<ProviderInteraction>().AddAsync(interaction, cancellationToken);

        public Task<ProviderInteraction?> GetAsync(long id, CancellationToken cancellationToken = default)
            => _dbContext.Set<ProviderInteraction>()
                .FirstOrDefaultAsync(interaction => interaction.Id == id, cancellationToken);
    }
}
