using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder
{
    public static class CreateOrderArgsMapper
    {
        public static CreateOrderArgs Map(
            long customerId,
            SalesChannel channel,
            long creatorUserId,
            long airlineOfficeId,
            string offerId,
            decimal commissionRate,
            IReadOnlyList<CreateOrderTravellerInput> travellers,
            CreateOrderContactInput contact,
            IReadOnlyList<CreateOrderSeatInput> seatSelections)
            => new(
                customerId,
                channel,
                creatorUserId,
                airlineOfficeId,
                offerId,
                commissionRate,
                travellers
                    .Select(traveller => new CreateOrderTravellerArgs(
                        traveller.Index,
                        traveller.ParentIndex,
                        traveller.FirstName,
                        traveller.SurName,
                        traveller.NoSurname,
                        traveller.PassengerType,
                        traveller.AgeRange,
                        traveller.DateOfBirth,
                        traveller.Gender,
                        traveller.NationalityId,
                        traveller.CountryOfResidenceId,
                        traveller.Documents
                            .Select(document => new CreateTravellerDocumentArgs(
                                document.Type,
                                document.Number,
                                document.ExpiryDate,
                                document.IssuanceCountryId,
                                document.Holder))
                            .ToList()))
                    .ToList(),
                new CreateOrderContactArgs(
                    contact.ContactName,
                    contact.ContactPoints
                        .Select(point => new CreateContactPointArgs(point.Type, point.Value, point.CountryCode, point.IsPrimary))
                        .ToList()),
                seatSelections
                    .Select(seat => new CreateOrderSeatSelectionArgs(seat.BoundId, seat.TravellerIndex, seat.SeatNumber))
                    .ToList());
    }
}
