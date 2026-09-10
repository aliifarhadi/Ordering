using AeroTech.Messages.Ordering.Enums;

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
        long PredecessorTicketCouponId,
        long SuccessorTicketCouponId,
        long PreviousOrderServiceId,
        long ReplacementOrderServiceId,
        string QuotedExchangeId,
        string TargetSelectionRef,
        int DocumentVersion) : BaseIntegrationEvent;
}
