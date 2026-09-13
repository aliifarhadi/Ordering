using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ServicingFeeDocumentEvidencePolicy
    {
        public static string? Conflict(
            ElectronicMiscDocument existing,
            ResolvedServicingFeeDocument resolved,
            long operationId)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(resolved);

            var document = resolved.Document;

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

            if (existing.Coupons.Count != resolved.Coupons.Count)
                return $"document {existing.DocumentNumber} carries {existing.Coupons.Count} coupons against "
                       + $"{resolved.Coupons.Count} approved";

            if (existing.PriceLinks.Count != resolved.LinkCount)
                return $"document {existing.DocumentNumber} carries {existing.PriceLinks.Count} price links "
                       + $"against {resolved.LinkCount} approved attributions";

            var coupons = existing.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            for (var position = 0; position < coupons.Count; position++)
                if (CouponConflict(existing, coupons[position], resolved.Coupons[position]) is { } conflict)
                    return conflict;

            return null;
        }

        private static string? CouponConflict(
            ElectronicMiscDocument existing,
            EmdCoupon coupon,
            ResolvedServicingFeeCoupon resolved)
        {
            if (coupon.Purpose != EmdCouponPurpose.Fee)
                return $"coupon {coupon.CouponNumber} carries purpose {coupon.Purpose}";

            if (coupon.OrderServiceId is not null)
                return $"coupon {coupon.CouponNumber} names order service {coupon.OrderServiceId}";

            if (coupon.AssociatedTicketCouponId is not null)
                return $"coupon {coupon.CouponNumber} is associated to ticket coupon "
                       + coupon.AssociatedTicketCouponId;

            if (coupon.ExternalValueReference is not null)
                return $"coupon {coupon.CouponNumber} carries an external value reference";

            if (!string.Equals(
                    coupon.ReasonForIssuanceSubCode,
                    resolved.Accepted.ReasonForIssuanceSubCode,
                    StringComparison.Ordinal))
                return $"coupon {coupon.CouponNumber} carries sub code {coupon.ReasonForIssuanceSubCode}";

            if (coupon.IssuanceValue != resolved.Accepted.DocumentedAmount)
                return $"coupon {coupon.CouponNumber} carries value {coupon.IssuanceValue} against an approved "
                       + resolved.Accepted.DocumentedAmount;

            if (coupon.PricingLineId != resolved.PrimaryPricingLineId)
                return $"coupon {coupon.CouponNumber} names primary pricing line {coupon.PricingLineId}";

            return LinkConflict(existing, coupon, resolved);
        }

        private static string? LinkConflict(
            ElectronicMiscDocument existing,
            EmdCoupon coupon,
            ResolvedServicingFeeCoupon resolved)
        {
            var links = existing.PriceLinks.Where(link => link.EmdCouponId == coupon.Id).ToList();

            if (links.Count != resolved.PriceLinks.Count)
                return $"coupon {coupon.CouponNumber} carries {links.Count} price links against "
                       + $"{resolved.PriceLinks.Count} approved attributions";

            if (links.Select(link => link.PricingLineId).Distinct().Count() != links.Count)
                return $"coupon {coupon.CouponNumber} repeats a price link";

            foreach (var expected in resolved.PriceLinks)
            {
                if (links.SingleOrDefault(link => link.PricingLineId == expected.PricingLineId) is not { } link)
                    return $"coupon {coupon.CouponNumber} carries no price link to pricing line "
                           + expected.PricingLineId;

                if (link.AttributedValue != expected.AttributedValue)
                    return $"coupon {coupon.CouponNumber} attributes {link.AttributedValue} to pricing line "
                           + $"{expected.PricingLineId} against an approved {expected.AttributedValue}";

                if (link.AllocationId is not null)
                    return $"coupon {coupon.CouponNumber} binds pricing line {expected.PricingLineId} to "
                           + $"allocation {link.AllocationId}";

                if (link.CurrencyId != existing.CurrencyId)
                    return $"coupon {coupon.CouponNumber} carries currency {link.CurrencyId} on pricing line "
                           + expected.PricingLineId;
            }

            return null;
        }
    }
}
