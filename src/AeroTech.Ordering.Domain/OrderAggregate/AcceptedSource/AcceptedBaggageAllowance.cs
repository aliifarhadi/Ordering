using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedBaggageAllowance(int Pieces, decimal Weight, BaggageWeightUnit Unit);
}
