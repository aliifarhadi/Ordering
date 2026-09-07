namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentCompleted(
        long PaymentIntentId,
        long OrderId,
        string PayableInstructionId,
        long IssuerLegalEntityId,
        decimal CapturedAmount,
        int CurrencyId,
        DateTimeOffset CompletedAt) : BaseIntegrationEvent;
}
