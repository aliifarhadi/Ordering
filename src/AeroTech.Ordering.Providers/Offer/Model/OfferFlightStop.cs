using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferFlightStop(
        int DurationMinutes,
        StopType StopType,
        bool PassengersCanBoardOrLeave);
}
