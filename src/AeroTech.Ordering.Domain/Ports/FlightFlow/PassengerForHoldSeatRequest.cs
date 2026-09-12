using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record PassengerForHoldSeatRequest(
        string PaxReference,
        PassengerTypeCode Type,
        Gender Gender);
}
