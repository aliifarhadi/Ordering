namespace AeroTech.Ordering.Domain.Ports.ExchangeResidual
{
    public interface IExchangeResidualValuePort
    {
        Task<ExchangeResidualResult> FulfillAsync(
            ExchangeResidualRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeResidualRecovery> RecoverAsync(
            ExchangeResidualRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
