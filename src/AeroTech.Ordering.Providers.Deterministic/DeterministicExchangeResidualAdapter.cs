using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicExchangeResidualAdapter : IExchangeResidualValuePort
    {
        private readonly Dictionary<string, DeterministicResidualOperation> _dispatched = new(StringComparer.Ordinal);

        public ProviderOperationOutcome FulfillOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public decimal? AmountOverride { get; set; }

        public int? CurrencyOverride { get; set; }

        public bool OmitInstrumentReference { get; set; }

        public bool OmitProviderReference { get; set; }

        public ResidualInstrumentKind? InstrumentOverride { get; set; }

        public List<ExchangeResidualRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public Task<ExchangeResidualResult> FulfillAsync(
            ExchangeResidualRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The residual fulfilment request never left Ordering.");

            var recorded = Remember(request);

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The residual fulfilment response never reached Ordering.");

            return Task.FromResult(recorded.AsResult());
        }

        public Task<ExchangeResidualRecovery> RecoverAsync(
            ExchangeResidualRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The residual fulfilment provider is unreachable.");

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new ExchangeResidualRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such residual fulfilment operation"));

            var resolved = dispatched.Resolved(RecoveryOutcome);

            _dispatched[request.OperationKey] = resolved;

            return Task.FromResult(new ExchangeResidualRecovery(
                true,
                resolved.Outcome,
                resolved.ProviderReference,
                resolved.InstrumentReference,
                resolved.Instrument,
                resolved.Amount,
                resolved.CurrencyId));
        }

        private DeterministicResidualOperation Remember(ExchangeResidualRequest request)
        {
            var intent = Intent(request);

            if (_dispatched.TryGetValue(request.OperationKey, out var existing))
                return string.Equals(existing.Intent, intent, StringComparison.Ordinal)
                    ? existing
                    : throw new InvalidOperationException(
                        $"A different residual intent already owns operation key {request.OperationKey}.");

            var settled = FulfillOutcome != ProviderOperationOutcome.Rejected;

            var recorded = new DeterministicResidualOperation(
                intent,
                FulfillOutcome,
                settled && !OmitProviderReference ? $"RES-{Reference(request.OperationKey)}" : null,
                settled && !OmitInstrumentReference ? $"INSTR-{request.SuccessorDocumentNumber}" : null,
                settled ? InstrumentOverride ?? Family(request.ExpectedInstrument) : null,
                AmountOverride ?? request.Amount,
                CurrencyOverride ?? request.CurrencyId);

            _dispatched[request.OperationKey] = recorded;

            return recorded;
        }

        private static ResidualInstrumentKind Family(ResidualInstrumentKind expected)
            => expected == ResidualInstrumentKind.Unknown ? ResidualInstrumentKind.Mco : expected;

        private static string Intent(ExchangeResidualRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.QuotedExchangeId,
                request.PredecessorDocumentNumber,
                request.SuccessorDocumentNumber,
                request.BeneficiaryTravellerId,
                request.Amount,
                request.CurrencyId,
                request.Disposition);

        private static string Reference(string operationKey) => operationKey.Replace(':', '-');
    }
}
