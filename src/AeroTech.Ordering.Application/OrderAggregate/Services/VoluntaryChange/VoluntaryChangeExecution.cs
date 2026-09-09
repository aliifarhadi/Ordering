namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public sealed record VoluntaryChangeExecution(
        long OrderId,
        long OrderServiceId,
        string QuotedChangeId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion);
}
