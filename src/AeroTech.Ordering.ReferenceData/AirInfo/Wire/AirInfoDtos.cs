using AeroTech.Ordering.ReferenceData.ReadModels;
using AeroTech.Ordering.ReferenceData.Syncing;

namespace AeroTech.Ordering.ReferenceData.AirInfo.Wire
{
    public sealed class AirInfoEnvelope<T>
    {
        public T? Data { get; set; }

        public AirInfoError[]? Errors { get; set; }
    }

    public sealed class AirInfoError
    {
        public int Code { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }
    }

    public sealed class DisplayNameDto
    {
        public string? Language { get; set; }

        public string? Value { get; set; }
    }

    public sealed class CurrencyDto : ISyncSourceDto<int>, ISoftDeletable
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public int DecimalPlaces { get; set; }
        public double RoundingFactor { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class AirlineDto : ISyncSourceDto<int>, ISoftDeletable
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? DisplayName { get; set; }
        public string IataCode { get; set; } = default!;
        public string? LogoURL { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class CityDto : ISyncSourceDto<int>, ISoftDeletable
    {
        public int Id { get; set; }
        public string IataCode { get; set; } = default!;
        public ICollection<DisplayNameDto>? DisplayNames { get; set; }
        public StateDto? State { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class StateDto
    {
        public CountryDto? Country { get; set; }
    }

    public sealed class CountryDto
    {
        public int Id { get; set; }
    }

    public sealed class AirportDto : ISyncSourceDto<int>, ISoftDeletable
    {
        public int Id { get; set; }
        public string IataCode { get; set; } = default!;
        public string? IkaoCode { get; set; }
        public ICollection<DisplayNameDto>? DisplayNames { get; set; }
        public CityDto? City { get; set; }
        public ICollection<AirportTerminalDto>? Terminals { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class AirportTerminalDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = default!;
        public TerminalDirection Direction { get; set; }
        public ICollection<DisplayNameDto>? DisplayNames { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
        public bool IsDeleted { get; set; }
    }
}
