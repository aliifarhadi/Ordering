using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderPaid(
        long OrderId,
        OrderStatus Status,
        long PaymentId,
        decimal Amount,
        int CurrencyId,
        FormOfPayment FormOfPayment,
        string? ProviderReference,
        DateTimeOffset PaidAt,
        long CustomerId,
        long AirlineOfficeId) : BaseIntegrationEvent;
}
