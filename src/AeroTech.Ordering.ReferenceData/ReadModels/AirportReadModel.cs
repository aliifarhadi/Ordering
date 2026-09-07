namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class AirportReadModel : IReferenceReadModel<int>
    {
        public int Id { get; set; }
        public string IataCode { get; set; } = default!;
        public string? IkaoCode { get; set; }
        public string DisplayName { get; set; } = default!;
        public int? CityId { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
