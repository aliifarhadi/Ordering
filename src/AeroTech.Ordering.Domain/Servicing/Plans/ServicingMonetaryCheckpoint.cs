using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record ServicingMonetaryCheckpoint(
        bool RequiresFunding,
        ProviderOperationOutcome? FundingCaptureOutcome,
        bool RequiresRefundDue,
        ProviderOperationOutcome? RefundDueOutcome,
        bool RequiresResidual,
        ProviderOperationOutcome? ResidualOutcome)
    {
        public bool IsRequired => RequiresFunding || RequiresRefundDue || RequiresResidual;

        public bool IsSettled
            => ServicingSettlementRules.IsMonetarySettled(
                RequiresFunding,
                FundingCaptureOutcome,
                RequiresRefundDue,
                RefundDueOutcome,
                RequiresResidual,
                ResidualOutcome);
    }
}
