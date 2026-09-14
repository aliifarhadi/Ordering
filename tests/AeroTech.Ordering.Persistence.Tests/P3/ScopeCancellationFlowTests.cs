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

        [Fact]
        public async Task DA_a_crash_after_the_scope_release_reached_the_provider_recovers_first_and_never_releases_twice()
        {
            var (order, item, scope, caller) = await ReservedItemAsync();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(
                             _fixture, caller, decorateReservationPort: port => new DispatchThenFailReservationPort(port)))
            {
                QuoteCredit(crashing, order, scope, OrderChangeType.Cancel);
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.ScopeCancel.CancelItemAsync(order.Id, item, QuoteId, key, order.CommercialVersion));

                Assert.NotEmpty(ReleaseKeys(crashing));
            }

            var crashed = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.True(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                QuoteCredit(resuming, order, scope, OrderChangeType.Cancel);
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await resuming.ScopeCancel.CancelItemAsync(
                    order.Id, item, QuoteId, key, order.CommercialVersion);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }

            var cancelled = await ReloadAsync(order.Id);

            Assert.Single(cancelled.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);

            await using (var replaying = new OrderSliceHarness(_fixture, caller))
            {
                var replay = await replaying.ScopeCancel.CancelItemAsync(
                    order.Id, item, QuoteId, key, order.CommercialVersion);

                Assert.True(replay.IsReplay);
                Assert.Empty(ReleaseKeys(replaying));
            }

            Assert.Equal(cancelled.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task DA2_a_transport_failure_after_the_scope_release_reached_the_provider_keeps_the_order_claimed()
        {
            var (order, item, scope, caller) = await ReservedItemAsync();
            var key = NewKey();

            await using (var failing = new OrderSliceHarness(
                             _fixture, caller, decorateReservationPort: port => new DispatchThenFailReservationPort(port)))
            {
                QuoteCredit(failing, order, scope, OrderChangeType.Cancel);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => failing.ScopeCancel.CancelItemAsync(order.Id, item, QuoteId, key, order.CommercialVersion));
            }

            var held = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.Status);
            Assert.True(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));

            await using (var competing = NewHarness())
            {
                var refusal = await Assert.ThrowsAsync<BusinessException>(
                    () => competing.Cancel.CancelAsync(order.Id, VoidReason.AgentError, 7, NewKey(), null));

                Assert.Equal(20070, refusal.Code);
            }

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                QuoteCredit(resuming, order, scope, OrderChangeType.Cancel);
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await resuming.ScopeCancel.CancelItemAsync(
                    order.Id, item, QuoteId, key, order.CommercialVersion);

                Assert.Equal(held.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
            }
        }

        [Fact]
        public async Task DB_an_unknown_scope_release_whose_local_save_failed_is_recovered_before_any_second_release()
        {
            var (order, item, scope, caller) = await ReservedItemAsync();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(_fixture, caller))
            {
                QuoteCredit(crashing, order, scope, OrderChangeType.Cancel);
                crashing.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.ScopeCancel.CancelItemAsync(order.Id, item, QuoteId, key, order.CommercialVersion));
            }

            var crashed = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                QuoteCredit(resuming, order, scope, OrderChangeType.Cancel);
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await resuming.ScopeCancel.CancelItemAsync(
                    order.Id, item, QuoteId, key, order.CommercialVersion);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }
        }

        [Fact]
        public async Task DE_a_failed_execution_boundary_never_reaches_the_scope_release_provider()
        {
            var (order, item, scope, caller) = await ReservedItemAsync();
            var key = NewKey();
            RefusingExecutionBoundaryOperationStore? boundary = null;

            await using (var refusing = new OrderSliceHarness(
                             _fixture, caller,
                             decorateOperationStore: store => boundary = new RefusingExecutionBoundaryOperationStore(store)))
            {
                QuoteCredit(refusing, order, scope, OrderChangeType.Cancel);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => refusing.ScopeCancel.CancelItemAsync(order.Id, item, QuoteId, key, order.CommercialVersion));

                Assert.Equal(0, refusing.CancellationQuotes.CallCount);
                Assert.Empty(ReleaseKeys(refusing));
            }

            var refused = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(1, boundary!.Refusals);
            Assert.Equal(ServicingOperationStatus.Prepared, refused.Status);

            await using (var retrying = new OrderSliceHarness(_fixture, caller))
            {
                QuoteCredit(retrying, order, scope, OrderChangeType.Cancel);

                var retried = await retrying.ScopeCancel.CancelItemAsync(
                    order.Id, item, QuoteId, key, order.CommercialVersion);

                Assert.Equal(refused.Id, retried.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, retried.OperationStatus);
                Assert.NotEmpty(ReleaseKeys(retrying));
            }
        }

        [Fact]
        public async Task A_rejected_scope_release_replay_releases_the_replay_claim()
        {
            var (order, item, scope, caller) = await ReservedItemAsync();
            var key = NewKey();

            await using var harness = new OrderSliceHarness(_fixture, caller);

            QuoteCredit(harness, order, scope, OrderChangeType.Cancel);
            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            await harness.ScopeCancel.CancelItemAsync(order.Id, item, QuoteId, key, order.CommercialVersion);

            var releases = ReleaseKeys(harness);

            var replay = await harness.ScopeCancel.CancelItemAsync(
                order.Id, item, QuoteId, key, order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);
            Assert.Equal(releases, ReleaseKeys(harness));
            Assert.False(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));
        }

        [Fact]
        public async Task DG_a_stale_snapshot_replay_of_a_completed_scope_cancellation_never_releases_twice()
        {
            var (order, item, scope, caller) = await ReservedItemAsync();
            var key = NewKey();
            IReadOnlyList<string> releases = [];
            Order? cancelled = null;
            CompletingElsewhereOrderRepository? interleaving = null;

            await using var stale = new OrderSliceHarness(
                _fixture, caller,
                decorateOrders: orders => interleaving = new CompletingElsewhereOrderRepository(orders, async () =>
                {
                    await using var completing = new OrderSliceHarness(_fixture, caller);

                    QuoteCredit(completing, order, scope, OrderChangeType.Cancel);

                    var completed = await completing.ScopeCancel.CancelItemAsync(
                        order.Id, item, QuoteId, key, order.CommercialVersion);

                    Assert.Equal(ServicingOperationStatus.Completed, completed.OperationStatus);

                    releases = ReleaseKeys(completing);
                    cancelled = await ReloadAsync(order.Id);
                }));

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => stale.ScopeCancel.CancelItemAsync(order.Id, item, QuoteId, key, order.CommercialVersion));

            Assert.Equal(1, interleaving!.Completions);
            Assert.NotEmpty(releases);
            Assert.Equal(20334, refusal.Code);
            Assert.Empty(ReleaseKeys(stale));
            Assert.Empty(stale.Reservation.ObservedRecoveryKeys);
            Assert.Equal(0, stale.CancellationQuotes.CallCount);

            var operation = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);
            var after = await ReloadAsync(order.Id);

            Assert.Equal(ServicingOperationStatus.Completed, operation.Status);
            Assert.Equal(cancelled!.CommercialVersion, after.CommercialVersion);
            Assert.Single(after.Changes, change => change.OperationId == operation.Id);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.False(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));

            await using (var retrying = new OrderSliceHarness(_fixture, caller))
            {
                var replay = await retrying.ScopeCancel.CancelItemAsync(
                    order.Id, item, QuoteId, key, order.CommercialVersion);

                Assert.True(replay.IsReplay);
                Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
                Assert.Empty(ReleaseKeys(retrying));
            }
        }

        private async Task<(Order Order, long ItemId, IReadOnlyList<long> Scope, Domain._Shared.Contracts.ICallerContext Caller)>
            ReservedItemAsync()
        {
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");

            await using var setup = new OrderSliceHarness(_fixture, caller);

            var order = await ReservedOrderAsync(setup);
            var item = order.Items.First();

            return (order, item.Id, order.ServiceIdsOfItem(item.Id).ToList(), caller);
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
