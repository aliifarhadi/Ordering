using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public static class ExchangeAncillaryPlanner
    {
        public static AncillaryExchangeDispositionRequest Request(
            long orderId,
            long operationId,
            string quotedExchangeId,
            ExchangeScope scope)
        {
            ArgumentNullException.ThrowIfNull(scope);

            return new AncillaryExchangeDispositionRequest(
                orderId,
                operationId,
                quotedExchangeId,
                scope.PredecessorTicket.DocumentNumber,
                scope.Coupons.Select(coupon => coupon.CouponNumber).Order().ToList(),
                scope.AffectedAncillaries
                    .Select(association => new AffectedAncillaryCoupon(
                        association.Document.DocumentNumber,
                        association.Coupon.CouponNumber,
                        association.Document.Type,
                        association.Coupon.Purpose,
                        association.Coupon.ReasonForIssuanceSubCode,
                        scope.PredecessorTicket.DocumentNumber,
                        association.PredecessorCoupon.CouponNumber))
                    .ToList());
        }

        public static IReadOnlyList<AcceptedExchangeAncillaryDisposition> Accept(
            string quotedExchangeId,
            ExchangeScope scope,
            IReadOnlyList<AcceptedExchangePlanCoupon> planCoupons,
            AncillaryExchangeDispositionResult decision)
        {
            ArgumentNullException.ThrowIfNull(scope);
            ArgumentNullException.ThrowIfNull(planCoupons);
            ArgumentNullException.ThrowIfNull(decision);

            if (string.IsNullOrWhiteSpace(decision.DecisionReference))
                throw ExceptionFactory.AncillaryDispositionMalformed(quotedExchangeId, "the decision carries no reference");

            var affected = scope.AffectedAncillaries;
            var decided = decision.Dispositions;

            if (decided.Select(Key).Distinct(StringComparer.Ordinal).Count() != decided.Count)
                throw ExceptionFactory.AncillaryDispositionMalformed(quotedExchangeId, "duplicate coupon decision");

            foreach (var disposition in decided)
                if (!affected.Any(association => Key(association) == Key(disposition)))
                    throw ExceptionFactory.AncillaryDispositionMalformed(
                        quotedExchangeId,
                        $"decision for {disposition.EmdDocumentNumber} coupon {disposition.EmdCouponNumber} names no affected ancillary");

            return affected.Select(association => Accepted(
                quotedExchangeId, scope, planCoupons, decision, association)).ToList();
        }

        private static AcceptedExchangeAncillaryDisposition Accepted(
            string quotedExchangeId,
            ExchangeScope scope,
            IReadOnlyList<AcceptedExchangePlanCoupon> planCoupons,
            AncillaryExchangeDispositionResult decision,
            AffectedAncillaryAssociation association)
        {
            var decided = decision.Dispositions.SingleOrDefault(candidate => Key(candidate) == Key(association))
                          ?? throw ExceptionFactory.AncillaryDispositionMissing(
                              association.Document.DocumentNumber, association.Coupon.CouponNumber);

            if (decided.PredecessorCouponNumber != association.PredecessorCoupon.CouponNumber
                || !string.Equals(
                    decided.PredecessorDocumentNumber,
                    scope.PredecessorTicket.DocumentNumber,
                    StringComparison.Ordinal))
                throw ExceptionFactory.AncillaryDispositionMalformed(
                    quotedExchangeId,
                    $"decision for {decided.EmdDocumentNumber} coupon {decided.EmdCouponNumber} names an unrelated predecessor coupon");

            long? targetSuccessorCouponId = null;

            if (decided.Disposition == AncillaryExchangeDisposition.ReassociateExisting)
            {
                if (decided.TargetPredecessorCouponNumber is not { } target)
                    throw ExceptionFactory.AncillaryDispositionMalformed(
                        quotedExchangeId,
                        $"reassociation of {decided.EmdDocumentNumber} coupon {decided.EmdCouponNumber} names no target coupon");

                if (scope.HistoricalUsedCoupons.Any(used => used.CouponNumber == target))
                    throw ExceptionFactory.AncillaryDispositionMalformed(
                        quotedExchangeId,
                        $"reassociation target coupon {target} is historical used context");

                var planCoupon = planCoupons.SingleOrDefault(coupon => coupon.PredecessorCouponNumber == target)
                                 ?? throw ExceptionFactory.AncillaryDispositionMalformed(
                                     quotedExchangeId,
                                     $"reassociation target coupon {target} is outside the accepted successor scope");

                targetSuccessorCouponId = planCoupon.SuccessorTicketCouponId;
            }

            return new AcceptedExchangeAncillaryDisposition(
                association.Document.Id,
                association.Document.DocumentNumber,
                association.Coupon.CouponNumber,
                association.Coupon.Id,
                association.PredecessorCoupon.Id,
                scope.PredecessorTicket.DocumentNumber,
                association.PredecessorCoupon.CouponNumber,
                decided.Disposition,
                decided.TargetPredecessorCouponNumber,
                targetSuccessorCouponId,
                decision.DecisionReference,
                decision.DecisionVersion);
        }

        public static void EnsureExecutable(IReadOnlyList<AcceptedExchangeAncillaryDisposition> dispositions)
        {
            ArgumentNullException.ThrowIfNull(dispositions);

            if (dispositions.FirstOrDefault(disposition => !disposition.IsReassociation) is { } unsupported)
                throw ExceptionFactory.AncillaryDispositionNotExecutable(
                    unsupported.EmdDocumentNumber, unsupported.EmdCouponNumber, unsupported.Disposition);
        }

        private static string Key(AffectedAncillaryAssociation association)
            => $"{association.Document.DocumentNumber}:{association.Coupon.CouponNumber}";

        private static string Key(AncillaryCouponDisposition disposition)
            => $"{disposition.EmdDocumentNumber}:{disposition.EmdCouponNumber}";
    }
}
