using AeroTech.Ordering.Domain.Ports.EmdAssociation;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredEmdAssociationProvider : IEmdAssociationPort
    {
        public Task<EmdAssociationResult> ReassociateAsync(
            EmdReassociationRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.EmdAssociationSourceNotConfigured();

        public Task<EmdAssociationRecovery> RecoverReassociationAsync(
            EmdAssociationRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.EmdAssociationSourceNotConfigured();
    }
}
