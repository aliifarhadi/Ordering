using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed record ReserveOrderOutcome(
        long OrderId,
        long OperationId,
        FulfillmentReservationStatus ReservationStatus,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<long> ConfirmedServiceIds,
        IReadOnlyList<long> UnconfirmedServiceIds,
        string? Detail);
}
