using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.Ports.EmdExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ElectronicMiscDocumentIdentityPolicy
    {
        public static string? Conflict(
            ElectronicMiscDocument existing,
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorEmdIdentity successor)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(successor);

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

            if (existing.OperationId != group.SourceElectronicMiscDocumentId
                && !existing.Coupons.Any(coupon =>
                    coupon.PredecessorElectronicMiscDocumentId == group.SourceElectronicMiscDocumentId))
                return Mismatch(successor.DocumentNumber, "exchange lineage");

            foreach (var returned in successor.Coupons)
            {
                var local = existing.Coupons.FirstOrDefault(coupon => coupon.CouponNumber == returned.CouponNumber);

                if (local is null)
                    return Mismatch(successor.DocumentNumber, $"coupon {returned.CouponNumber}");

                if (local.Purpose != returned.Purpose
                    || !string.Equals(
                        local.ReasonForIssuanceSubCode,
                        returned.ReasonForIssuanceSubCode,
                        StringComparison.Ordinal)
                    || local.IssuanceValue != returned.Value
                    || local.CurrencyId != returned.CurrencyId)
                    return Mismatch(successor.DocumentNumber, $"coupon {returned.CouponNumber} identity");
            }

            return null;
        }

        private static string Mismatch(string documentNumber, string facet)
            => $"miscellaneous document {documentNumber} already exists locally with a different {facet}";
    }
}
