using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferCharge(
        string AirChargeId,
        AirChargeKind Kind,
        string? Code,
        string? Name,
        bool IsRefundable);
}
