namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class CurrencyReadModel : IReferenceReadModel<int>
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public int DecimalPlaces { get; set; }
        public double RoundingFactor { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
