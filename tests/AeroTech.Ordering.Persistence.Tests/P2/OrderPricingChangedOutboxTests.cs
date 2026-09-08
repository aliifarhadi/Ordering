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
    public sealed class OrderPricingChangedOutboxTests
    {
        private static readonly string PricingChangedType = typeof(Contract.OrderPricingChanged).FullName!;

        private readonly OrderingDatabaseFixture _fixture;

        public OrderPricingChangedOutboxTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Creating_an_order_writes_one_pricing_change_message()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var messages = await PricingMessagesAsync(order.Id);
            var message = Assert.Single(messages);

            Assert.Equal(PriceChangeReason.OriginalSale, message.Reason);
            Assert.Equal(1, message.CommercialVersion);
            Assert.Equal(1, message.FinancialSequence);
            Assert.Equal(order.Id, message.OrderId);
            Assert.Equal(order.OwnerAirlineId, message.OwnerAirlineId);
            Assert.NotEmpty(message.PricingLines);
        }

        [Fact]
        public async Task Adding_a_service_writes_one_further_message_with_every_line()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var outcome = await AddAsync(
                harness,
                order,
                ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m));

            var messages = await PricingMessagesAsync(order.Id);

            Assert.Equal(2, messages.Count);

            var addition = messages.Single(message => message.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(outcome.PriceChangeSetId, addition.PriceChangeSetId);
            Assert.Equal(outcome.OrderChangeId, addition.OrderChangeId);
            Assert.Equal(outcome.OperationId, addition.OperationId);
            Assert.Equal(2, addition.CommercialVersion);
            Assert.Equal(2, addition.FinancialSequence);
            Assert.Equal(3, addition.PricingLines.Count);
            Assert.Equal(220_000m, addition.DerivedCustomerBalanceImpact);
            Assert.Equal(outcome.CustomerTotal, addition.CustomerTotalAfter);
        }

        [Fact]
        public async Task The_message_keeps_modern_polarity_and_settlement_semantics()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            await AddAsync(harness, order, ProductAdditionFactory.SeatWithTaxAndCommission(order));

            var addition = (await PricingMessagesAsync(order.Id))
                .Single(message => message.Reason == PriceChangeReason.AddProduct);

            Assert.All(addition.PricingLines, line => Assert.True(line.SaleAmount >= 0m));

            var commission = addition.PricingLines.Single(line => line.ComponentType == PricingComponentType.Commission);
            var charge = addition.PricingLines.Single(line => line.ComponentType == PricingComponentType.ProductCharge);

            Assert.Equal(PricingEffect.SettlementOnly, commission.Effect);
            Assert.Equal(OrderPricingLineDirection.Debit, charge.Direction);
            Assert.Equal(PricingEffect.CustomerBalance, charge.Effect);
            Assert.Equal(PricingBasisType.OrderService, charge.BasisType);
        }

        [Fact]
        public async Task The_message_envelope_is_stamped_and_carries_the_domain_event_identity()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var message = Assert.Single(await PricingMessagesAsync(order.Id));

            Assert.False(string.IsNullOrWhiteSpace(message.EventId));
            Assert.Equal(order.Id.ToString(), message.AggregateId);
            Assert.Equal("Ordering", message.SourceSystem);
            Assert.Equal(1, message.TenantId);
        }

        [Fact]
        public async Task A_replayed_change_writes_no_second_message()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var accepted = ProductAdditionFactory.Seat(order);
            var key = NewKey();

            harness.Quotes.Quote(accepted);

            var first = await harness.OrderChange.AddServiceAsync(order.Id, Selection(accepted), key, 1);
            var replay = await harness.OrderChange.AddServiceAsync(order.Id, Selection(accepted), key, 1);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.PriceChangeSetId, replay.PriceChangeSetId);

            var messages = await PricingMessagesAsync(order.Id);

            Assert.Equal(2, messages.Count);
            Assert.Single(messages.Where(message => message.Reason == PriceChangeReason.AddProduct));
            Assert.Equal(
                messages.Select(message => message.PriceChangeSetId).Distinct().Count(),
                messages.Count);
        }

        [Fact]
        public async Task A_rejected_change_writes_no_pricing_message()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var before = (await PricingMessagesAsync(order.Id)).Count;

            await Assert.ThrowsAsync<Framework.Core.Domain.Exceptions.BusinessException>(
                () => harness.OrderChange.AddServiceAsync(
                    order.Id,
                    Selection(ProductAdditionFactory.Seat(order)),
                    NewKey(),
                    7));

            Assert.Equal(before, (await PricingMessagesAsync(order.Id)).Count);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(1, reloaded.CommercialVersion);
        }

        [Fact]
        public async Task A_failed_quote_leaves_no_orphan_pricing_message()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var before = (await PricingMessagesAsync(order.Id)).Count;

            await Assert.ThrowsAsync<Framework.Core.Domain.Exceptions.BusinessException>(
                () => harness.OrderChange.AddServiceAsync(
                    order.Id,
                    [new SelectedQuotedOffer("QOFFER-MISSING", ["QOFFERITEM-MISSING"])],
                    NewKey(),
                    1));

            Assert.Equal(before, (await PricingMessagesAsync(order.Id)).Count);
        }

        [Fact]
        public async Task Reservation_and_issuance_write_no_pricing_message()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            var afterCreate = (await PricingMessagesAsync(order.Id)).Count;

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            Assert.Equal(afterCreate, (await PricingMessagesAsync(order.Id)).Count);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(afterCreate, (await PricingMessagesAsync(order.Id)).Count);

            var ticketed = await ReloadAsync(order.Id);

            await AddAsync(harness, ticketed, ProductAdditionFactory.EmdBaggage(ticketed));

            var afterAddition = (await PricingMessagesAsync(order.Id)).Count;

            Assert.Equal(afterCreate + 1, afterAddition);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(afterAddition, (await PricingMessagesAsync(order.Id)).Count);
        }

        [Fact]
        public async Task A_projection_refresh_writes_no_pricing_message()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var before = (await PricingMessagesAsync(order.Id)).Count;

            await harness.Projector.ProjectAsync(order.Id);
            await harness.UnitOfWork.SaveChangesAsync();

            Assert.Equal(before, (await PricingMessagesAsync(order.Id)).Count);
        }

        [Fact]
        public async Task The_existing_created_contract_still_publishes_alongside_the_new_one()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var raised = harness.Events.Dispatched.Select(domainEvent => domainEvent.GetType().Name).ToList();

            Assert.Contains("OrderCreated", raised);
            Assert.Contains("OrderPricingChanged", raised);
            Assert.NotNull(order);
        }

        [Fact]
        public void The_legacy_issued_contract_and_its_polarity_adapter_are_untouched()
        {
            var legacyLine = typeof(Contract.OrderIssuedPricingLine)
                .GetProperties()
                .Single(property => property.Name == "Category");

            Assert.Equal(typeof(OrderPricingLineCategory), legacyLine.PropertyType);

            var translation = typeof(Application.OrderAggregate.Services.OrderChange.OrderChangeService).Assembly
                .GetType("AeroTech.Ordering.Application.OrderAggregate.EventHandlers.LegacyPricingLineTranslation");

            Assert.NotNull(translation);

            var modernLine = typeof(Contract.OrderPricingChangedLine).GetProperties().Select(property => property.Name).ToList();

            Assert.DoesNotContain("Category", modernLine);
            Assert.Contains("Direction", modernLine);
            Assert.Contains("Effect", modernLine);
            Assert.Contains("LineRole", modernLine);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static IReadOnlyList<SelectedQuotedOffer> Selection(
            Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedAddServiceChange accepted)
            => [new SelectedQuotedOffer(accepted.QuotedOfferId, [accepted.SelectedOfferItemId])];

        private static async Task<OrderChangeOutcome> AddAsync(
            OrderSliceHarness harness,
            Order order,
            Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedAddServiceChange accepted)
        {
            harness.Quotes.Quote(accepted);

            return await harness.OrderChange.AddServiceAsync(
                order.Id,
                Selection(accepted),
                NewKey(),
                order.CommercialVersion);
        }

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

            var order = await new OrderRepository(context).GetAsync(orderId);

            Assert.NotNull(order);

            return order!;
        }
    }
}
