using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderTravellerDocument : Entity<long>
    {
        private OrderTravellerDocument()
        {
        }

        public OrderTravellerDocument(long id, TravellerDocumentType type, string number, DateOnly? expiryDate, int issuanceCountryId, bool holder)
        {
            Id = id;
            Type = type;
            Number = number;
            ExpiryDate = expiryDate;
            IssuanceCountryId = issuanceCountryId;
            Holder = holder;
        }

        public TravellerDocumentType Type { get; private set; }

        public string Number { get; private set; } = default!;

        public DateOnly? ExpiryDate { get; private set; }

        public int IssuanceCountryId { get; private set; }

        public bool Holder { get; private set; }
    }
}
