namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class AirlineReadModel : IReferenceReadModel<int>
    {
        public int Id { get; set; }
        public string IataCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? DisplayName { get; set; }
        public string? LogoUrl { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
