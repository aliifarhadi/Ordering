namespace AeroTech.Ordering.Domain.Ports.RefundValue
{
    public sealed record RefundValueRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        decimal ApprovedAmount,
        int CurrencyId,
        string ApprovedDisposition,
        string? DispositionReference,
        string? SuccessorDocumentNumber = null,
        string? SourcePricingReference = null);
}
