namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public sealed record ExchangeFundingGuaranteeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string PredecessorDocumentNumber,
        long PayerTravellerId,
        decimal Amount,
        int CurrencyId,
        string FundingMethodRef);
}
