using AeroTech.Ordering.ReferenceData.AirInfo;
using AeroTech.Ordering.ReferenceData.AirInfo.Wire;
using AeroTech.Ordering.ReferenceData.Configuration;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public sealed class AirportSyncer : ReferenceSyncerBase<AirportReadModel, AirportDto, int>
    {
        private readonly IAirInfoClient _client;
        private readonly string _primaryLanguage;

        public AirportSyncer(ReferenceDbContext db, IAirInfoClient client, IOptions<ReferenceDataOptions> options, TimeProvider timeProvider)
            : base(db, timeProvider)
        {
            _client = client;
            _primaryLanguage = options.Value.PrimaryLanguage;
        }

        protected override string Resource => "Airports";

        protected override Task<List<AirportDto>> FetchAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken)
            => _client.GetAirportsAsync(modifiedAfter, cancellationToken);

        protected override AirportReadModel CreateNew(AirportDto dto) => new()
        {
            Id = dto.Id,
            IataCode = dto.IataCode,
            IkaoCode = dto.IkaoCode,
            DisplayName = DisplayNameSelector.Pick(dto.DisplayNames, _primaryLanguage),
            CityId = dto.City?.Id,
            LastUpdateTime = dto.LastUpdateTime
        };

        protected override void ApplyChanges(AirportDto dto, AirportReadModel model)
        {
            model.IataCode = dto.IataCode;
            model.IkaoCode = dto.IkaoCode;
            model.DisplayName = DisplayNameSelector.Pick(dto.DisplayNames, _primaryLanguage);
            model.CityId = dto.City?.Id;
            model.LastUpdateTime = dto.LastUpdateTime;
        }

        protected override async Task SyncChildrenAsync(AirportDto dto, CancellationToken cancellationToken)
        {
            var incoming = dto.Terminals ?? new List<AirportTerminalDto>();
            var incomingIds = incoming.Select(terminal => terminal.Id).ToHashSet();

            var existing = await Db.AirportTerminals
                .Where(terminal => terminal.AirportId == dto.Id)
                .ToListAsync(cancellationToken);
            var existingById = existing.ToDictionary(terminal => terminal.Id);

            foreach (var terminal in incoming)
            {
                if (terminal.IsDeleted)
                {
                    if (existingById.TryGetValue(terminal.Id, out var toRemove))
                        Db.AirportTerminals.Remove(toRemove);
                    continue;
                }

                if (existingById.TryGetValue(terminal.Id, out var current))
                {
                    if (current.LastUpdateTime >= terminal.LastUpdateTime)
                        continue;

                    current.Number = terminal.Number;
                    current.Direction = terminal.Direction;
                    current.DisplayName = DisplayNameSelector.Pick(terminal.DisplayNames, _primaryLanguage);
                    current.LastUpdateTime = terminal.LastUpdateTime;
                }
                else
                {
                    await Db.AirportTerminals.AddAsync(new AirportTerminalReadModel
                    {
                        Id = terminal.Id,
                        AirportId = dto.Id,
                        Number = terminal.Number,
                        Direction = terminal.Direction,
                        DisplayName = DisplayNameSelector.Pick(terminal.DisplayNames, _primaryLanguage),
                        LastUpdateTime = terminal.LastUpdateTime
                    }, cancellationToken);
                }
            }

            foreach (var current in existing)
                if (!incomingIds.Contains(current.Id))
                    Db.AirportTerminals.Remove(current);
        }
    }
}
