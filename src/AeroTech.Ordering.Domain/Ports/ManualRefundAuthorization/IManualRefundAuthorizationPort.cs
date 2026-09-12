namespace AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization
{
    public interface IManualRefundAuthorizationPort
    {
        Task<ManualRefundAuthorizationDecision> AuthorizeAsync(
            ManualRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default);
    }
}
