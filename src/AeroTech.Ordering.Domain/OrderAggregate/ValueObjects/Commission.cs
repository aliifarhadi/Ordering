using AeroTech.Framework.Core.Domain.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class Commission : ValueObject
    {
        private Commission()
        {
        }

        public Commission(decimal commissionRate, decimal totalAmount)
        {
            CommissionRate = commissionRate;
            CommissionAmount = commissionRate <= 0 ? 0m : totalAmount * commissionRate / 100m;
        }

        public decimal CommissionRate { get; private set; }

        public decimal CommissionAmount { get; private set; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return CommissionRate;
            yield return CommissionAmount;
        }
    }
}
