using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderArgs(
        long CustomerId,
        SalesChannel Channel,
        long CreatorUserId,
        long AirlineOfficeId,
        string OfferId,
        decimal CommissionRate,
        IReadOnlyList<CreateOrderTravellerArgs> Travellers,
        CreateOrderContactArgs Contact,
        IReadOnlyList<CreateOrderSeatSelectionArgs> SeatSelections);

    public sealed record CreateOrderTravellerArgs(
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
        IReadOnlyList<CreateTravellerDocumentArgs> Documents);

    public sealed record CreateTravellerDocumentArgs(
        TravellerDocumentType Type,
        string Number,
        DateOnly? ExpiryDate,
        int IssuanceCountryId,
        bool Holder);

    public sealed record CreateOrderContactArgs(
        string? ContactName,
        IReadOnlyList<CreateContactPointArgs> ContactPoints);

    public sealed record CreateContactPointArgs(
        Messages.Ordering.Enums.ContactPointType Type,
        string Value,
        string? CountryCode,
        bool IsPrimary);

    public sealed record CreateOrderSeatSelectionArgs(
        string BoundId,
        int TravellerIndex,
        string? SeatNumber);
}
