namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class CityReadModel : IReferenceReadModel<int>
    {
        public int Id { get; set; }
        public string IataCode { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public int? CountryId { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
