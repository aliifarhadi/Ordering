using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public interface IDocumentIssuancePort
    {
        Task<DocumentIssuanceResult> IssueAsync(DocumentIssuanceRequest request, CancellationToken cancellationToken = default);

        Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default);
    }

    public sealed record DocumentIssuanceRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long TravelerId,
        string DocumentNumber,
        int IssuerCarrierId,
        int CurrencyId,
        decimal TotalAmount,
        IReadOnlyList<DocumentCouponRequest> Coupons);

    public sealed record DocumentCouponRequest(
        long OrderServiceId,
        long JourneySegmentId,
        decimal AttributedValue);

    public sealed record DocumentRecoveryRequest(string OperationKey, long OrderId, long OperationId, string DocumentNumber);

    public sealed record DocumentIssuanceResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null);
}
