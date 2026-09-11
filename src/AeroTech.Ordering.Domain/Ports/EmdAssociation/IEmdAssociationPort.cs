using AeroTech.Messages.Ordering.Enums;

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

    public sealed record EmdReassociationRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string EmdDocumentNumber,
        int EmdCouponNumber,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        string SuccessorDocumentNumber,
        int SuccessorCouponNumber,
        long? BeneficiaryTravellerId,
        long IssuerCarrierId,
        string DecisionReference);

    public sealed record EmdAssociationRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string EmdDocumentNumber,
        int EmdCouponNumber);

    public sealed record EmdAssociationResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? EmdDocumentNumber = null,
        int? EmdCouponNumber = null,
        string? AssociatedDocumentNumber = null,
        int? AssociatedCouponNumber = null,
        string? Detail = null);

    public sealed record EmdAssociationRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? EmdDocumentNumber = null,
        int? EmdCouponNumber = null,
        string? AssociatedDocumentNumber = null,
        int? AssociatedCouponNumber = null,
        string? Detail = null)
    {
        public EmdAssociationResult AsResult()
            => new(
                Outcome,
                ProviderReference,
                EmdDocumentNumber,
                EmdCouponNumber,
                AssociatedDocumentNumber,
                AssociatedCouponNumber,
                Detail);
    }
}
