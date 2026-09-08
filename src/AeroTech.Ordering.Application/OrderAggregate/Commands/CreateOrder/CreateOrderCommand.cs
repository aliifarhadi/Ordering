using AeroTech.Messages.Core.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder
{
    public sealed record CreateOrderCommand(
        long CustomerId,
        SalesChannel Channel,
        long CreatorUserId,
        long AirlineOfficeId,
        string OfferId,
        decimal CommissionRate,
        IReadOnlyList<CreateOrderTravellerInput> Travellers,
        CreateOrderContactInput Contact,
        IReadOnlyList<CreateOrderSeatInput> SeatSelections) : IRequest<long>;

    public sealed record CreateOrderTravellerInput(
        int Index,
        int? ParentIndex,
        string FirstName,
        string SurName,
        bool NoSurname,
        PassengerTypeCode PassengerType,
        AgeRange AgeRange,
        DateOnly DateOfBirth,
        Gender Gender,
        int NationalityId,
        int CountryOfResidenceId,
        IReadOnlyList<CreateOrderDocumentInput> Documents);

    public sealed record CreateOrderDocumentInput(
        TravellerDocumentType Type,
        string Number,
        DateOnly? ExpiryDate,
        int IssuanceCountryId,
        bool Holder);

    public sealed record CreateOrderContactInput(
        string? ContactName,
        IReadOnlyList<CreateOrderContactPointInput> ContactPoints);

    public sealed record CreateOrderContactPointInput(
        Messages.Ordering.Enums.ContactPointType Type,
        string Value,
        string? CountryCode,
        bool IsPrimary);

    public sealed record CreateOrderSeatInput(
        string BoundId,
        int TravellerIndex,
        string? SeatNumber);
}
