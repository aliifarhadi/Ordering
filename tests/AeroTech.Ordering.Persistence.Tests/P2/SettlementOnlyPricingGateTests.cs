using System.Text.Json;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Contract = AeroTech.Messages.Ordering.IntegrationEvents.V1;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class SettlementOnlyPricingGateTests
    {
        private static readonly string PricingChangedType = typeof(Contract.OrderPricingChanged).FullName!;

        private readonly OrderingDatabaseFixture _fixture;

        public SettlementOnlyPricingGateTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_committed_settlement_only_change_publishes_one_neutral_pricing_event()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var totalBefore = order.CustomerTotal;
            var obligationBefore = order.ObligationVersion;
            var accepted = ProductAdditionFactory.SettlementOnly(order);

            harness.Quotes.Quote(accepted);

            var outcome = await harness.OrderChange.AddServiceAsync(
                order.Id,
                [new SelectedQuotedOffer(accepted.QuotedOfferId, [accepted.SelectedOfferItemId])],
                Guid.NewGuid().ToString("N"),
                order.CommercialVersion);

            var messages = await PricingMessagesAsync(order.Id);
            var settlement = Assert.Single(messages, message => message.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(outcome.PriceChangeSetId, settlement.PriceChangeSetId);
            Assert.Equal(2, settlement.FinancialSequence);
            Assert.Equal(2, settlement.CommercialVersion);
            Assert.Equal(0m, settlement.DerivedCustomerBalanceImpact);
            Assert.Equal(totalBefore, settlement.CustomerTotalAfter);
            Assert.Equal(obligationBefore, settlement.ObligationVersion);
            Assert.All(settlement.PricingLines, line => Assert.Equal(PricingEffect.SettlementOnly, line.Effect));

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(totalBefore, reloaded.CustomerTotal);
            Assert.Equal(obligationBefore, reloaded.ObligationVersion);
            Assert.Equal(2, reloaded.CommercialVersion);
            Assert.Equal(2, reloaded.PriceChangeSets.Count);
        }

        [Fact]
        public async Task A_settlement_only_change_writes_exactly_one_outbox_row()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var accepted = ProductAdditionFactory.SettlementOnly(order);
            var key = Guid.NewGuid().ToString("N");

            harness.Quotes.Quote(accepted);

            var selection = new[] { new SelectedQuotedOffer(accepted.QuotedOfferId, [accepted.SelectedOfferItemId]) };

            await harness.OrderChange.AddServiceAsync(order.Id, selection, key, order.CommercialVersion);
            var replay = await harness.OrderChange.AddServiceAsync(order.Id, selection, key, 1);

            Assert.True(replay.IsReplay);

            var messages = await PricingMessagesAsync(order.Id);

            Assert.Equal(2, messages.Count);
            Assert.Equal(messages.Count, messages.Select(message => message.PriceChangeSetId).Distinct().Count());
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private async Task<IReadOnlyList<Contract.OrderPricingChanged>> PricingMessagesAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var rows = await context.OutboxMessages
                .AsNoTracking()
                .Where(message => message.MessageType.StartsWith(PricingChangedType))
                .OrderBy(message => message.Id)
                .ToListAsync();

            return rows
                .Select(row => JsonSerializer.Deserialize<Contract.OrderPricingChanged>(row.Payload)!)
                .Where(message => message.OrderId == orderId)
                .ToList();
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }
    }
}
