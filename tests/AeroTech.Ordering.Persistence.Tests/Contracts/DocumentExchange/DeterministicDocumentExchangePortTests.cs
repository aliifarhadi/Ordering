using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Providers.Deterministic;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentExchange
{
    public sealed class DeterministicDocumentExchangePortTests : DocumentExchangePortContract
    {
        protected override IDocumentExchangePort Port()
            => new DeterministicDocumentExchangeAdapter { RecoveryOutcome = ProviderOperationOutcome.Confirmed };
    }
}
