namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class IssuanceOptions
    {
        public TimeSpan VoidWindow { get; set; } = TimeSpan.FromHours(24);

        public TimeSpan TicketValidity { get; set; } = TimeSpan.FromDays(365);
    }
}
