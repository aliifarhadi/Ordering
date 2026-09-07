namespace AeroTech.Ordering.Domain._Shared.Contracts
{
    public interface IHomeOperatorProvider
    {
        Task<long> GetOwnerAirlineIdAsync(CancellationToken cancellationToken = default);
    }
}
