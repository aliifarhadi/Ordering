using AeroTech.Ordering.Domain.Ports.ExchangeResidual;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal static class DeterministicResidualOperationResult
    {
        public static ExchangeResidualResult AsResult(this DeterministicResidualOperation operation)
            => new(
                operation.Outcome,
                operation.ProviderReference,
                operation.InstrumentReference,
                operation.Instrument,
                operation.Amount,
                operation.CurrencyId);
    }
}
