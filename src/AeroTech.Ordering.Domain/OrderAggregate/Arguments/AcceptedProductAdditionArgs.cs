using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedProductAdditionArgs(
        AcceptedProductAddition Accepted,
        long OperationId,
        long? ActorId = null,
        string? ActorScope = null,
        string? ExternalReference = null);
}
