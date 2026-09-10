using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record CancelConfirmedSeatsRequest(string HoldBatchId, IReadOnlyList<string> SeatHoldReferences, VoidReason Reason);
}
