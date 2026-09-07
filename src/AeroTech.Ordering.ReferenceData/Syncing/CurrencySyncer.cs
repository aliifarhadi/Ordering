using AeroTech.Ordering.ReferenceData.AirInfo;
using AeroTech.Ordering.ReferenceData.AirInfo.Wire;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public sealed class CurrencySyncer : ReferenceSyncerBase<CurrencyReadModel, CurrencyDto, int>
    {
        private readonly IAirInfoClient _client;

        public CurrencySyncer(ReferenceDbContext db, IAirInfoClient client, TimeProvider timeProvider)
            : base(db, timeProvider) => _client = client;

        protected override string Resource => "Currencies";

        protected override Task<List<CurrencyDto>> FetchAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken)
            => _client.GetCurrenciesAsync(modifiedAfter, cancellationToken);

        protected override CurrencyReadModel CreateNew(CurrencyDto dto) => new()
        {
            Id = dto.Id,
            Code = dto.Code,
            DecimalPlaces = dto.DecimalPlaces,
            RoundingFactor = dto.RoundingFactor,
            LastUpdateTime = dto.LastUpdateTime
        };

        protected override void ApplyChanges(CurrencyDto dto, CurrencyReadModel model)
        {
            model.Code = dto.Code;
            model.DecimalPlaces = dto.DecimalPlaces;
            model.RoundingFactor = dto.RoundingFactor;
            model.LastUpdateTime = dto.LastUpdateTime;
        }
    }
}
