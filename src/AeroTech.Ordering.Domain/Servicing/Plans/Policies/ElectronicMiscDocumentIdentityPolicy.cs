using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.EmdExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ElectronicMiscDocumentIdentityPolicy
    {
        public static string? Conflict(
            ElectronicMiscDocument existing,
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorEmdIdentity successor,
            long operationId,
            long? beneficiaryTravellerId)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(successor);

            if (existing.OperationId != operationId)
                return Mismatch(successor.DocumentNumber, "origin servicing operation");

            if (existing.TravelerId != beneficiaryTravellerId)
                return Mismatch(successor.DocumentNumber, "beneficiary");

            if (existing.Type != successor.Type)
                return Mismatch(successor.DocumentNumber, "document type");

            if (existing.CurrencyId != successor.CurrencyId)
                return Mismatch(successor.DocumentNumber, "currency");

            if (existing.IssuerCarrierId != successor.IssuerCarrierId)
                return Mismatch(successor.DocumentNumber, "issuer carrier");

            if (existing.IssuingOfficeId != successor.IssuingOfficeId)
                return Mismatch(successor.DocumentNumber, "issuing office");

            if (existing.Authority != successor.Authority)
                return Mismatch(successor.DocumentNumber, "document authority");

            if (!string.Equals(
                    existing.ReasonForIssuanceCode,
                    successor.ReasonForIssuanceCode,
                    StringComparison.Ordinal))
                return Mismatch(successor.DocumentNumber, "reason for issuance");

            if (existing.Coupons.Count != successor.Coupons.Count)
                return Mismatch(successor.DocumentNumber, "coupon count");

            for (var index = 0; index < successor.Coupons.Count; index++)
            {
                if (CouponConflict(existing, group, successor, index) is { } conflict)
                    return conflict;
            }

            return null;
        }

        private static string? CouponConflict(
            ElectronicMiscDocument existing,
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorEmdIdentity successor,
            int index)
        {
            var returned = successor.Coupons[index];
            var accepted = group.SuccessorCoupons[index];
            var document = successor.DocumentNumber;

            if (existing.Coupons.FirstOrDefault(coupon => coupon.CouponNumber == returned.CouponNumber)
                is not { } local)
                return Mismatch(document, $"coupon {returned.CouponNumber}");

            if (local.Purpose != returned.Purpose
                || !string.Equals(
                    local.ReasonForIssuanceSubCode,
                    returned.ReasonForIssuanceSubCode,
                    StringComparison.Ordinal)
                || local.IssuanceValue != returned.Value
                || local.CurrencyId != returned.CurrencyId)
                return Mismatch(document, $"coupon {returned.CouponNumber} identity");

            if (local.PredecessorElectronicMiscDocumentId != group.SourceElectronicMiscDocumentId
                || !string.Equals(
                    local.PredecessorDocumentNumber,
                    group.SourceDocumentNumber,
                    StringComparison.Ordinal)
                || local.PredecessorCouponNumber != group.SourceCouponNumbers[index])
                return Mismatch(document, $"coupon {returned.CouponNumber} exchange lineage");

            if (group.IsAssociatedSuccessor)
            {
                if (local.AssociatedTicketCouponId != accepted.TargetSuccessorTicketCouponId)
                    return Mismatch(document, $"coupon {returned.CouponNumber} ticket association");
            }
            else if (local.AssociatedTicketCouponId is not null)
            {
                return Mismatch(document, $"coupon {returned.CouponNumber} ticket association");
            }

            if (accepted.Purpose == EmdCouponPurpose.Service
                && local.OrderServiceId != accepted.OrderServiceId)
                return Mismatch(document, $"coupon {returned.CouponNumber} order service");

            return accepted.ExternalValueReference is { } externalReference
                   && !string.Equals(local.ExternalValueReference, externalReference, StringComparison.Ordinal)
                ? Mismatch(document, $"coupon {returned.CouponNumber} external value reference")
                : null;
        }

        private static string Mismatch(string documentNumber, string facet)
            => $"miscellaneous document {documentNumber} already exists locally with a different {facet}";
    }
}
