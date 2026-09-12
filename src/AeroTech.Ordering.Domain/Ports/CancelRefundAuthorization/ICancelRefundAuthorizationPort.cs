namespace AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization
{
    public interface ICancelRefundAuthorizationPort
    {
        Task<CancelRefundAuthorizationDecision> AuthorizeAsync(
            CancelRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default);
    }
}
