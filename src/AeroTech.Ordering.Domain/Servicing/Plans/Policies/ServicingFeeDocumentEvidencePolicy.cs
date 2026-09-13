using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ServicingFeeDocumentEvidencePolicy
    {
        public static string? Conflict(
            ElectronicMiscDocument existing,
            AcceptedExchangeFeeDocument document,
            long operationId,
            IReadOnlyList<long> primaryPricingLineIds)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(primaryPricingLineIds);

            if (!string.Equals(existing.DocumentNumber, document.AllocatedDocumentNumber, StringComparison.Ordinal))
                return $"document {existing.DocumentNumber} is not the number allocated to "
                       + document.DocumentReference;

            if (existing.OperationId != operationId)
                return $"document {existing.DocumentNumber} belongs to servicing operation {existing.OperationId}";

            if (existing.Type != ElectronicMiscDocumentType.Standalone)
                return $"document {existing.DocumentNumber} is a {existing.Type} document";

            if (existing.IssuerCarrierId != document.IssuerCarrierId)
                return $"document {existing.DocumentNumber} names issuer {existing.IssuerCarrierId}";

            if (existing.TravelerId != document.TravelerId)
                return $"document {existing.DocumentNumber} names traveller {existing.TravelerId}";

            if (!string.Equals(
                    existing.ReasonForIssuanceCode, document.ReasonForIssuanceCode, StringComparison.Ordinal))
                return $"document {existing.DocumentNumber} carries reason for issuance "
                       + existing.ReasonForIssuanceCode;

            if (existing.CurrencyId != document.CurrencyId)
                return $"document {existing.DocumentNumber} carries currency {existing.CurrencyId}";

            if (existing.Coupons.Count != document.Coupons.Count)
                return $"document {existing.DocumentNumber} carries {existing.Coupons.Count} coupons against "
                       + $"{document.Coupons.Count} approved";

            var coupons = existing.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            for (var position = 0; position < coupons.Count; position++)
            {
                var coupon = coupons[position];
                var approved = document.Coupons[position];

                if (coupon.Purpose != EmdCouponPurpose.Fee)
                    return $"coupon {coupon.CouponNumber} carries purpose {coupon.Purpose}";

                if (!string.Equals(
                        coupon.ReasonForIssuanceSubCode,
                        approved.ReasonForIssuanceSubCode,
                        StringComparison.Ordinal))
                    return $"coupon {coupon.CouponNumber} carries sub code {coupon.ReasonForIssuanceSubCode}";

                if (coupon.IssuanceValue != approved.DocumentedAmount)
                    return $"coupon {coupon.CouponNumber} carries value {coupon.IssuanceValue} against an approved "
                           + approved.DocumentedAmount;

                if (coupon.PricingLineId != primaryPricingLineIds[position])
                    return $"coupon {coupon.CouponNumber} names pricing line {coupon.PricingLineId}";
            }

            return null;
        }
    }
}
