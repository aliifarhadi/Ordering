using AeroTech.Ordering.ReferenceData.ReadModels;
using AeroTech.Ordering.ReferenceData.Syncing;

namespace AeroTech.Ordering.ReferenceData.Core.Wire
{
    public sealed class CoreEnvelope<T>
    {
        public T? Data { get; set; }

        public CoreError[]? Errors { get; set; }
    }

    public sealed class CoreError
    {
        public int? Code { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }
    }

    public sealed class CustomerDto : ISyncSourceDto<long>
    {
        public long Id { get; set; }
        public CustomerType Type { get; set; }
        public int? CityId { get; set; }
        public CustomerContactDto? Contact { get; set; }
        public int PreferredCurrencyId { get; set; }
        public ActivationStatus Status { get; set; }
        public string? Name { get; set; }
        public string UniqueIdentifier { get; set; } = default!;
        public DateTimeOffset LastUpdateTime { get; set; }
    }

    public sealed class OperatorSettingsDto : ISyncSourceDto<long>
    {
        public long Id { get; set; }
        public string ScopeKey { get; set; } = default!;
        public long HomeAirlineId { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }

    public sealed class CustomerContactDto
    {
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
