using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public sealed record AirFareBoundReservationPassenger(
        string PassengerId,
        PassengerTypeCode PassengerTypeCode);
}
