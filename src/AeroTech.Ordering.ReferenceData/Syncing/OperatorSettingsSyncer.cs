using AeroTech.Ordering.ReferenceData.Core;
using AeroTech.Ordering.ReferenceData.Core.Wire;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public sealed class OperatorSettingsSyncer : ReferenceSyncerBase<OperatorSettingsReadModel, OperatorSettingsDto, long>
    {
        private readonly ICoreClient _client;

        public OperatorSettingsSyncer(ReferenceDbContext db, ICoreClient client, TimeProvider timeProvider)
            : base(db, timeProvider) => _client = client;

        protected override string Resource => "OperatorSettings";

        protected override Task<List<OperatorSettingsDto>> FetchAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken)
            => _client.GetOperatorSettingsAsync(modifiedAfter, cancellationToken);

        protected override OperatorSettingsReadModel CreateNew(OperatorSettingsDto dto) => new()
        {
            Id = dto.Id,
            ScopeKey = dto.ScopeKey,
            HomeAirlineId = dto.HomeAirlineId,
            LastUpdateTime = dto.LastUpdateTime
        };

        protected override void ApplyChanges(OperatorSettingsDto dto, OperatorSettingsReadModel model)
        {
            model.ScopeKey = dto.ScopeKey;
            model.HomeAirlineId = dto.HomeAirlineId;
            model.LastUpdateTime = dto.LastUpdateTime;
        }
    }
}
