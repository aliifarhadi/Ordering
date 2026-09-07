namespace AeroTech.Ordering.Domain.ProviderInteractionAggregate.Contracts
{
    public interface IProviderInteractionRepository
    {
        Task AddAsync(ProviderInteraction interaction, CancellationToken cancellationToken = default);

        Task<ProviderInteraction?> GetAsync(long id, CancellationToken cancellationToken = default);
    }
}
