namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentPaidUnapplied(
        long PaymentIntentId,
        long OrderId,
        string SupersededPayableInstructionId,
        decimal CapturedAmount,
        int CurrencyId,
        string ReasonCode) : BaseIntegrationEvent;
}
