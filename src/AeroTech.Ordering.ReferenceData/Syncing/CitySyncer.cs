using AeroTech.Ordering.ReferenceData.AirInfo;
using AeroTech.Ordering.ReferenceData.AirInfo.Wire;
using AeroTech.Ordering.ReferenceData.Configuration;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public sealed class CitySyncer : ReferenceSyncerBase<CityReadModel, CityDto, int>
    {
        private readonly IAirInfoClient _client;
        private readonly string _primaryLanguage;

        public CitySyncer(ReferenceDbContext db, IAirInfoClient client, IOptions<ReferenceDataOptions> options, TimeProvider timeProvider)
            : base(db, timeProvider)
        {
            _client = client;
            _primaryLanguage = options.Value.PrimaryLanguage;
        }

        protected override string Resource => "Cities";

        protected override Task<List<CityDto>> FetchAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken)
            => _client.GetCitiesAsync(modifiedAfter, cancellationToken);

        protected override CityReadModel CreateNew(CityDto dto) => new()
        {
            Id = dto.Id,
            IataCode = dto.IataCode,
            DisplayName = DisplayNameSelector.Pick(dto.DisplayNames, _primaryLanguage),
            CountryId = dto.State?.Country?.Id,
            LastUpdateTime = dto.LastUpdateTime
        };

        protected override void ApplyChanges(CityDto dto, CityReadModel model)
        {
            model.IataCode = dto.IataCode;
            model.DisplayName = DisplayNameSelector.Pick(dto.DisplayNames, _primaryLanguage);
            model.CountryId = dto.State?.Country?.Id;
            model.LastUpdateTime = dto.LastUpdateTime;
        }
    }
}
