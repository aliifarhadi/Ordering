using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record ServicingAncillaryCheckpoint(
        AncillaryExchangeDisposition Disposition,
        ProviderOperationOutcome? AssociationOutcome,
        ProviderOperationOutcome? RefundDocumentOutcome,
        ProviderOperationOutcome? RefundValueOutcome,
        DateTimeOffset? RetentionSettledAt)
    {
        public bool IsSettled
            => ServicingSettlementRules.IsAncillaryUnitSettled(
                Disposition,
                AssociationOutcome,
                RefundDocumentOutcome,
                RefundValueOutcome,
                RetentionSettledAt);
    }
}
