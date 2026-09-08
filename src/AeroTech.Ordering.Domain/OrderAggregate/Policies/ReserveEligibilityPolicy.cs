using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class ReserveEligibilityPolicy
    {
        public static EligibilityDecision Evaluate(Order order, IReadOnlyCollection<long> alreadyReservedServiceIds)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.CommercialSummary is not (CommercialSummary.Draft or CommercialSummary.Active))
                return EligibilityDecision.Denied(EligibilityReasonCodes.OrderNotCommerciallyActive);

            var scope = order.OrderServices
                .Where(service => service.RequiresReservation
                                  && service.Status != OrderServiceStatus.Cancelled
                                  && !alreadyReservedServiceIds.Contains(service.Id))
                .Select(service => service.Id)
                .ToList();

            if (scope.Count == 0)
            {
                return order.OrderServices.Any(service => service.RequiresReservation)
                    ? EligibilityDecision.Denied(EligibilityReasonCodes.AlreadyReserved)
                    : EligibilityDecision.Denied(EligibilityReasonCodes.NoEligibleServices);
            }

            return EligibilityDecision.Allowed(scope);
        }
    }

    public static class WithdrawEligibilityPolicy
    {
        public static EligibilityDecision Evaluate(Order order, IReadOnlyCollection<long> documentedServiceIds)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.CommercialSummary is CommercialSummary.Cancelled or CommercialSummary.Closed)
                return EligibilityDecision.Denied(EligibilityReasonCodes.OrderNotCommerciallyActive);

            if (documentedServiceIds.Count > 0)
                return EligibilityDecision.Denied(EligibilityReasonCodes.AlreadyIssued);

            var scope = order.OrderServices
                .Where(service => service.Status != OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToList();

            return scope.Count == 0
                ? EligibilityDecision.Denied(EligibilityReasonCodes.NoEligibleServices)
                : EligibilityDecision.Allowed(scope);
        }
    }
}
