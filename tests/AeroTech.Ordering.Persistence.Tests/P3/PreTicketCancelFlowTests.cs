using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Persistence.FulfillmentReservationAggregate;
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
        public async Task A_rejected_release_finalizes_nothing()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var before = await SnapshotAsync(order.Id);

            harness.Events.Dispatched.Clear();
            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ProviderOperationOutcome.Rejected, outcome.ReservationReleaseOutcome);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);

            Assert.Equal(before, await SnapshotAsync(order.Id));
            Assert.Empty(harness.Events.Dispatched.Where(IsCancellationEvent));

            await using var command = _fixture.NewCommandContext();

            Assert.Equal(ServicingOperationStatus.Rejected, (await command.ServicingOperations
                .AsNoTracking()
                .SingleAsync(row => row.Id == outcome.OperationId)).Status);
        }

        [Fact]
        public async Task A_rejected_replay_returns_the_rejection_without_calling_the_provider_again()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseCalls = ReleaseKeys(harness);
            var before = await SnapshotAsync(order.Id);

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ProviderOperationOutcome.Rejected, replay.ReservationReleaseOutcome);
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
            Assert.Empty(harness.Reservation.ObservedRecoveryKeys);
            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task An_unknown_release_finalizes_nothing_and_stays_reconcilable()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var before = await SnapshotAsync(order.Id);

            harness.Events.Dispatched.Clear();
            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ProviderOperationOutcome.Unknown, outcome.ReservationReleaseOutcome);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);

            Assert.Equal(before, await SnapshotAsync(order.Id));
            Assert.Empty(harness.Events.Dispatched.Where(IsCancellationEvent));

            await using var command = _fixture.NewCommandContext();

            var claim = await command.OperationOrderClaims
                .AsNoTracking()
                .SingleAsync(row => row.OrderId == order.Id && row.OperationId == outcome.OperationId);

            Assert.Null(claim.ResolvedAt);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, (await command.ServicingOperations
                .AsNoTracking()
                .SingleAsync(row => row.Id == outcome.OperationId)).Status);
        }

        [Fact]
        public async Task An_unknown_release_does_not_trigger_a_second_external_cancellation()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseCalls = ReleaseKeys(harness);
            var before = await SnapshotAsync(order.Id);

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
            Assert.Single(releaseCalls.Distinct());
            Assert.Equal(before, await SnapshotAsync(order.Id));

            Assert.All(
                harness.Reservation.ObservedRecoveryKeys,
                recoveryKey => Assert.Equal(releaseCalls.Distinct().Single(), recoveryKey));
        }

        [Fact]
        public async Task An_unknown_release_recovered_as_confirmed_finalizes_exactly_once()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal(OrderStatus.Cancelled, recovered.Status);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(2, reloaded.CommercialVersion);
            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.Single(reloaded.Changes, change => change.ChangeType == OrderChangeType.Cancel);
            Assert.Equal(0m, reloaded.CustomerTotal);

            var again = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.True(again.IsReplay);
            Assert.Equal(first.OperationId, again.OperationId);

            var final = await ReloadAsync(order.Id);

            Assert.Single(final.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.Equal(2, final.CommercialVersion);
        }

        [Fact]
        public async Task A_rejected_release_does_not_publish_cancellation_or_pricing_events()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            harness.Events.Dispatched.Clear();
            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.DoesNotContain(harness.Events.Dispatched, IsCancellationEvent);
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

        [Fact]
        public async Task An_immediately_rejected_release_leaves_the_reservation_in_its_stable_state()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var before = await ReservationStatesAsync(order.Id);

            Assert.Equal([FulfillmentReservationStatus.Confirmed], before);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(before, await ReservationStatesAsync(order.Id));
            Assert.DoesNotContain(FulfillmentReservationStatus.CancellationPending, await ReservationStatesAsync(order.Id));
        }

        [Fact]
        public async Task An_immediately_unknown_release_moves_the_reservation_to_cancellation_pending()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var snapshot = await SnapshotAsync(order.Id);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(
                [FulfillmentReservationStatus.CancellationPending],
                await ReservationStatesAsync(order.Id));

            Assert.Equal(snapshot, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task A_recovery_confirming_the_release_marks_the_reservation_released()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            await harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal([FulfillmentReservationStatus.CancellationPending], await ReservationStatesAsync(order.Id));

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal([FulfillmentReservationStatus.Released], await ReservationStatesAsync(order.Id));
        }

        [Fact]
        public async Task A_recovery_rejecting_the_release_restores_the_previous_stable_reservation_state()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            var stableBefore = await ReservationStatesAsync(order.Id);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            await harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal([FulfillmentReservationStatus.CancellationPending], await ReservationStatesAsync(order.Id));

            var commercialBefore = await SnapshotAsync(order.Id);

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var recovered = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.Rejected, recovered.OperationStatus);

            var after = await ReservationStatesAsync(order.Id);

            Assert.DoesNotContain(FulfillmentReservationStatus.CancellationPending, after);
            Assert.Equal(stableBefore, after);
            Assert.Equal(commercialBefore, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task Mixed_reservation_outcomes_are_recorded_per_reservation_without_finalizing_the_order()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var second = await AddConfirmedReservationAsync(harness, order, "PNR-SECOND");

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.ReplyToRelease("PNR-SECOND", ProviderOperationOutcome.Rejected);

            var snapshot = await SnapshotAsync(order.Id);

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ProviderOperationOutcome.Unknown, outcome.ReservationReleaseOutcome);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);

            Assert.Equal(snapshot, await SnapshotAsync(order.Id));

            await using var command = _fixture.NewCommandContext();

            var reservations = await new FulfillmentReservationRepository(command).ListByOrderAsync(order.Id);

            var rejected = reservations.Single(reservation => reservation.Id == second);
            var unresolved = reservations.Single(reservation => reservation.Id != second);

            Assert.Equal(FulfillmentReservationStatus.Confirmed, rejected.Status);
            Assert.Equal(FulfillmentReservationStatus.CancellationPending, unresolved.Status);
        }

        [Fact]
        public async Task A_confirmed_release_is_not_rolled_back_when_another_reservation_is_unresolved()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var second = await AddConfirmedReservationAsync(harness, order, "PNR-SECOND");

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.ReplyToRelease("PNR-SECOND", ProviderOperationOutcome.Confirmed);

            var snapshot = await SnapshotAsync(order.Id);

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ProviderOperationOutcome.Unknown, outcome.ReservationReleaseOutcome);
            Assert.Equal(snapshot, await SnapshotAsync(order.Id));

            await using var command = _fixture.NewCommandContext();

            var reservations = await new FulfillmentReservationRepository(command).ListByOrderAsync(order.Id);

            Assert.Equal(
                FulfillmentReservationStatus.Released,
                reservations.Single(reservation => reservation.Id == second).Status);
            Assert.Equal(
                FulfillmentReservationStatus.CancellationPending,
                reservations.Single(reservation => reservation.Id != second).Status);
        }

        [Fact]
        public async Task Each_reservation_release_uses_its_own_stable_provider_operation_key()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var second = await AddConfirmedReservationAsync(harness, order, "PNR-SECOND");
            var first = await OtherReservationIdAsync(order.Id, second);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            var releaseKeys = ReleaseKeys(harness);

            Assert.Equal(2, releaseKeys.Distinct().Count());
            Assert.Contains($"release:{first}:{outcome.OperationId}", releaseKeys);
            Assert.Contains($"release:{second}:{outcome.OperationId}", releaseKeys);
        }

        [Fact]
        public async Task A_reservation_keeps_the_same_provider_key_between_release_and_recovery()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var second = await AddConfirmedReservationAsync(harness, order, "PNR-SECOND");
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            await harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseKeys = ReleaseKeys(harness).Distinct().OrderBy(value => value).ToList();

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Cancel.CancelAsync(order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var recoveryKeys = harness.Reservation.ObservedRecoveryKeys.Distinct().OrderBy(value => value).ToList();

            Assert.Equal(releaseKeys, recoveryKeys);
            Assert.Equal(releaseKeys, ReleaseKeys(harness).Distinct().OrderBy(value => value).ToList());
            Assert.NotEqual(0, second);
        }

        [Fact]
        public async Task An_unknown_and_a_rejected_reservation_resolve_independently_on_replay()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var rejectedId = await AddConfirmedReservationAsync(harness, order, "PNR-REJECTED");
            var unknownId = await OtherReservationIdAsync(order.Id, rejectedId);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.ReplyToRelease("PNR-REJECTED", ProviderOperationOutcome.Rejected);

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(FulfillmentReservationStatus.CancellationPending, await ReservationStateAsync(order.Id, unknownId));
            Assert.Equal(FulfillmentReservationStatus.Confirmed, await ReservationStateAsync(order.Id, rejectedId));

            var releaseCalls = ReleaseKeys(harness);
            var commercialBefore = await SnapshotAsync(order.Id);

            harness.Reservation.ReplyToRecovery($"release:{unknownId}", ProviderOperationOutcome.Confirmed);
            harness.Reservation.ReplyToRecovery($"release:{rejectedId}", ProviderOperationOutcome.Rejected);

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);

            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, unknownId));
            Assert.Equal(FulfillmentReservationStatus.Confirmed, await ReservationStateAsync(order.Id, rejectedId));

            Assert.Equal(commercialBefore, await SnapshotAsync(order.Id));
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
        }

        [Fact]
        public async Task Two_unknown_reservations_recover_to_independent_results()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var secondId = await AddConfirmedReservationAsync(harness, order, "PNR-SECOND");
            var firstId = await OtherReservationIdAsync(order.Id, secondId);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseCalls = ReleaseKeys(harness);
            var commercialBefore = await SnapshotAsync(order.Id);

            harness.Reservation.ReplyToRecovery($"release:{firstId}", ProviderOperationOutcome.Confirmed);
            harness.Reservation.ReplyToRecovery($"release:{secondId}", ProviderOperationOutcome.Rejected);

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);

            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, firstId));
            Assert.Equal(FulfillmentReservationStatus.Confirmed, await ReservationStateAsync(order.Id, secondId));

            Assert.Equal(commercialBefore, await SnapshotAsync(order.Id));
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
        }

        [Fact]
        public async Task Two_unknown_reservations_that_both_confirm_finalize_the_cancellation_once()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var secondId = await AddConfirmedReservationAsync(harness, order, "PNR-SECOND");
            var firstId = await OtherReservationIdAsync(order.Id, secondId);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            var releaseCalls = ReleaseKeys(harness);

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Equal(OrderStatus.Cancelled, replay.Status);

            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, firstId));
            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, secondId));

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(2, reloaded.CommercialVersion);
            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
            Assert.Equal(releaseCalls, ReleaseKeys(harness));
        }

        [Fact]
        public async Task Only_the_unresolved_reservation_is_recovered_after_a_mixed_first_attempt()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var confirmedId = await AddConfirmedReservationAsync(harness, order, "PNR-CONFIRMED");
            var unknownId = await OtherReservationIdAsync(order.Id, confirmedId);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.ReplyToRelease("PNR-CONFIRMED", ProviderOperationOutcome.Confirmed);

            var first = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, confirmedId));

            var releaseCalls = ReleaseKeys(harness);

            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var replay = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, key, order.CommercialVersion);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);

            Assert.All(
                harness.Reservation.ObservedRecoveryKeys,
                recoveryKey => Assert.StartsWith($"release:{unknownId}:", recoveryKey, StringComparison.Ordinal));

            Assert.Equal(releaseCalls, ReleaseKeys(harness));
            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, confirmedId));
            Assert.Equal(FulfillmentReservationStatus.Released, await ReservationStateAsync(order.Id, unknownId));
        }

        [Fact]
        public async Task A_non_releasable_rejected_reservation_row_does_not_block_finalization()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var rejectedRow = await AddRejectedReservationAsync(harness, order, "PNR-NEVER-HELD");

            Assert.Equal(FulfillmentReservationStatus.Rejected, await ReservationStateAsync(order.Id, rejectedRow));

            var outcome = await harness.Cancel.CancelAsync(
                order.Id, VoidReason.CustomerRequest, 7, NewKey(), order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(OrderStatus.Cancelled, outcome.Status);

            Assert.DoesNotContain(
                ReleaseKeys(harness),
                releaseKey => releaseKey.StartsWith($"release:{rejectedRow}:", StringComparison.Ordinal));

            Assert.Equal(FulfillmentReservationStatus.Rejected, await ReservationStateAsync(order.Id, rejectedRow));

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(2, reloaded.CommercialVersion);
            Assert.Single(reloaded.PriceChangeSets, set => set.Reason == PriceChangeReason.Cancellation);
        }

        private static bool IsCancellationEvent(Framework.Core.Domain.Events.IDomainEvent domainEvent)
            => domainEvent.GetType().Name is "OrderCancelled" or "OrderPricingChanged";

        private static IReadOnlyList<string> ReleaseKeys(OrderSliceHarness harness)
            => harness.Reservation.ObservedOperationKeys
                .Where(observed => observed.StartsWith(
                    Application.OrderAggregate.Services.Cancel.OrderCancelService.ReleaseStep,
                    StringComparison.Ordinal))
                .ToList();

        private async Task<CancelStateSnapshot> SnapshotAsync(long orderId)
        {
            var order = await ReloadAsync(orderId);

            await using var query = _fixture.NewQueryContext();

            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == orderId);

            return new CancelStateSnapshot(
                order.Status,
                order.CommercialSummary,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                order.PriceChangeSets.Count,
                order.PricingLines.Count,
                order.Changes.Count,
                order.OrderServices.Count(service => service.Status == OrderServiceStatus.Cancelled),
                details.ProjectionRevision,
                details.SnapshotJson);
        }

        private sealed record CancelStateSnapshot(
            OrderStatus Status,
            CommercialSummary CommercialSummary,
            int CommercialVersion,
            long FinancialSequence,
            long ObligationVersion,
            decimal CustomerTotal,
            int PriceChangeSets,
            int PricingLines,
            int Changes,
            int CancelledServices,
            long ProjectionRevision,
            string SnapshotJson);

        private async Task<IReadOnlyList<FulfillmentReservationStatus>> ReservationStatesAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            var reservations = await new FulfillmentReservationRepository(command).ListByOrderAsync(orderId);

            return reservations.Select(reservation => reservation.Status).OrderBy(status => status).ToList();
        }

        private async Task<FulfillmentReservationStatus> ReservationStateAsync(long orderId, long reservationId)
        {
            await using var command = _fixture.NewCommandContext();

            var reservations = await new FulfillmentReservationRepository(command).ListByOrderAsync(orderId);

            return reservations.Single(reservation => reservation.Id == reservationId).Status;
        }

        private async Task<long> OtherReservationIdAsync(long orderId, long knownId)
        {
            await using var command = _fixture.NewCommandContext();

            var reservations = await new FulfillmentReservationRepository(command).ListByOrderAsync(orderId);

            return reservations.Single(reservation => reservation.Id != knownId).Id;
        }

        private static Task<long> AddRejectedReservationAsync(
            OrderSliceHarness harness,
            Order order,
            string externalReference)
            => AddReservationAsync(harness, order, externalReference, ReservationMemberStatus.Rejected);

        private static Task<long> AddConfirmedReservationAsync(
            OrderSliceHarness harness,
            Order order,
            string externalReference)
            => AddReservationAsync(harness, order, externalReference, ReservationMemberStatus.Confirmed);

        private static async Task<long> AddReservationAsync(
            OrderSliceHarness harness,
            Order order,
            string externalReference,
            ReservationMemberStatus memberStatus)
        {
            var serviceIds = order.OrderServices.Select(service => service.Id).ToList();

            var reservation = FulfillmentReservation.Open(
                harness.Ids.NewId(),
                order.Id,
                harness.Ids.NewId(),
                OrderProviderType.Airline,
                "SECOND",
                serviceIds,
                harness.Ids,
                harness.Clock);

            reservation.Observe(
                serviceIds.Select(id => new ReservationServiceObservation(id, memberStatus)).ToList(),
                externalReference,
                null,
                harness.Clock);

            await harness.Reservations.AddAsync(reservation);
            await harness.UnitOfWork.SaveChangesAsync();

            return reservation.Id;
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
