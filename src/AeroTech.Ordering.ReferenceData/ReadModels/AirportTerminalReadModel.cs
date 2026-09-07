namespace AeroTech.Ordering.ReferenceData.ReadModels
{
    public sealed class AirportTerminalReadModel
    {
        public int Id { get; set; }
        public int AirportId { get; set; }
        public string Number { get; set; } = default!;
        public TerminalDirection Direction { get; set; }
        public string DisplayName { get; set; } = default!;
        public DateTimeOffset LastUpdateTime { get; set; }
    }
}
