using AeroTech.Ordering.ReferenceData.Core;
using AeroTech.Ordering.ReferenceData.Core.Wire;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public sealed class CustomerSyncer : ReferenceSyncerBase<CustomerReadModel, CustomerDto, long>
    {
        private readonly ICoreClient _client;

        public CustomerSyncer(ReferenceDbContext db, ICoreClient client, TimeProvider timeProvider)
            : base(db, timeProvider) => _client = client;

        protected override string Resource => "Customers";

        protected override Task<List<CustomerDto>> FetchAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken)
            => _client.GetCustomersAsync(modifiedAfter, cancellationToken);

        protected override CustomerReadModel CreateNew(CustomerDto dto) => new()
        {
            Id = dto.Id,
            Name = dto.Name,
            Type = dto.Type,
            UniqueIdentifier = dto.UniqueIdentifier,
            Status = dto.Status,
            PreferredCurrencyId = dto.PreferredCurrencyId,
            CityId = dto.CityId,
            Email = dto.Contact?.Email,
            PhoneNumber = dto.Contact?.PhoneNumber,
            LastUpdateTime = dto.LastUpdateTime
        };

        protected override void ApplyChanges(CustomerDto dto, CustomerReadModel model)
        {
            model.Name = dto.Name;
            model.Type = dto.Type;
            model.UniqueIdentifier = dto.UniqueIdentifier;
            model.Status = dto.Status;
            model.PreferredCurrencyId = dto.PreferredCurrencyId;
            model.CityId = dto.CityId;
            model.Email = dto.Contact?.Email;
            model.PhoneNumber = dto.Contact?.PhoneNumber;
            model.LastUpdateTime = dto.LastUpdateTime;
        }
    }
}
