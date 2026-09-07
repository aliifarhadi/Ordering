using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.ServiceHost.OperatorContext
{
    public sealed class ReferenceDataHomeOperatorProvider : IHomeOperatorProvider
    {
        private readonly ReferenceDbContext _referenceDbContext;

        public ReferenceDataHomeOperatorProvider(ReferenceDbContext referenceDbContext)
            => _referenceDbContext = referenceDbContext;

        public async Task<long> GetOwnerAirlineIdAsync(CancellationToken cancellationToken = default)
        {
            var homeAirlineId = await _referenceDbContext.OperatorSettings
                .AsNoTracking()
                .Where(settings => settings.ScopeKey == OperatorScopeKey.HomeOperator)
                .Select(settings => (long?)settings.HomeAirlineId)
                .SingleOrDefaultAsync(cancellationToken);

            if (homeAirlineId is not { } ownerAirlineId || ownerAirlineId <= 0)
                throw ExceptionFactory.HomeOperatorNotProvisioned(OperatorScopeKey.HomeOperator);

            return ownerAirlineId;
        }
    }
}
