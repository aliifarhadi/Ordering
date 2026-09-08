using AeroTech.Framework.Core.Domain.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class OrderAmount : ValueObject
    {
        private OrderAmount()
        {
        }

        public OrderAmount(
            decimal baseFareTotal,
            decimal taxTotal,
            decimal feeTotal,
            decimal surchargeTotal,
            decimal discountTotal,
            decimal penaltyTotal,
            decimal ancillaryTotal,
            decimal grandTotal)
        {
            BaseFareTotal = baseFareTotal;
            TaxTotal = taxTotal;
            FeeTotal = feeTotal;
            SurchargeTotal = surchargeTotal;
            DiscountTotal = discountTotal;
            PenaltyTotal = penaltyTotal;
            AncillaryTotal = ancillaryTotal;
            GrandTotal = grandTotal;
        }

        public decimal BaseFareTotal { get; private set; }

        public decimal TaxTotal { get; private set; }

        public decimal FeeTotal { get; private set; }

        public decimal SurchargeTotal { get; private set; }

        public decimal DiscountTotal { get; private set; }

        public decimal PenaltyTotal { get; private set; }

        public decimal AncillaryTotal { get; private set; }

        public decimal GrandTotal { get; private set; }

        public static OrderAmount Zero() => new(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return BaseFareTotal;
            yield return TaxTotal;
            yield return FeeTotal;
            yield return SurchargeTotal;
            yield return DiscountTotal;
            yield return PenaltyTotal;
            yield return AncillaryTotal;
            yield return GrandTotal;
        }
    }
}
