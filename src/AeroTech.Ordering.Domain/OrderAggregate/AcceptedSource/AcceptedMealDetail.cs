namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedMealDetail(
        string MealCode,
        int Quantity,
        string? SpecialMealCode = null) : AcceptedServiceDetail;
}
