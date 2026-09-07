using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota
{
    public static class OtaCreateOrderArgsMapper
    {
        private const decimal OtaCommissionRate = 0m;

        public static CreateOrderArgs Map(OtaCreateOrderFromOfferCommand command, SalesChannel channel, long airlineOfficeId)
            => new(
                command.CustomerId,
                channel,
                command.CreatorUserId,
                airlineOfficeId,
                command.OfferId,
                OtaCommissionRate,
                command.Travellers.Select(MapTraveller).ToList(),
                MapContact(command.Contact),
                new List<CreateOrderSeatSelectionArgs>());

        private static CreateOrderTravellerArgs MapTraveller(OtaOrderTraveller traveller)
            => new(
                traveller.Index,
                traveller.ParentIndex,
                traveller.Name.FirstName,
                traveller.Name.LastName,
                traveller.Name.NoLastName,
                traveller.PassengerType,
                ToAgeRange(traveller.PassengerType),
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
                    .ToList());

        private static CreateOrderContactArgs MapContact(OtaOrderContact contact)
        {
            var points = new List<CreateContactPointArgs>();

            foreach (var email in contact.EmailAddresses)
                points.Add(new CreateContactPointArgs(Messages.Ordering.Enums.ContactPointType.Email, email, null, points.Count == 0));

            foreach (var phone in contact.Phones)
                points.Add(new CreateContactPointArgs(ToContactPointType(phone.DeviceType), phone.Number, phone.CountryCallingCode, points.Count == 0));

            return new CreateOrderContactArgs(null, points);
        }

        private static AgeRange ToAgeRange(PassengerTypeCode passengerType)
            => passengerType switch
            {
                PassengerTypeCode.INF => AgeRange.Infant,
                PassengerTypeCode.INN => AgeRange.Infant,
                PassengerTypeCode.CHD => AgeRange.Child,
                PassengerTypeCode.CNN => AgeRange.Child,
                _ => AgeRange.Adult
            };

        private static Messages.Ordering.Enums.ContactPointType ToContactPointType(PhoneDeviceType deviceType)
            => deviceType switch
            {
                PhoneDeviceType.SMS => Messages.Ordering.Enums.ContactPointType.Sms,
                PhoneDeviceType.WAP => Messages.Ordering.Enums.ContactPointType.WhatsApp,
                PhoneDeviceType.TLG => Messages.Ordering.Enums.ContactPointType.Telegram,
                _ => Messages.Ordering.Enums.ContactPointType.Phone
            };
    }
}
