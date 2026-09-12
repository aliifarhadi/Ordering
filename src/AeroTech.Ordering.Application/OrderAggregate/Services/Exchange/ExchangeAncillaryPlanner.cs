using System.Security.Cryptography;
using System.Text;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
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

            var reissueScope = scope.Coupons.Select(coupon => coupon.CouponNumber).Order().ToList();

            var affected = scope.AffectedAncillaries
                .Select(association => new AffectedAncillaryCoupon(
                    association.Document.DocumentNumber,
                    association.Coupon.CouponNumber,
                    association.Document.Type,
                    association.Coupon.Purpose,
                    association.Coupon.ReasonForIssuanceSubCode,
                    scope.PredecessorTicket.DocumentNumber,
                    association.PredecessorCoupon.CouponNumber))
                .ToList();

            return new AncillaryExchangeDispositionRequest(
                orderId,
                operationId,
                quotedExchangeId,
                scope.PredecessorTicket.DocumentNumber,
                reissueScope,
                affected,
                Fingerprint(orderId, quotedExchangeId, scope.PredecessorTicket.DocumentNumber, reissueScope, affected));
        }

        public static string Fingerprint(AncillaryExchangeDispositionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return Fingerprint(
                request.OrderId,
                request.QuotedExchangeId,
                request.PredecessorDocumentNumber,
                request.ReissueScopeCouponNumbers,
                request.AffectedCoupons);
        }

        private static string Fingerprint(
            long orderId,
            string quotedExchangeId,
            string predecessorDocumentNumber,
            IReadOnlyList<int> reissueScopeCouponNumbers,
            IReadOnlyList<AffectedAncillaryCoupon> affectedCoupons)
        {
            var context = string.Join(
                '|',
                orderId,
                quotedExchangeId,
                predecessorDocumentNumber,
                string.Join(',', reissueScopeCouponNumbers),
                string.Join(
                    ',',
                    affectedCoupons
                        .Select(coupon => string.Join(
                            '~',
                            coupon.EmdDocumentNumber,
                            coupon.EmdCouponNumber,
                            (int)coupon.EmdType,
                            (int)coupon.Purpose,
                            coupon.ReasonForIssuanceSubCode,
                            coupon.PredecessorDocumentNumber,
                            coupon.PredecessorCouponNumber))
                        .Order(StringComparer.Ordinal)));

            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(context)));
        }

        public static IReadOnlyList<AcceptedExchangeAncillaryDisposition> Accept(
            AncillaryExchangeDispositionRequest request,
            ExchangeScope scope,
            IReadOnlyList<AcceptedExchangePlanCoupon> planCoupons,
            AncillaryExchangeDispositionResult decision)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(scope);
            ArgumentNullException.ThrowIfNull(planCoupons);
            ArgumentNullException.ThrowIfNull(decision);

            var quotedExchangeId = request.QuotedExchangeId;

            if (string.IsNullOrWhiteSpace(decision.DecisionReference))
                throw ExceptionFactory.AncillaryDispositionMalformed(quotedExchangeId, "the decision carries no reference");

            if (!string.Equals(decision.QuotedExchangeId, quotedExchangeId, StringComparison.Ordinal))
                throw ExceptionFactory.AncillaryDispositionContextMismatch(
                    quotedExchangeId, $"it names exchange {decision.QuotedExchangeId}");

            if (!string.Equals(decision.ContextFingerprint, request.ContextFingerprint, StringComparison.Ordinal))
                throw ExceptionFactory.AncillaryDispositionContextMismatch(
                    quotedExchangeId, "its context fingerprint does not match the request");

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
                request, scope, planCoupons, decision, association)).ToList();
        }

        private static AcceptedExchangeAncillaryDisposition Accepted(
            AncillaryExchangeDispositionRequest request,
            ExchangeScope scope,
            IReadOnlyList<AcceptedExchangePlanCoupon> planCoupons,
            AncillaryExchangeDispositionResult decision,
            AffectedAncillaryAssociation association)
        {
            var quotedExchangeId = request.QuotedExchangeId;
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

            if (decided.Disposition == AncillaryExchangeDisposition.Refund)
                EnsureRefundIsExecutable(association, decided);

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
                decision.DecisionVersion,
                request.ContextFingerprint,
                decided.Refund?.ApprovedAmount,
                decided.Refund?.CurrencyId,
                decided.Refund?.ApprovedDisposition,
                decided.Refund?.SourceReference,
                decided.Refund?.PricingSource,
                decided.Refund?.PricingLines,
                RefundPriceChangeSetId: null,
                decided.Disposition == AncillaryExchangeDisposition.Refund
                    ? association.Coupon.OrderServiceId
                    : null);
        }

        private static void EnsureRefundIsExecutable(
            AffectedAncillaryAssociation association,
            AncillaryCouponDisposition decided)
        {
            var document = association.Document.DocumentNumber;
            var coupon = association.Coupon.CouponNumber;

            if (decided.Refund is not { } refund)
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(document, coupon, "economics");

            if (refund.ApprovedAmount <= 0m)
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(document, coupon, "amount");

            if (refund.CurrencyId <= 0)
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(document, coupon, "currency");

            if (string.IsNullOrWhiteSpace(refund.ApprovedDisposition))
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(document, coupon, "disposition");

            if (string.IsNullOrWhiteSpace(refund.SourceReference))
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(document, coupon, "source reference");

            if (refund.CurrencyId != association.Coupon.CurrencyId)
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(
                    document, coupon, $"currency matching the document ({association.Coupon.CurrencyId})");

            if (refund.PricingSource is PricingSource.OrderingDerived or 0)
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(
                    document, coupon, "an external pricing source");

            if (refund.PricingLines.Count == 0)
                throw ExceptionFactory.AncillaryRefundEconomicsMissing(document, coupon, "pricing evidence");

            RefundConservationPolicy.EnsureReconciles(refund.PricingLines, refund.ApprovedAmount);

            EnsureReversalsStayWithinTheDocument(association, refund.PricingLines);
        }

        private static void EnsureReversalsStayWithinTheDocument(
            AffectedAncillaryAssociation association,
            IReadOnlyList<AcceptedRefundPricingLine> lines)
        {
            if (association.Coupon.PricingLineId is not { } carried)
                return;

            foreach (var line in lines)
            {
                if (line.ReversesPricingLineId is { } reversed && reversed != carried)
                    throw ExceptionFactory.RefundReversalOutsideDocumentScope(
                        reversed, association.Document.DocumentNumber);
            }
        }

        public static void EnsureExecutable(IReadOnlyList<AcceptedExchangeAncillaryDisposition> dispositions)
        {
            ArgumentNullException.ThrowIfNull(dispositions);

            if (dispositions.FirstOrDefault(disposition =>
                    !disposition.IsReassociation && !disposition.IsRefund) is { } unsupported)
                throw ExceptionFactory.AncillaryDispositionNotExecutable(
                    unsupported.EmdDocumentNumber, unsupported.EmdCouponNumber, unsupported.Disposition);
        }

        private static string Key(AffectedAncillaryAssociation association)
            => $"{association.Document.DocumentNumber}:{association.Coupon.CouponNumber}";

        private static string Key(AncillaryCouponDisposition disposition)
            => $"{disposition.EmdDocumentNumber}:{disposition.EmdCouponNumber}";
    }
}
