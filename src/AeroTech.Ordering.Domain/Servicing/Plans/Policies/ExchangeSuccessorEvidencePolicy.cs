using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ExchangeSuccessorEvidencePolicy
    {
        public static bool CoversEveryCoupon(AcceptedExchangePlan plan, SuccessorDocumentIdentity successor)
        {
            var planned = plan.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToHashSet();
            var reported = successor.Coupons.Select(identity => identity.PredecessorCouponNumber).ToList();

            if (reported.Count != planned.Count || reported.Distinct().Count() != reported.Count)
                return false;

            if (!reported.All(planned.Contains))
                return false;

            var issued = successor.Coupons.Select(identity => identity.CouponNumber).ToList();

            return issued.All(number => number >= 1) && issued.Distinct().Count() == issued.Count;
        }

        public static bool ContradictsDurableEvidence(AcceptedExchangePlan plan, DocumentExchangeResult result)
        {
            if (plan.Successor is { } known && result.Successor is { } reported)
            {
                if (!string.Equals(known.DocumentNumber, reported.DocumentNumber, StringComparison.Ordinal))
                    return true;

                foreach (var identity in known.Coupons)
                {
                    var reportedCoupon = reported.Coupons.FirstOrDefault(candidate =>
                        candidate.PredecessorCouponNumber == identity.PredecessorCouponNumber);

                    if (reportedCoupon is not null && reportedCoupon.CouponNumber != identity.CouponNumber)
                        return true;
                }
            }

            return plan.DocumentExchangeProviderReference is { } knownReference
                   && result.ProviderReference is { } reportedReference
                   && !string.Equals(knownReference, reportedReference, StringComparison.Ordinal);
        }
    }
}
