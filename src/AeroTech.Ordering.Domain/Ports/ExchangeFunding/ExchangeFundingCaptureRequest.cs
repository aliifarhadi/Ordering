namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public sealed record ExchangeFundingCaptureRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string SuccessorDocumentNumber,
        string? GuaranteeReference,
        decimal Amount,
        int CurrencyId);
}
