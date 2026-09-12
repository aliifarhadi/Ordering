using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public sealed record AirFareBoundReservationSalesContext(
        long CreatorUserId,
        int? CountryId,
        SalesChannel? Channel,
        long CustomerId,
        DateTimeOffset SalesDate,
        int PreferredCurrencyId);
}
