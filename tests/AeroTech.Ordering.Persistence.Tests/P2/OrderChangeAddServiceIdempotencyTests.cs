using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OrderChangeAddServiceIdempotencyTests
    {
        private const string SecondOfferId = "QOFFER-2";
        private const string SecondOfferItemId = "QOFFERITEM-2";

        private readonly OrderingDatabaseFixture _fixture;

        public OrderChangeAddServiceIdempotencyTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_first_accepted_quote_commits_one_mutation()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            Assert.False(outcome.IsReplay);
            Assert.Equal(2, outcome.CommercialVersion);
            Assert.Equal(2, outcome.FinancialSequence);
            Assert.Single(outcome.ServiceIds);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Single(reloaded.Changes.Where(change => change.ChangeType == OrderChangeType.AddProduct));
            Assert.Single(reloaded.PriceChangeSets.Where(set => set.Reason == PriceChangeReason.AddProduct));
        }

        [Fact]
        public async Task An_exact_replay_returns_the_same_item_and_services()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var first = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);
            var replay = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OrderItemId, replay.OrderItemId);
            Assert.Equal(first.ServiceIds, replay.ServiceIds);
            Assert.Equal(first.OrderChangeId, replay.OrderChangeId);
            Assert.Equal(first.PriceChangeSetId, replay.PriceChangeSetId);
            Assert.Equal(first.OperationId, replay.OperationId);
        }

        [Fact]
        public async Task An_exact_replay_creates_no_second_price_change_set_and_advances_no_version()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var first = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);
            var replay = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            Assert.Equal(first.CommercialVersion, replay.CommercialVersion);
            Assert.Equal(first.FinancialSequence, replay.FinancialSequence);
            Assert.Equal(first.ObligationVersion, replay.ObligationVersion);
            Assert.Equal(first.CustomerTotal, replay.CustomerTotal);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Single(reloaded.PriceChangeSets.Where(set => set.Reason == PriceChangeReason.AddProduct));
            Assert.Equal(2, reloaded.CommercialVersion);
            Assert.Equal(2, reloaded.FinancialSequence);
            Assert.Single(reloaded.Items.Where(item => item.ProductType == ProductType.Seat));
        }

        [Fact]
        public async Task A_committed_replay_does_not_resolve_the_quote_provider_again()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            Assert.Equal(1, harness.Quotes.CallCount);

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            Assert.Equal(1, harness.Quotes.CallCount);
        }

        [Fact]
        public async Task The_same_key_with_a_different_quoted_offer_is_rejected()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));
            harness.Quotes.Quote(ProductAdditionFactory.Seat(order) with { QuotedOfferId = SecondOfferId });

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.OrderChange.AddServiceAsync(order.Id, Selection(SecondOfferId), key, 1));

            Assert.Equal(2703, exception.Code);
        }

        [Fact]
        public async Task The_same_key_with_a_different_selected_offer_item_is_rejected()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));
            harness.Quotes.Quote(ProductAdditionFactory.Seat(order) with { SelectedOfferItemId = SecondOfferItemId });

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.OrderChange.AddServiceAsync(
                    order.Id,
                    Selection(selectedOfferItemId: SecondOfferItemId),
                    key,
                    1));

            Assert.Equal(2703, exception.Code);
        }

        [Fact]
        public async Task The_same_key_with_a_different_expected_version_is_rejected_by_the_fingerprint()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var key = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 1);

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.OrderChange.AddServiceAsync(order.Id, Selection(), key, 2));

            Assert.Equal(2703, exception.Code);
        }

        [Fact]
        public async Task A_stale_expected_version_is_rejected_and_changes_nothing()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 7));

            Assert.Equal(2730, exception.Code);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(1, reloaded.CommercialVersion);
            Assert.DoesNotContain(reloaded.Changes, change => change.ChangeType == OrderChangeType.AddProduct);
        }

        [Fact]
        public async Task A_rejected_change_does_not_leave_the_order_blocked()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 7));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            Assert.False(outcome.IsReplay);
            Assert.Equal(2, outcome.CommercialVersion);
        }

        [Fact]
        public async Task A_missing_expected_version_is_rejected()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), null));

            Assert.Equal(2856, exception.Code);
        }

        [Fact]
        public async Task A_retry_after_a_local_commit_resolves_from_the_persisted_change()
        {
            long orderId;
            long operationId;
            long changeId;
            var key = NewKey();

            await using (var harness = NewHarness())
            {
                var order = await harness.CreateOrderAsync();
                orderId = order.Id;

                harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

                var first = await harness.OrderChange.AddServiceAsync(orderId, Selection(), key, 1);
                operationId = first.OperationId;
                changeId = first.OrderChangeId;
            }

            await using var retry = NewHarness();

            var replay = await retry.OrderChange.AddServiceAsync(orderId, Selection(), key, 1);

            Assert.True(replay.IsReplay);
            Assert.Equal(operationId, replay.OperationId);
            Assert.Equal(changeId, replay.OrderChangeId);
            Assert.Equal(0, retry.Quotes.CallCount);
        }

        [Fact]
        public async Task A_replay_after_a_later_unrelated_change_still_resolves_the_original_one()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var firstKey = NewKey();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var first = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), firstKey, 1);

            harness.Quotes.Quote(ProductAdditionFactory.GroundTransport(order) with { QuotedOfferId = SecondOfferId });

            var second = await harness.OrderChange.AddServiceAsync(order.Id, Selection(SecondOfferId), NewKey(), 2);

            Assert.Equal(3, second.CommercialVersion);

            var replay = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), firstKey, 1);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OrderChangeId, replay.OrderChangeId);
            Assert.Equal(first.OrderItemId, replay.OrderItemId);
            Assert.Equal(first.ServiceIds, replay.ServiceIds);
            Assert.Equal(first.FinancialSequence, replay.FinancialSequence);
            Assert.Equal(3, replay.CommercialVersion);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(2, reloaded.Changes.Count(change => change.ChangeType == OrderChangeType.AddProduct));
        }

        [Fact]
        public async Task Two_commercial_mutations_cannot_share_one_operation_id()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var first = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            await using var context = _fixture.NewCommandContext();

            var duplicate = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlRawAsync(
                $"""
                 INSERT INTO [Order].[OrderChanges]
                     ([Id],[OrderId],[ChangeType],[Reason],[Source],[ExternalReference],[ActorScope],[ActorId],[OperationId],[OccurredAt],[LastUpdateTime])
                 SELECT [Id] + 1, [OrderId], [ChangeType], [Reason], [Source], [ExternalReference], [ActorScope], [ActorId], [OperationId], [OccurredAt], SYSDATETIMEOFFSET()
                 FROM [Order].[OrderChanges] WHERE [Id] = {first.OrderChangeId}
                 """));

            Assert.Contains("IX_OrderChanges_OrderId_OperationId", duplicate.ToString());
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, "order-change-subject"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static IReadOnlyList<SelectedQuotedOffer> Selection(
            string quotedOfferId = ProductAdditionFactory.QuotedOfferId,
            string selectedOfferItemId = ProductAdditionFactory.SelectedOfferItemId)
            => [new SelectedQuotedOffer(quotedOfferId, [selectedOfferItemId])];

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var order = await new OrderRepository(context).GetAsync(orderId);

            Assert.NotNull(order);

            return order!;
        }
    }
}
