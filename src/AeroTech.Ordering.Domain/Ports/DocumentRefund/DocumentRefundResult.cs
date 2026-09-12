using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public sealed record DocumentRefundResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null,
        string? DocumentNumber = null,
        IReadOnlyList<int>? CouponNumbers = null);
}
