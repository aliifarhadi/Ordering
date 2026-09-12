namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public interface IEmdExchangePort
    {
        Task<EmdExchangeResult> ExchangeAsync(
            EmdExchangeRequest request,
            CancellationToken cancellationToken = default);

        Task<EmdExchangeRecovery> RecoverAsync(
            EmdExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
