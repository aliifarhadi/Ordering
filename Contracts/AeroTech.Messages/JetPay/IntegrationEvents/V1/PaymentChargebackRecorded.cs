namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentChargebackRecorded(
        long DisputeId,
        long PaymentIntentId,
        long OrderId,
        long OriginalCaptureOperationId,
        decimal Amount,
        int CurrencyId,
        string ReasonCategory,
        DateTimeOffset OccurredAt) : BaseIntegrationEvent;
}
