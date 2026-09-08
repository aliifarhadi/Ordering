namespace AeroTech.Ordering.Persistence.Migrations
{
    public static class P2D1ServicePriceTreatmentBackfill
    {
        public const string Sql = """
            UPDATE s
            SET s.[PriceTreatment] = CASE
                WHEN EXISTS (
                    SELECT 1 FROM [Order].[OrderPricingLines] l
                    WHERE l.[OrderId] = s.[OrderId]
                      AND l.[Effect] = 1
                      AND l.[LineRole] = 1
                      AND l.[ComponentType] IN (1, 2)
                      AND l.[BasisType] = 3
                      AND l.[BasisReferenceId] = s.[Id])
                THEN 1
                WHEN EXISTS (
                    SELECT 1 FROM [Order].[OrderPricingLines] l
                    WHERE l.[OrderId] = s.[OrderId]
                      AND l.[Effect] = 1
                      AND l.[LineRole] = 1
                      AND l.[ComponentType] IN (1, 2)
                      AND l.[BasisType] = 2
                      AND l.[BasisReferenceId] = s.[OrderItemId])
                THEN 2
                ELSE 4
            END
            FROM [Order].[OrderServices] s
            WHERE s.[PriceTreatment] <> 3;
            """;
    }
}
