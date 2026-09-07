namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class CustomerReadModel : IReferenceReadModel<long>
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public CustomerType Type { get; set; }
        public string UniqueIdentifier { get; set; } = default!;
        public ActivationStatus Status { get; set; }
        public int PreferredCurrencyId { get; set; }
        public int? CityId { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
