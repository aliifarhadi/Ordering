using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.EmdExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class AncillaryExchangeEvidencePolicy
    {
        public static string? Contradiction(
            AcceptedExchangeAncillaryExchangeGroup group,
            string successorTicketDocumentNumber,
            IReadOnlyList<int?> expectedTicketCouponNumbers,
            EmdExchangeResult result)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(expectedTicketCouponNumbers);
            ArgumentNullException.ThrowIfNull(result);

            if (string.IsNullOrWhiteSpace(result.ProviderReference))
                return "the confirmed miscellaneous document exchange carries no provider reference";

            if (result.Successor is not { } successor)
                return "the confirmed miscellaneous document exchange returned no successor document";

            if (string.IsNullOrWhiteSpace(successor.DocumentNumber))
                return "the returned successor miscellaneous document carries no document number";

            if (successor.Type != group.SuccessorType)
                return $"the returned successor is {successor.Type} against an accepted {group.SuccessorType}";

            if (successor.CurrencyId != group.CurrencyId)
                return $"the returned successor uses currency {successor.CurrencyId} "
                       + $"against an accepted {group.CurrencyId}";

            if (!string.Equals(
                    successor.ReasonForIssuanceCode,
                    group.SuccessorReasonForIssuanceCode,
                    StringComparison.Ordinal))
                return $"the returned successor carries reason for issuance {successor.ReasonForIssuanceCode} "
                       + $"against an accepted {group.SuccessorReasonForIssuanceCode}";

            if (successor.Coupons.Count != group.SuccessorCoupons.Count)
                return $"the returned successor carries {successor.Coupons.Count} coupons "
                       + $"against an accepted {group.SuccessorCoupons.Count}";

            for (var index = 0; index < successor.Coupons.Count; index++)
            {
                if (CouponContradiction(
                        group, successorTicketDocumentNumber, expectedTicketCouponNumbers, successor, index)
                    is { } contradiction)
                    return contradiction;
            }

            return ResidualContradiction(group, result.Residual);
        }

        private static string? CouponContradiction(
            AcceptedExchangeAncillaryExchangeGroup group,
            string successorTicketDocumentNumber,
            IReadOnlyList<int?> expectedTicketCouponNumbers,
            SuccessorEmdIdentity successor,
            int index)
        {
            var accepted = group.SuccessorCoupons[index];
            var returned = successor.Coupons[index];

            if (returned.Purpose != accepted.Purpose)
                return $"successor coupon {returned.CouponNumber} is {returned.Purpose} "
                       + $"against an accepted {accepted.Purpose}";

            if (!string.Equals(
                    returned.ReasonForIssuanceSubCode,
                    accepted.ReasonForIssuanceSubCode,
                    StringComparison.Ordinal))
                return $"successor coupon {returned.CouponNumber} carries sub code "
                       + $"{returned.ReasonForIssuanceSubCode} against an accepted {accepted.ReasonForIssuanceSubCode}";

            if (returned.Value != accepted.Value)
                return $"successor coupon {returned.CouponNumber} is for {returned.Value} "
                       + $"against an accepted {accepted.Value}";

            if (returned.CurrencyId != accepted.CurrencyId)
                return $"successor coupon {returned.CouponNumber} uses currency {returned.CurrencyId} "
                       + $"against an accepted {accepted.CurrencyId}";

            if (!group.IsAssociatedSuccessor)
                return returned.AssociatedTicketCouponNumber is null
                    ? null
                    : $"successor coupon {returned.CouponNumber} claims a ticket association "
                      + "on a standalone document";

            if (returned.AssociatedTicketCouponNumber is not { } associated)
                return $"successor coupon {returned.CouponNumber} carries no ticket association";

            var expected = expectedTicketCouponNumbers.Count > index
                ? expectedTicketCouponNumbers[index]
                : null;

            if (expected is null)
                return $"successor coupon {returned.CouponNumber} names ticket coupon "
                       + $"{accepted.TargetPredecessorCouponNumber}, which the reissue did not replace";

            if (associated != expected)
                return $"successor coupon {returned.CouponNumber} is associated to ticket coupon {associated} "
                       + $"against an accepted {expected}";

            return successor.AssociatedTicketDocumentNumber is { } document
                   && !string.Equals(document, successorTicketDocumentNumber, StringComparison.Ordinal)
                ? $"the returned successor is associated to ticket {document} "
                  + $"against the successor ticket {successorTicketDocumentNumber}"
                : null;
        }

        private static string? ResidualContradiction(
            AcceptedExchangeAncillaryExchangeGroup group,
            ResidualDocumentIdentity? residual)
        {
            if (group.Residual is not { } obligation || !obligation.IsDocumentCoupled)
                return residual is null
                    ? null
                    : "the miscellaneous document exchange returned a coupled residual document "
                      + "for an obligation that owes none";

            if (residual is null)
                return "the confirmed miscellaneous document exchange returned no coupled residual document";

            if (string.IsNullOrWhiteSpace(residual.DocumentNumber))
                return "the returned coupled residual document carries no document number";

            if (residual.Amount != obligation.Amount)
                return $"the returned coupled residual document is for {residual.Amount} "
                       + $"against an obligation of {obligation.Amount}";

            return residual.CurrencyId != obligation.CurrencyId
                ? $"the returned coupled residual document uses currency {residual.CurrencyId} "
                  + $"against an obligation in {obligation.CurrencyId}"
                : null;
        }

        public static string? FundingContradiction(
            AcceptedExchangeAncillaryExchangeGroup group,
            decimal? amount,
            int? currencyId)
        {
            ArgumentNullException.ThrowIfNull(group);

            if (group.AddCollect is not { } obligation)
                return "the funding act confirms money for an exchange group that owes nothing";

            if (amount is { } confirmed && confirmed != obligation.Amount)
                return $"the funding act moved {confirmed} against an obligation of {obligation.Amount}";

            return currencyId is { } currency && currency != obligation.CurrencyId
                ? $"the funding act used currency {currency} against an obligation in {obligation.CurrencyId}"
                : null;
        }
    }
}
