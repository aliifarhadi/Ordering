using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class PricingComponentPolicy
    {
        public static void EnsurePermitted(
            PricingComponentType componentType,
            PricingEffect effect,
            OrderPricingLineDirection direction,
            PricingLineRole lineRole,
            string? code,
            string? settlementPartyRef,
            string? settlementCategory)
        {
            if (componentType == PricingComponentType.Tax && effect == PricingEffect.SettlementOnly)
                throw ExceptionFactory.TaxCannotBeSettlementOnly();

            if (componentType == PricingComponentType.Commission && effect == PricingEffect.CustomerBalance)
                throw ExceptionFactory.CommissionCannotAffectCustomerBalance();

            if (componentType == PricingComponentType.Other && effect != PricingEffect.Informational)
                throw ExceptionFactory.PricingComponentNotPermitted(componentType, effect);

            if (componentType is PricingComponentType.Adjustment or PricingComponentType.Other
                && string.IsNullOrWhiteSpace(code))
                throw ExceptionFactory.PricingComponentRequiresCode(componentType);

            if (effect == PricingEffect.SettlementOnly
                && (string.IsNullOrWhiteSpace(settlementPartyRef) || string.IsNullOrWhiteSpace(settlementCategory)))
                throw ExceptionFactory.SettlementLineRequiresParty();

            if (effect != PricingEffect.CustomerBalance)
                return;

            if (NormalDirection(componentType) is not { } normal)
                return;

            var expected = lineRole == PricingLineRole.Reversal ? Opposite(normal) : normal;

            if (lineRole is PricingLineRole.Adjustment or PricingLineRole.Transfer)
                return;

            if (direction != expected)
                throw ExceptionFactory.PricingDirectionNotPermitted(componentType, direction);
        }

        public static OrderPricingLineDirection Opposite(OrderPricingLineDirection direction)
            => direction == OrderPricingLineDirection.Debit
                ? OrderPricingLineDirection.Credit
                : OrderPricingLineDirection.Debit;

        public static int Sign(OrderPricingLineDirection direction)
            => direction == OrderPricingLineDirection.Debit ? 1 : -1;

        private static OrderPricingLineDirection? NormalDirection(PricingComponentType componentType)
            => componentType switch
            {
                PricingComponentType.Fare => OrderPricingLineDirection.Debit,
                PricingComponentType.ProductCharge => OrderPricingLineDirection.Debit,
                PricingComponentType.Tax => OrderPricingLineDirection.Debit,
                PricingComponentType.CarrierSurcharge => OrderPricingLineDirection.Debit,
                PricingComponentType.Fee => OrderPricingLineDirection.Debit,
                PricingComponentType.Markup => OrderPricingLineDirection.Debit,
                PricingComponentType.Penalty => OrderPricingLineDirection.Debit,
                PricingComponentType.Discount => OrderPricingLineDirection.Credit,
                _ => null
            };
    }
}
