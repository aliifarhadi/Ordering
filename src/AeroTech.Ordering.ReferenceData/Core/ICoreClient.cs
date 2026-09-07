using AeroTech.Ordering.ReferenceData.Core.Wire;

namespace AeroTech.Ordering.ReferenceData.Core
{
    public interface ICoreClient
    {
        Task<List<CustomerDto>> GetCustomersAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default);

        Task<List<OperatorSettingsDto>> GetOperatorSettingsAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default);
    }
}
