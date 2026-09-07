namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentIntentGuaranteed(
        long PaymentIntentId,
        long OrderId,
        string PayableInstructionId,
        long IssuerLegalEntityId,
        decimal GuaranteedAmount,
        int CurrencyId,
        DateTimeOffset? EarliestGuaranteeExpiry) : BaseIntegrationEvent;
}
