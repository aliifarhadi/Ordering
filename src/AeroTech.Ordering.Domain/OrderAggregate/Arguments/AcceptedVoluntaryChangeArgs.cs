using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedVoluntaryChangeArgs(
        AcceptedVoluntaryChange Accepted,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long OperationId,
        long? ActorId = null,
        string? ActorScope = null);
}
