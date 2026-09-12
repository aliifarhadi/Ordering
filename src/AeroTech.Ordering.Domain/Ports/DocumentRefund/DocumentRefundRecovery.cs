using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public sealed record DocumentRefundRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null,
        string? DocumentNumber = null,
        IReadOnlyList<int>? CouponNumbers = null)
    {
        public DocumentRefundResult AsResult()
            => new(Outcome, ProviderReference, Detail, DocumentNumber, CouponNumbers);
    }
}
