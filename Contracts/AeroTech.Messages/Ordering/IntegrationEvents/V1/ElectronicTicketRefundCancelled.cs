using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketRefundCancelled(
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long RefundRecordId,
        long CorrectionRecordId,
        long OriginalRefundOperationId,
        decimal CorrectedAmount,
        int CurrencyId,
        string Reason,
        ElectronicTicketStatus StatusSummary,
        int DocumentVersion) : BaseIntegrationEvent;
}
