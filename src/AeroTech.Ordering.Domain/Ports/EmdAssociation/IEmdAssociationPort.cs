namespace AeroTech.Ordering.Domain.Ports.EmdAssociation
{
    public interface IEmdAssociationPort
    {
        Task<EmdAssociationResult> ReassociateAsync(
            EmdReassociationRequest request,
            CancellationToken cancellationToken = default);

        Task<EmdAssociationRecovery> RecoverReassociationAsync(
            EmdAssociationRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
