using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketRefunded(
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long RefundRecordId,
        string QuotedRefundId,
        PricingSource PricingSource,
        decimal ApprovedAmount,
        int CurrencyId,
        string ApprovedDisposition,
        IReadOnlyList<long> TicketCouponIds,
        ElectronicTicketStatus StatusSummary,
        int DocumentVersion) : BaseIntegrationEvent;
}
