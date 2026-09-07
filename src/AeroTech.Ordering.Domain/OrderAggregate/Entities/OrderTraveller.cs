using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderTraveller : Entity<long>
    {
        private readonly List<OrderTravellerDocument> _documents = new();

        private OrderTraveller()
        {
        }

        public OrderTraveller(
            long id,
            long orderId,
            int index,
            Name name,
            PassengerTypeCode passengerType,
            AgeRange ageRange,
            DateOnly dateOfBirth,
            Gender gender,
            int nationalityId,
            int countryOfResidenceId,
            IEnumerable<OrderTravellerDocument> documents)
        {
            Id = id;
            OrderId = orderId;
            Index = index;
            Name = name;
            PassengerType = passengerType;
            AgeRange = ageRange;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            NationalityId = nationalityId;
            CountryOfResidenceId = countryOfResidenceId;
            _documents.AddRange(documents);
        }

        public long OrderId { get; private set; }

        public int Index { get; private set; }

        public Name Name { get; private set; } = default!;

        public PassengerTypeCode PassengerType { get; private set; }

        public AgeRange AgeRange { get; private set; }

        public DateOnly DateOfBirth { get; private set; }

        public Gender Gender { get; private set; }

        public int NationalityId { get; private set; }

        public int CountryOfResidenceId { get; private set; }

        public long? ParentTravellerId { get; private set; }

        public IReadOnlyCollection<OrderTravellerDocument> Documents => _documents.AsReadOnly();

        public void AssignParent(long parentTravellerId) => ParentTravellerId = parentTravellerId;

        internal OrderTraveller CopyTo(long newId, long newOrderId, long? newParentTravellerId, IIdGenerator idGenerator)
        {
            var documents = _documents.Select(document => new OrderTravellerDocument(
                idGenerator.NewId(), document.Type, document.Number, document.ExpiryDate, document.IssuanceCountryId, document.Holder));

            var copy = new OrderTraveller(
                newId, newOrderId, Index, Name.Copy(), PassengerType, AgeRange, DateOfBirth, Gender, NationalityId, CountryOfResidenceId, documents);

            if (newParentTravellerId.HasValue)
                copy.AssignParent(newParentTravellerId.Value);

            return copy;
        }
    }
}
