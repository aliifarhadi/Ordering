using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class OrderTravellerReadModel
    {
        public long Id { get; set; }

        public long OrderId { get; set; }

        public int Index { get; set; }

        public string FirstName { get; set; } = default!;

        public string? SurName { get; set; }

        public AgeRange AgeRange { get; set; }
    }
}
