namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record ExchangeProvenance(
        long OperationId,
        string QuotedExchangeId,
        string TargetSelectionRef,
        string? SourcePricingReference,
        string? ProviderReference,
        long? ActorId,
        string? ActorScope);
}
