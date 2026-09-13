using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record ServicingExchangeGroupCheckpoint(
        ProviderOperationOutcome? ExchangeOutcome,
        bool IsMaterialized,
        bool RequiresFunding,
        ProviderOperationOutcome? FundingCaptureOutcome,
        bool RequiresRefundDue,
        ProviderOperationOutcome? RefundDueOutcome,
        bool RequiresExternalResidual,
        ProviderOperationOutcome? ResidualOutcome)
    {
        public bool IsSettled
            => ServicingSettlementRules.IsExchangeGroupSettled(
                ExchangeOutcome,
                IsMaterialized,
                RequiresFunding,
                FundingCaptureOutcome,
                RequiresRefundDue,
                RefundDueOutcome,
                RequiresExternalResidual,
                ResidualOutcome);
    }
}
