using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.FulfillmentReservationAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ScopeCancellationFlowTests
    {
        private const string QuoteId = "QCXL-1";

        private readonly OrderingDatabaseFixture _fixture;

        public ScopeCancellationFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_whole_order_item_is_cancelled_before_ticketing()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);

            var outcome = await harness.ScopeCancel.CancelItemAsync(
                order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion);

            Assert.Equal(OrderChangeType.Cancel, outcome.Intent);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, outcome.CommercialVersion);
            Assert.NotNull(outcome.PriceChangeSetId);

            var reloaded = await ReloadAsync(order.Id);

            Assert.All(
                reloaded.OrderServices.Where(service => scope.Contains(service.Id)),
                service => Assert.Equal(OrderServiceStatus.Cancelled, service.Status));

            Assert.Equal(
                OrderItemCommercialStatus.Cancelled,
                reloaded.Items.Single(candidate => candidate.Id == item.Id).CommercialStatus);

            Assert.Equal(OrderChangeType.Cancel, reloaded.Changes.Single(c => c.Id == outcome.OrderChangeId).ChangeType);
        }

        [Fact]
        public async Task Cancelling_one_item_leaves_the_other_item_and_its_services_active()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            Assert.True(order.Items.Count > 1);

            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();
            var untouched = order.OrderServices.Where(s => !scope.Contains(s.Id)).Select(s => s.Id).ToList();

            Assert.NotEmpty(untouched);

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);

            await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion);

            var reloaded = await ReloadAsync(order.Id);

            Assert.All(
                reloaded.OrderServices.Where(service => untouched.Contains(service.Id)),
                service => Assert.NotEqual(OrderServiceStatus.Cancelled, service.Status));

            Assert.Contains(
                reloaded.Items,
                candidate => candidate.Id != item.Id && candidate.CommercialStatus == OrderItemCommercialStatus.Active);

            Assert.NotEqual(CommercialSummary.Cancelled, reloaded.CommercialSummary);

            await using var command = _fixture.NewCommandContext();

            var reservation = (await new FulfillmentReservationRepository(command).ListByOrderAsync(order.Id)).Single();

            Assert.Equal(FulfillmentReservationStatus.Mixed, reservation.Status);
            Assert.Equal(untouched.OrderBy(id => id), reservation.OutstandingServiceIds().OrderBy(id => id));
        }

        [Fact]
        public async Task Removing_a_service_keeps_sibling_services_and_records_remove_service_intent()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var removed = order.OrderServices.First();
            var siblings = order.OrderServices.Where(s => s.Id != removed.Id).Select(s => s.Id).ToList();

            Assert.NotEmpty(siblings);

            QuoteCredit(harness, order, [removed.Id], OrderChangeType.RemoveService);

            var outcome = await harness.ScopeCancel.RemoveServicesAsync(
                order.Id, [removed.Id], QuoteId, NewKey(), order.CommercialVersion);

            Assert.Equal(OrderChangeType.RemoveService, outcome.Intent);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(
                OrderChangeType.RemoveService,
                reloaded.Changes.Single(c => c.Id == outcome.OrderChangeId).ChangeType);

            Assert.All(
                reloaded.OrderServices.Where(service => siblings.Contains(service.Id)),
                service => Assert.NotEqual(OrderServiceStatus.Cancelled, service.Status));

            Assert.Contains(
                reloaded.Items,
                candidate => candidate.CommercialStatus == OrderItemCommercialStatus.Active);

            Assert.NotEqual(CommercialSummary.Cancelled, reloaded.CommercialSummary);
        }

        [Fact]
        public async Task Removing_the_last_service_of_an_item_rolls_it_up_but_keeps_remove_service_intent()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            QuoteCredit(harness, order, scope, OrderChangeType.RemoveService);

            var outcome = await harness.ScopeCancel.RemoveServicesAsync(
                order.Id, scope, QuoteId, NewKey(), order.CommercialVersion);

            Assert.Equal(OrderChangeType.RemoveService, outcome.Intent);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(
                OrderItemCommercialStatus.Cancelled,
                reloaded.Items.Single(candidate => candidate.Id == item.Id).CommercialStatus);

            Assert.Equal(
                OrderChangeType.RemoveService,
                reloaded.Changes.Single(c => c.Id == outcome.OrderChangeId).ChangeType);

            Assert.DoesNotContain(reloaded.Changes, c => c.ChangeType == OrderChangeType.Cancel && c.Id == outcome.OrderChangeId);
        }

        [Fact]
        public async Task A_documented_scope_is_refused_without_any_mutation()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await ReservedOrderAsync(harness);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var ticketed = await ReloadAsync(order.Id);
            var item = ticketed.Items.First();
            var scope = ticketed.ServiceIdsOfItem(item.Id).ToList();

            QuoteCredit(harness, ticketed, scope, OrderChangeType.Cancel);

            var before = await SnapshotAsync(order.Id);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(
                    order.Id, item.Id, QuoteId, NewKey(), ticketed.CommercialVersion));

            Assert.Contains("ALREADY_ISSUED", error.Message, StringComparison.Ordinal);
            Assert.Equal(0, harness.CancellationQuotes.CallCount);
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task A_stale_expected_version_is_rejected_before_any_provider_call()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();

            QuoteCredit(harness, order, order.ServiceIdsOfItem(item.Id).ToList(), OrderChangeType.Cancel);

            var before = await SnapshotAsync(order.Id);

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, NewKey(), 99));

            Assert.Equal(0, harness.CancellationQuotes.CallCount);
            Assert.Empty(ReleaseKeys(harness));
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task A_replay_is_idempotent()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();
            var key = NewKey();
            var expected = order.CommercialVersion;

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);

            var first = await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, key, expected);
            var releaseCalls = ReleaseKeys(harness);
            var quoteCalls = harness.CancellationQuotes.CallCount;

            var replay = await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, key, expected);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
            Assert.Equal(quoteCalls, harness.CancellationQuotes.CallCount);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Single(reloaded.Changes, c => c.OperationId == first.OperationId);
            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.Equal(2, reloaded.CommercialVersion);
        }

        [Fact]
        public async Task A_rejected_release_leaves_the_commercial_state_untouched()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();

            QuoteCredit(harness, order, order.ServiceIdsOfItem(item.Id).ToList(), OrderChangeType.Cancel);

            var before = await SnapshotAsync(order.Id);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.ScopeCancel.CancelItemAsync(
                order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Equal(1, harness.CancellationQuotes.CallCount);
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task An_unknown_release_stays_reconcilable_and_recovers_exactly_once()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();
            var key = NewKey();

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, key, order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(1, harness.CancellationQuotes.CallCount);

            var releaseCalls = ReleaseKeys(harness);

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, key, order.CommercialVersion);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal(releaseCalls, ReleaseKeys(harness));

            var reloaded = await ReloadAsync(order.Id);

            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.Equal(2, reloaded.CommercialVersion);
        }

        [Fact]
        public async Task The_monetary_outcome_comes_from_the_pricing_authority_not_from_allocations()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            var allocationDerived = order.PricingLines
                .Where(line => line.AffectsCustomerBalance)
                .SelectMany(line => line.CommercialAllocations())
                .Where(allocation => allocation.OrderServiceId is { } id && scope.Contains(id))
                .Sum(allocation => allocation.SaleAmount);

            const decimal authorized = 12_345m;

            Assert.NotEqual(authorized, allocationDerived);

            harness.CancellationQuotes.Quote(
                Bound(order, OrderChangeType.Cancel, scope, [Credit(authorized, order.CurrencyId)]));

            var outcome = await harness.ScopeCancel.CancelItemAsync(
                order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion);

            var reloaded = await ReloadAsync(order.Id);
            var set = reloaded.PriceChangeSets.Single(candidate => candidate.Id == outcome.PriceChangeSetId);
            var lines = reloaded.PricingLines.Where(line => line.PriceChangeSetId == set.Id).ToList();

            Assert.Equal(PricingSource.PricingEngine, set.Source);
            Assert.NotEqual(PricingSource.OrderingDerived, set.Source);
            Assert.Equal(authorized, Assert.Single(lines).SaleAmount);
        }

        [Fact]
        public async Task A_penalty_supplied_by_the_pricing_authority_is_recorded_as_given()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.Cancel,
                scope,
                [
                    Credit(100_000m, order.CurrencyId),
                    new AcceptedCancellationPricingLine(
                        PricingComponentType.Penalty,
                        PricingEffect.CustomerBalance,
                        OrderPricingLineDirection.Debit,
                        PricingLineRole.Original,
                        25_000m,
                        order.CurrencyId,
                        25_000m,
                        order.CurrencyId,
                        PricingBasisType.Order,
                        RefundabilityRule.NonRefundable,
                        Code: "CXLFEE")
                ]));

            var outcome = await harness.ScopeCancel.CancelItemAsync(
                order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion);

            var reloaded = await ReloadAsync(order.Id);
            var lines = reloaded.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();

            var penalty = Assert.Single(lines, line => line.ComponentType == PricingComponentType.Penalty);

            Assert.Equal(25_000m, penalty.SaleAmount);
            Assert.Equal(OrderPricingLineDirection.Debit, penalty.Direction);
            Assert.Equal(PricingLineRole.Original, penalty.LineRole);
        }

        [Fact]
        public async Task A_dependent_surviving_service_blocks_removal_of_what_it_covers()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var air = order.OrderServices.First();

            var covering = order.OrderServices.FirstOrDefault(service =>
                service.CoveredServices.Any(coverage => coverage.CoveredOrderServiceId == air.Id));

            if (covering is null)
                return;

            QuoteCredit(harness, order, [air.Id], OrderChangeType.RemoveService);

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.RemoveServicesAsync(
                    order.Id, [air.Id], QuoteId, NewKey(), order.CommercialVersion));
        }

        [Fact]
        public async Task Removing_every_service_is_refused_as_a_service_removal()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var all = order.OrderServices.Select(service => service.Id).ToList();

            QuoteCredit(harness, order, all, OrderChangeType.RemoveService);

            var before = await SnapshotAsync(order.Id);

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.RemoveServicesAsync(
                    order.Id, all, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task An_accepted_quote_covering_a_different_scope_is_rejected_before_inventory_release()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();
            var extra = order.OrderServices.First(service => !scope.Contains(service.Id)).Id;

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.Cancel,
                [.. scope, extra],
                [Credit(1_000m, order.CurrencyId)]));

            var before = await SnapshotAsync(order.Id);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(
                    order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(20203, error.Code);
            Assert.Empty(ReleaseKeys(harness));
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task An_accepted_quote_missing_a_requested_service_is_rejected_before_inventory_release()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var scope = order.OrderServices.Take(2).Select(service => service.Id).ToList();

            Assert.Equal(2, scope.Count);

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.RemoveService,
                [scope[0]],
                [Credit(1_000m, order.CurrencyId)]));

            var before = await SnapshotAsync(order.Id);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.RemoveServicesAsync(
                    order.Id, scope, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(20203, error.Code);
            Assert.Empty(ReleaseKeys(harness));
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task An_accepted_quote_for_another_operation_or_order_is_rejected_before_inventory_release()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.RemoveService,
                scope,
                [Credit(1_000m, order.CurrencyId)]));

            var wrongIntent = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(
                    order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(20202, wrongIntent.Code);

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.Cancel,
                scope,
                [Credit(1_000m, order.CurrencyId)],
                orderId: order.Id + 1));

            var wrongOrder = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(
                    order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(20202, wrongOrder.Code);

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.Cancel,
                scope,
                [Credit(1_000m, order.CurrencyId)],
                expectedCommercialVersion: order.CommercialVersion + 5));

            var wrongVersion = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(
                    order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(20202, wrongVersion.Code);

            harness.CancellationQuotes.Quote(Bound(
                order,
                OrderChangeType.Cancel,
                scope,
                [Credit(1_000m, order.CurrencyId)],
                currencyId: order.CurrencyId + 7));

            var wrongCurrency = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.CancelItemAsync(
                    order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(20202, wrongCurrency.Code);
            Assert.Empty(ReleaseKeys(harness));
        }

        [Fact]
        public async Task A_partial_release_is_satisfied_and_never_touches_unaffected_members()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();
            var untouched = order.OrderServices.Where(s => !scope.Contains(s.Id)).Select(s => s.Id).ToList();
            var key = NewKey();
            var expected = order.CommercialVersion;

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);

            var outcome = await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, key, expected);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            await using (var command = _fixture.NewCommandContext())
            {
                var reservation = (await new FulfillmentReservationRepository(command).ListByOrderAsync(order.Id)).Single();

                Assert.Equal(FulfillmentReservationStatus.Mixed, reservation.Status);
                Assert.Equal(untouched.OrderBy(id => id), reservation.OutstandingServiceIds().OrderBy(id => id));
            }

            var releaseCalls = ReleaseKeys(harness);
            var recoveryCalls = harness.Reservation.ObservedRecoveryKeys.Count;

            var replay = await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, key, expected);

            Assert.True(replay.IsReplay);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
            Assert.Equal(recoveryCalls, harness.Reservation.ObservedRecoveryKeys.Count);

            await using var verify = _fixture.NewCommandContext();

            var after = (await new FulfillmentReservationRepository(verify).ListByOrderAsync(order.Id)).Single();

            Assert.Equal(FulfillmentReservationStatus.Mixed, after.Status);
            Assert.Equal(untouched.OrderBy(id => id), after.OutstandingServiceIds().OrderBy(id => id));
        }

        [Fact]
        public async Task A_seat_that_depends_on_an_air_service_blocks_its_removal_before_inventory_release()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var air = order.OrderServices.First();

            order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)),
                harness.Ids,
                harness.Clock);

            await harness.UnitOfWork.SaveChangesAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Contains(
                reloaded.OrderServices,
                service => service.SeatDetail is { } seat && seat.AssociatedAirOrderServiceId == air.Id);

            QuoteCredit(harness, reloaded, [air.Id], OrderChangeType.RemoveService);

            var before = await SnapshotAsync(order.Id);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.ScopeCancel.RemoveServicesAsync(
                    order.Id, [air.Id], QuoteId, NewKey(), reloaded.CommercialVersion));

            Assert.Equal(20189, error.Code);
            Assert.Equal(0, harness.CancellationQuotes.CallCount);
            Assert.Empty(ReleaseKeys(harness));
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task Removing_the_last_service_publishes_removal_semantics_not_cancellation()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            harness.Events.Dispatched.Clear();

            QuoteCredit(harness, order, scope, OrderChangeType.RemoveService);

            await harness.ScopeCancel.RemoveServicesAsync(
                order.Id, scope, QuoteId, NewKey(), order.CommercialVersion);

            var names = harness.Events.Dispatched.Select(e => e.GetType().Name).ToList();

            Assert.Contains("OrderServicesRemoved", names);
            Assert.DoesNotContain("OrderItemCancelled", names);
            Assert.DoesNotContain("OrderCancelled", names);

            var removed = harness.Events.Dispatched
                .OfType<Domain.OrderAggregate.DomainEvents.OrderServicesRemoved>()
                .Single();

            Assert.Equal(OrderChangeType.RemoveService, removed.Intent);
            Assert.Equal(scope.OrderBy(id => id), removed.RemovedServiceIds.OrderBy(id => id));
            Assert.Contains(item.Id, removed.RolledUpOrderItemIds);
        }

        [Fact]
        public async Task Cancelling_an_item_publishes_cancellation_semantics()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var item = order.Items.First();
            var scope = order.ServiceIdsOfItem(item.Id).ToList();

            harness.Events.Dispatched.Clear();

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);

            await harness.ScopeCancel.CancelItemAsync(order.Id, item.Id, QuoteId, NewKey(), order.CommercialVersion);

            var names = harness.Events.Dispatched.Select(e => e.GetType().Name).ToList();

            Assert.Contains("OrderItemCancelled", names);
            Assert.DoesNotContain("OrderServicesRemoved", names);
        }

        private static void QuoteCredit(
            OrderSliceHarness harness,
            Order order,
            IReadOnlyList<long> scope,
            OrderChangeType intent)
            => harness.CancellationQuotes.Quote(
                Bound(order, intent, scope, [Credit(1_000m, order.CurrencyId)]));

        private static AcceptedScopeCancellation Bound(
            Order order,
            OrderChangeType intent,
            IReadOnlyList<long> scope,
            IReadOnlyList<AcceptedCancellationPricingLine> lines,
            long? orderId = null,
            int? expectedCommercialVersion = null,
            int? currencyId = null)
            => new(
                "AirPrice",
                QuoteId,
                PricingSource.PricingEngine,
                orderId ?? order.Id,
                expectedCommercialVersion ?? order.CommercialVersion,
                intent,
                currencyId ?? order.CurrencyId,
                scope,
                lines);

        private static AcceptedCancellationPricingLine Credit(decimal amount, int currencyId)
            => new(
                PricingComponentType.Fare,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Credit,
                PricingLineRole.Adjustment,
                amount,
                currencyId,
                amount,
                currencyId,
                PricingBasisType.Order,
                RefundabilityRule.Refundable,
                Code: "CXLCREDIT");

        private static IReadOnlyList<string> ReleaseKeys(OrderSliceHarness harness)
            => harness.Reservation.ObservedOperationKeys
                .Where(key => key.StartsWith("release", StringComparison.Ordinal))
                .ToList();

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

        private async Task<ScopeSnapshot> SnapshotAsync(long orderId)
        {
            var order = await ReloadAsync(orderId);

            await using var query = _fixture.NewQueryContext();

            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == orderId);

            return new ScopeSnapshot(
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                order.PriceChangeSets.Count,
                order.PricingLines.Count,
                order.Changes.Count,
                order.OrderServices.Count(service => service.Status == OrderServiceStatus.Cancelled),
                order.Items.Count(item => item.CommercialStatus == OrderItemCommercialStatus.Cancelled),
                details.ProjectionRevision,
                details.SnapshotJson);
        }

        private sealed record ScopeSnapshot(
            int CommercialVersion,
            long FinancialSequence,
            long ObligationVersion,
            decimal CustomerTotal,
            int PriceChangeSets,
            int PricingLines,
            int Changes,
            int CancelledServices,
            int CancelledItems,
            long ProjectionRevision,
            string SnapshotJson);
    }
}
