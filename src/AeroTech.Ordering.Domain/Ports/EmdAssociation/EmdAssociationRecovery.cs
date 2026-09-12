using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.EmdAssociation
{
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
