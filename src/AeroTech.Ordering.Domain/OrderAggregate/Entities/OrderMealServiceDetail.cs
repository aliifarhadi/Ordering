using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderMealServiceDetail : Entity<long>
    {
        private OrderMealServiceDetail()
        {
        }

        internal OrderMealServiceDetail(
            long id,
            long orderServiceId,
            string mealCode,
            int quantity,
            string? specialMealCode)
        {
            if (quantity <= 0)
                throw ExceptionFactory.MealQuantityMustBePositive();

            Id = id;
            OrderServiceId = orderServiceId;
            MealCode = mealCode;
            Quantity = quantity;
            SpecialMealCode = specialMealCode;
        }

        public long OrderServiceId { get; private set; }

        public string MealCode { get; private set; } = default!;

        public int Quantity { get; private set; }

        public string? SpecialMealCode { get; private set; }
    }
}
