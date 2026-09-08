using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.OrderAggregate
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ExchangeRatePrecisionTests
    {
        [Theory]
        [InlineData("OrderPricingLines")]
        [InlineData("OrderPricingAllocations")]
        public async Task The_accepted_rate_of_exchange_keeps_the_owners_precision(string table)
        {
            await using var connection = new SqlConnection(OrderingDatabaseFixture.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'Order' AND TABLE_NAME = @table AND COLUMN_NAME = 'RateOfExchange'
                """;
            command.Parameters.Add(new SqlParameter("@table", table));

            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync(), $"[Order].[{table}].RateOfExchange was not found.");
            Assert.Equal(28, reader.GetByte(0));
            Assert.Equal(12, reader.GetInt32(1));
        }

        [Fact]
        public async Task A_twelve_decimal_rate_survives_the_round_trip()
        {
            const decimal rate = 0.000024751234m;

            await using var connection = new SqlConnection(OrderingDatabaseFixture.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(@rate AS decimal(28,12))";
            command.Parameters.Add(new SqlParameter("@rate", rate));

            var stored = (decimal)(await command.ExecuteScalarAsync())!;

            Assert.Equal(rate, stored);
        }
    }
}
