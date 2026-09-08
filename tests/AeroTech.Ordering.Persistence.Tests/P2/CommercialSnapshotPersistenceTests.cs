using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class CommercialSnapshotPersistenceTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public CommercialSnapshotPersistenceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_accepted_product_and_terms_snapshots_survive_a_reload_unchanged()
        {
            await using var harness = new OrderSliceHarness(
                _fixture,
                TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

            var created = await harness.CreateOrderAsync();

            await using var reload = _fixture.NewCommandContext();

            var order = await new Persistence.OrderAggregate.OrderRepository(reload).GetAsync(created.Id);

            Assert.NotNull(order);
            Assert.NotEmpty(order!.Items);

            foreach (var item in order.Items)
            {
                var expected = created.Items.Single(candidate => candidate.Id == item.Id);

                Assert.Equal(expected.ProductSnapshot.ProductType, item.ProductSnapshot.ProductType);
                Assert.Equal(expected.ProductSnapshot.SourceProductReference, item.ProductSnapshot.SourceProductReference);
                Assert.Equal(expected.ProductSnapshot.SourceSystem, item.ProductSnapshot.SourceSystem);
                Assert.Equal(expected.ProductSnapshot.SourceOfferId, item.ProductSnapshot.SourceOfferId);
                Assert.Equal(expected.ProductSnapshot.BrandName, item.ProductSnapshot.BrandName);
                Assert.Equal(expected.ProductSnapshot.BrandCode, item.ProductSnapshot.BrandCode);
                Assert.Equal(expected.ProductSnapshot.MarketingAirlineId, item.ProductSnapshot.MarketingAirlineId);
                Assert.Equal(expected.ProductSnapshot.OperatingAirlineId, item.ProductSnapshot.OperatingAirlineId);
                Assert.Null(item.ProductSnapshot.ProductCode);
                Assert.Null(item.ProductSnapshot.ProductName);

                Assert.Equal(expected.CommercialTermsSnapshot.RefundabilitySummary, item.CommercialTermsSnapshot.RefundabilitySummary);
                Assert.Equal(expected.CommercialTermsSnapshot.ChangeabilitySummary, item.CommercialTermsSnapshot.ChangeabilitySummary);
                Assert.Equal(expected.CommercialTermsSnapshot.UpgradeEligibilitySummary, item.CommercialTermsSnapshot.UpgradeEligibilitySummary);
                Assert.Equal(expected.CommercialTermsSnapshot.SourceSystem, item.CommercialTermsSnapshot.SourceSystem);
                Assert.Equal(expected.CommercialTermsSnapshot.SourcePolicyReference, item.CommercialTermsSnapshot.SourcePolicyReference);
                Assert.Equal(expected.CommercialTermsSnapshot.TermsCapturedAt, item.CommercialTermsSnapshot.TermsCapturedAt);
            }
        }

        [Fact]
        public async Task A_conditional_commercial_summary_round_trips_without_a_schema_change()
        {
            await using var harness = new OrderSliceHarness(
                _fixture,
                TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

            var source = MultiPassengerOrderFactory.AcceptedSource(harness.Clock);

            var conditional = source with
            {
                Products = source.Products
                    .Select(product => product with
                    {
                        CommercialTerms = product.CommercialTerms with
                        {
                            RefundabilitySummary = CommercialTermState.Conditional,
                            SourcePolicyVersion = "RULES-2026-09"
                        }
                    })
                    .ToList()
            };

            var order = Domain.OrderAggregate.Order.Create(
                MultiPassengerOrderFactory.Args(),
                conditional,
                MultiPassengerOrderFactory.OwnerAirlineId,
                harness.Ids,
                harness.Clock);

            await harness.Orders.AddAsync(order);
            await harness.UnitOfWork.SaveChangesAsync();

            await using var reload = _fixture.NewCommandContext();

            var reloaded = await new Persistence.OrderAggregate.OrderRepository(reload).GetAsync(order.Id);

            Assert.NotNull(reloaded);
            Assert.All(reloaded!.Items, item =>
            {
                Assert.Equal(CommercialTermState.Conditional, item.CommercialTermsSnapshot.RefundabilitySummary);
                Assert.Equal("RULES-2026-09", item.CommercialTermsSnapshot.SourcePolicyVersion);
            });
        }
    }
}
