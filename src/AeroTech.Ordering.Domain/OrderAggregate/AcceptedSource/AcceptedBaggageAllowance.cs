using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedBaggageAllowance(int Pieces, decimal Weight, WeightUnit Unit);
}
