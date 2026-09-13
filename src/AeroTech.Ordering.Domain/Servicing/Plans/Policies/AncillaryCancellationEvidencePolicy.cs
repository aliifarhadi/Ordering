using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class AncillaryCancellationEvidencePolicy
    {
        public static string? Conflict(
            ElectronicMiscDocument document,
            AcceptedExchangeAncillaryCancelGroup group,
            IReadOnlyList<AcceptedExchangeAncillaryDisposition> members)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(members);

            if (document.WholeDocumentCancellationConflict(group.EmdCouponNumbers) is { } scope)
                return scope;

            if (members.Count != group.EmdCouponNumbers.Count)
                return $"miscellaneous document {group.EmdDocumentNumber} carries "
                       + $"{members.Count} approved cancellations for {group.EmdCouponNumbers.Count} approved coupons";

            foreach (var member in members)
            {
                var coupon = document.Coupons
                    .SingleOrDefault(candidate => candidate.CouponNumber == member.EmdCouponNumber);

                if (coupon is null)
                    return $"coupon {member.EmdCouponNumber} is no longer carried by miscellaneous document "
                           + group.EmdDocumentNumber;

                if (coupon.Id != member.EmdCouponId)
                    return $"coupon {member.EmdCouponNumber} of miscellaneous document {group.EmdDocumentNumber} "
                           + "is no longer the approved coupon";

                if (coupon.OrderServiceId != member.CancelledOrderServiceId)
                    return $"coupon {member.EmdCouponNumber} of miscellaneous document {group.EmdDocumentNumber} "
                           + "no longer names the approved order service";
            }

            return null;
        }
    }
}
