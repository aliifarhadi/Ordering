namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class OperatorSettingsReadModel : IReferenceReadModel<long>
    {
        public long Id { get; set; }
        public string ScopeKey { get; set; } = default!;
        public long HomeAirlineId { get; set; }
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
