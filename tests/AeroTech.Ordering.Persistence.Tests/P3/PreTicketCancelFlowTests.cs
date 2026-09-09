using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PreTicketCancelFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public PreTicketCancelFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_reserved_order_is_cancelled_through_the_durable_operation_rail()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(OrderStatus.Cancelled, outcome.Status);
            Assert.Equal(2, outcome.CommercialVersion);
            Assert.Equal(2, outcome.FinancialSequence);
            Assert.False(outcome.IsReplay);
            Assert.NotEmpty(outcome.CancelledServiceIds);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Cancelled, reloaded.Status);
            Assert.Equal(0m, reloaded.CustomerTotal);
            Assert.All(reloaded.OrderServices, service => Assert.Equal(OrderServiceStatus.Cancelled, service.Status));

            await using var command = _fixture.NewCommandContext();

            Assert.Equal(1, await command.ServicingOperations.CountAsync(row =>
                row.OrderId == order.Id && row.Kind == ServicingOperationKind.Cancel));
            Assert.Equal(1, await command.CommandReceipts.CountAsync(row =>
                row.OperationId == outcome.OperationId
                && row.OperationName == ServicingOperationKind.Cancel.ToString()));
        }

        [Fact]
        public async Task The_persisted_cancellation_change_set_is_ordering_derived()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            await harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            var reloaded = await ReloadAsync(order.Id);
            var cancellation = reloaded.PriceChangeSets.Single(set => set.Reason == PriceChangeReason.Cancellation);

            Assert.Equal(PricingSource.OrderingDerived, cancellation.Source);
            Assert.Equal(OrderChangeType.Cancel, reloaded.Changes.Single(change => change.Id == cancellation.ChangeId).ChangeType);

            var reversals = reloaded.PricingLines.Where(line => line.PriceChangeSetId == cancellation.Id).ToList();

            Assert.NotEmpty(reversals);
            Assert.All(reversals, line => Assert.Equal(PricingLineRole.Reversal, line.LineRole));
            Assert.All(reversals, line => Assert.True(line.SaleAmount >= 0m));
            Assert.All(reversals, line => Assert.NotNull(line.OriginalPricingLineId));
        }

        [Fact]
        public async Task A_replay_creates_no_second_change_set_and_no_second_provider_call()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseCallsAfterFirst = harness.Reservation.ObservedOperationKeys.Count;

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(first.CommercialVersion, replay.CommercialVersion);
            Assert.Equal(releaseCallsAfterFirst, harness.Reservation.ObservedOperationKeys.Count);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.Single(reloaded.Changes, change => change.ChangeType == OrderChangeType.Cancel);
            Assert.Equal(2, reloaded.CommercialVersion);
        }

        [Fact]
        public async Task A_stale_expected_version_is_rejected_and_changes_nothing()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, NewKey(), 99));

            Assert.Empty(harness.Reservation.ObservedOperationKeys.Where(key => key.StartsWith("release", StringComparison.Ordinal)));

            var reloaded = await ReloadAsync(order.Id);

            Assert.NotEqual(OrderStatus.Cancelled, reloaded.Status);
            Assert.Equal(1, reloaded.CommercialVersion);
            Assert.DoesNotContain(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
        }

        [Fact]
        public async Task A_ticketed_order_is_refused_without_commercial_or_pricing_mutation()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await ReservedOrderAsync(harness);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var ticketed = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Ticketed, ticketed.Status);

            var versionBefore = ticketed.CommercialVersion;

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, NewKey(), versionBefore));

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Ticketed, reloaded.Status);
            Assert.Equal(versionBefore, reloaded.CommercialVersion);
            Assert.DoesNotContain(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.DoesNotContain(reloaded.Changes, change => change.ChangeType == OrderChangeType.Cancel);
        }

        [Fact]
        public async Task An_issued_misc_document_blocks_cancel_even_when_the_order_status_allows_it()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            Assert.Equal(OrderStatus.Confirmed, order.Status);

            var documentedService = order.OrderServices.First();

            await harness.MiscDocumentRepository.AddAsync(ElectronicMiscDocument.Issue(
                harness.Ids.NewId(),
                order.Id,
                documentedService.SoleBeneficiaryId,
                harness.Ids.NewId(),
                $"M{harness.Ids.NewId()}",
                ElectronicMiscDocumentType.Standalone,
                "C",
                order.OwnerAirlineId,
                null,
                DocumentAuthority.Local,
                order.CurrencyId,
                [
                    new EmdCouponIssuance(
                        EmdCouponPurpose.Service,
                        "0DF",
                        0m,
                        [],
                        OrderServiceId: documentedService.Id)
                ],
                harness.Ids,
                harness.Clock));

            await harness.UnitOfWork.SaveChangesAsync();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion));

            Assert.Contains("ALREADY_ISSUED", error.Message, StringComparison.Ordinal);

            var reloaded = await ReloadAsync(order.Id);

            Assert.NotEqual(OrderStatus.Cancelled, reloaded.Status);
            Assert.DoesNotContain(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
        }

        [Fact]
        public async Task A_rejected_release_leaves_no_committed_cancellation()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ProviderOperationOutcome.Rejected, outcome.ReservationReleaseOutcome);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Cancelled, reloaded.Status);
            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
        }

        [Fact]
        public async Task An_unknown_release_leaves_the_operation_unresolved_for_reconciliation()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ProviderOperationOutcome.Unknown, outcome.ReservationReleaseOutcome);

            await using var command = _fixture.NewCommandContext();

            var claim = await command.OperationOrderClaims
                .AsNoTracking()
                .SingleAsync(row => row.OrderId == order.Id && row.OperationId == outcome.OperationId);

            Assert.Null(claim.ResolvedAt);
        }

        [Fact]
        public async Task An_unknown_release_does_not_trigger_a_second_external_cancellation()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseKeys = harness.Reservation.ObservedOperationKeys
                .Where(observed => observed.StartsWith("release", StringComparison.Ordinal))
                .ToList();

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(
                releaseKeys,
                harness.Reservation.ObservedOperationKeys
                    .Where(observed => observed.StartsWith("release", StringComparison.Ordinal))
                    .ToList());
            Assert.Single(releaseKeys.Distinct());
        }

        [Fact]
        public async Task The_order_view_reflects_the_cancellation()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            await harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            await using var query = _fixture.NewQueryContext();

            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == order.Id);
            var view = System.Text.Json.JsonSerializer.Deserialize<Query.OrderAggregate.View.OrderView>(
                details.SnapshotJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.Equal(OrderStatus.Cancelled, view.Status);
            Assert.Equal(2, view.CommercialVersion);
            Assert.Contains(view.PricingHistory, set => set.Reason == PriceChangeReason.Cancellation);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static async Task<Order> ReservedOrderAsync(OrderSliceHarness harness)
        {
            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            return order;
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }
    }
}
