namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketExchanged(
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long ExchangeRecordId,
        long SuccessorElectronicTicketId,
        string SuccessorDocumentNumber,
        IReadOnlyList<ElectronicTicketExchangedCoupon> Coupons,
        string QuotedExchangeId,
        string TargetSelectionRef,
        int DocumentVersion) : BaseIntegrationEvent;
}
