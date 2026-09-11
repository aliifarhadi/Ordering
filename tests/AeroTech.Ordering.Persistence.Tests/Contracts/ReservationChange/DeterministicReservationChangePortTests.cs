using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ReservationChange;
using AeroTech.Ordering.Providers.Deterministic;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ReservationChange
{
    public sealed class DeterministicReservationChangePortTests : ReservationChangePortContract
    {
        protected override IReservationChangePort Port()
            => new DeterministicReservationChangeAdapter { RecoveryOutcome = ProviderOperationOutcome.Confirmed };
    }
}
