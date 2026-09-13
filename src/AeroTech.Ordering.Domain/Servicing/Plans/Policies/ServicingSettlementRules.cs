using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ServicingSettlementRules
    {
        public static bool IsConfirmed(ProviderOperationOutcome? outcome)
            => outcome == ProviderOperationOutcome.Confirmed;

        public static bool IsLegSettled(bool required, ProviderOperationOutcome? outcome)
            => !required || IsConfirmed(outcome);

        public static bool IsMonetarySettled(
            bool requiresFunding,
            ProviderOperationOutcome? fundingCaptureOutcome,
            bool requiresRefundDue,
            ProviderOperationOutcome? refundDueOutcome,
            bool requiresResidual,
            ProviderOperationOutcome? residualOutcome)
            => IsLegSettled(requiresFunding, fundingCaptureOutcome)
               && IsLegSettled(requiresRefundDue, refundDueOutcome)
               && IsLegSettled(requiresResidual, residualOutcome);

        public static bool IsAncillaryRefundSettled(
            ProviderOperationOutcome? refundDocumentOutcome,
            ProviderOperationOutcome? refundValueOutcome)
            => IsConfirmed(refundDocumentOutcome) && IsConfirmed(refundValueOutcome);

        public static bool IsAncillaryUnitSettled(
            AncillaryExchangeDisposition disposition,
            ProviderOperationOutcome? associationOutcome,
            ProviderOperationOutcome? refundDocumentOutcome,
            ProviderOperationOutcome? refundValueOutcome,
            DateTimeOffset? retentionSettledAt)
            => disposition switch
            {
                AncillaryExchangeDisposition.Refund
                    => IsAncillaryRefundSettled(refundDocumentOutcome, refundValueOutcome),
                AncillaryExchangeDisposition.RetainAsResidual
                    => retentionSettledAt is not null,
                _ => IsConfirmed(associationOutcome)
            };

        public static bool IsExchangeGroupSettled(
            ProviderOperationOutcome? exchangeOutcome,
            bool isMaterialized,
            bool requiresFunding,
            ProviderOperationOutcome? fundingCaptureOutcome,
            bool requiresRefundDue,
            ProviderOperationOutcome? refundDueOutcome,
            bool requiresExternalResidual,
            ProviderOperationOutcome? residualOutcome)
            => IsConfirmed(exchangeOutcome)
               && isMaterialized
               && IsMonetarySettled(
                   requiresFunding,
                   fundingCaptureOutcome,
                   requiresRefundDue,
                   refundDueOutcome,
                   requiresExternalResidual,
                   residualOutcome);

        public static bool IsCancelGroupSettled(DateTimeOffset? cancellationSettledAt)
            => cancellationSettledAt is not null;

        public static bool IsFeeDocumentSettled(DateTimeOffset? settledAt)
            => settledAt is not null;

        public static bool IsExecutable(AncillaryExchangeDisposition disposition)
            => disposition is AncillaryExchangeDisposition.ReassociateExisting
                or AncillaryExchangeDisposition.Refund
                or AncillaryExchangeDisposition.ExchangeToNewEmd
                or AncillaryExchangeDisposition.RetainAsResidual
                or AncillaryExchangeDisposition.Cancel;

        public static bool IsGrouped(AncillaryExchangeDisposition disposition)
            => disposition is AncillaryExchangeDisposition.ExchangeToNewEmd
                or AncillaryExchangeDisposition.Cancel;
    }
}
