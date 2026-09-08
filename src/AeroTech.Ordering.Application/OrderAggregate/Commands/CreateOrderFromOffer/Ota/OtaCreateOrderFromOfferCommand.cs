using AeroTech.Messages.Ordering.Enums;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota
{
    public sealed record OtaCreateOrderFromOfferCommand(
        long CustomerId,
        long CreatorUserId,
        string OfferId,
        OtaOrderContact Contact,
        IReadOnlyList<OtaOrderTraveller> Travellers) : IRequest<CreateOrderFromOfferResult>;

    public sealed record OtaOrderContact(
        IReadOnlyList<string> EmailAddresses,
        IReadOnlyList<OtaContactPhone> Phones);

    public sealed record OtaContactPhone(
        string CountryCallingCode,
        string Number,
        PhoneDeviceType DeviceType);

    public sealed record OtaOrderTraveller(
        int Index,
        int? ParentIndex,
        OtaTravellerName Name,
        DateOnly DateOfBirth,
        Gender Gender,
        PassengerTypeCode PassengerType,
        int NationalityId,
        int CountryOfResidenceId,
        IReadOnlyList<OtaTravellerDocument> Documents);

    public sealed record OtaTravellerName(
        string FirstName,
        string LastName,
        bool NoLastName);

    public sealed record OtaTravellerDocument(
        TravellerDocumentType Type,
        string Number,
        DateOnly? ExpiryDate,
        int IssuanceCountryId,
        bool Holder);
}
