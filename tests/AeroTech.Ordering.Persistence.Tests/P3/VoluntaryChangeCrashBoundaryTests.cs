using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class VoluntaryChangeCrashBoundaryTests
    {
        private const string SourceSystem = "AirPrice";
        private const string QuoteId = "CHG-QUOTE-1";
        private const string TargetRef = "AIRPRICE-TARGET-1";
        private const long ReplacementCapacityReference = 987_654L;
        private const string ReplacementBookingClass = "Q";

        private readonly OrderingDatabaseFixture _fixture;

        public VoluntaryChangeCrashBoundaryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A. AirPrice acceptance

        [Fact]
        public async Task A2_accept_failure_retains_the_claim_and_blocks_other_operations()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);

            harness.ChangeQuotes.ThrowOnAccept = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Null(await harness.ChangePlans.FindAsync(
                harness.ChangeQuotes.ObservedSelections.Single().OperationId));
        }

        [Fact]
        public async Task A3_accept_replay_reuses_the_same_operation_key_and_intent()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.ChangeQuotes.ThrowOnAccept = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(scenario.Execution(key)));

            harness.ChangeQuotes.ThrowOnAccept = false;

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            var selections = harness.ChangeQuotes.ObservedSelections;

            Assert.Equal(2, selections.Count);
            Assert.Equal(selections[0].OperationKey, selections[1].OperationKey);
            Assert.Equal(selections[0].OperationId, selections[1].OperationId);
            Assert.Equal(selections[0].QuotedChangeId, selections[1].QuotedChangeId);
            Assert.Equal(selections[0].ExpectedCommercialVersion, selections[1].ExpectedCommercialVersion);
            Assert.Equal(selections[0].OrderServiceId, selections[1].OrderServiceId);
            Assert.Equal(selections[0].TicketCouponId, selections[1].TicketCouponId);
        }

        // ---------------------------------------------------------------- C. eligibility

        [Fact]
        public async Task C5_denied_terminal_replay_releases_the_replay_claim()
            => await TerminalReplayReleasesClaimAsync(DocumentChangeEligibilityOutcome.Denied);

        [Fact]
        public async Task C6_reissue_required_terminal_replay_releases_the_replay_claim()
            => await TerminalReplayReleasesClaimAsync(DocumentChangeEligibilityOutcome.ReissueRequired);

        private async Task TerminalReplayReleasesClaimAsync(DocumentChangeEligibilityOutcome terminal)
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentChangeEligibilities.Outcome = terminal;

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            var replay = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);
            Assert.Single(harness.DocumentChangeEligibilities.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);

            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- D/E. inventory boundaries

        [Fact]
        public async Task D6_apply_failure_after_dispatch_retains_the_claim_and_recovers()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.ReservationChanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.ReservationChanges.ThrowAfterDispatch = false;
            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
        }

        [Fact]
        public async Task E1_reservation_confirmation_is_durable_before_the_first_document_call()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentRevalidations.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(scenario.Execution(key)));

            var plan = await PlanAsync(harness);

            Assert.Equal(ProviderOperationOutcome.Confirmed, plan.ReservationOutcome);
            Assert.True(plan.IsReservationConfirmed);
            Assert.Null(plan.RevalidationOutcome);
            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Empty(harness.DocumentRevalidations.DispatchedKeys);
        }

        [Fact]
        public async Task E2_crash_before_document_dispatch_recovers_then_dispatches_once()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentRevalidations.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(scenario.Execution(key)));

            var attempts = harness.DocumentRevalidations.ObservedRequests.Count;

            harness.DocumentRevalidations.ThrowBeforeDispatch = false;

            var recovered = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentRevalidations.ObservedRecoveryKeys);
            Assert.Equal(attempts + 1, harness.DocumentRevalidations.ObservedRequests.Count);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
            Assert.Equal(
                harness.DocumentRevalidations.ObservedRequests[^1].OperationKey,
                harness.DocumentRevalidations.ObservedRecoveryKeys.Single());
        }

        [Fact]
        public async Task E3_reservation_recovered_confirmed_never_blind_dispatches_the_document()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentRevalidations.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
        }

        // ---------------------------------------------------------------- F. document boundaries

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending, ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ProviderOperationOutcome.Unknown, ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ProviderOperationOutcome.Rejected, ServicingOperationStatus.NeedsReconciliation)]
        public async Task F2_to_F4_a_nonconfirmed_document_outcome_is_durable_and_holds_the_claim(
            ProviderOperationOutcome outcome,
            ServicingOperationStatus expected)
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);

            harness.DocumentRevalidations.RevalidationOutcome = outcome;

            var result = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(NewKey()));
            var plan = await PlanAsync(harness);

            Assert.Equal(expected, result.OperationStatus);
            Assert.Equal(outcome, plan.RevalidationOutcome);
            Assert.False(plan.IsRevalidationConfirmed);

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            Assert.Equal(scenario.CommercialVersion, (await ReloadAsync(scenario.OrderId)).CommercialVersion);
        }

        [Fact]
        public async Task F6_document_failure_after_dispatch_never_redispatches()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentRevalidations.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(scenario.Execution(key)));

            harness.DocumentRevalidations.ThrowAfterDispatch = false;
            harness.DocumentRevalidations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Single(harness.DocumentRevalidations.ObservedRecoveryKeys);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        [InlineData(ProviderOperationOutcome.Rejected)]
        public async Task F9_F10_a_dispatched_document_operation_is_never_revalidated_again(
            ProviderOperationOutcome recoveryOutcome)
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentRevalidations.RevalidationOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            harness.DocumentRevalidations.RecoveryOutcome = recoveryOutcome;

            var replay = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Single(harness.DocumentRevalidations.ObservedRecoveryKeys);
            Assert.NotEqual(ChangeDocumentOutcome.Revalidated, replay.DocumentOutcome);
            Assert.Equal(scenario.CommercialVersion, (await ReloadAsync(scenario.OrderId)).CommercialVersion);
        }

        // ---------------------------------------------------------------- G. provider confirmed -> local commit

        [Fact]
        public async Task G1_G2_durable_document_confirmation_finalizes_without_provider_calls()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentRevalidations.RevalidationOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            harness.DocumentRevalidations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            var plan = await PlanAsync(harness);

            Assert.Equal(ProviderOperationOutcome.Confirmed, plan.RevalidationOutcome);
            Assert.True(plan.IsRevalidationConfirmed);
            Assert.NotNull(plan.RevalidationProviderReference);

            var applies = harness.ReservationChanges.ObservedApplies.Count;
            var recoveries = harness.DocumentRevalidations.ObservedRecoveryKeys.Count;
            var requests = harness.DocumentRevalidations.ObservedRequests.Count;

            var replay = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.True(replay.IsReplay);
            Assert.Equal(applies, harness.ReservationChanges.ObservedApplies.Count);
            Assert.Equal(recoveries, harness.DocumentRevalidations.ObservedRecoveryKeys.Count);
            Assert.Equal(requests, harness.DocumentRevalidations.ObservedRequests.Count);

            var after = await ReloadAsync(scenario.OrderId);
            var ticket = await TicketAsync(scenario.OrderId, scenario.TicketId);

            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
            Assert.Single(ticket.Revalidations);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(scenario.DocumentVersion + 1, ticket.DocumentVersion);
        }

        [Fact]
        public async Task G3_H6_a_completed_replay_makes_no_external_call_and_releases_its_claim()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);
            var key = NewKey();

            await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            var settled = await ReloadAsync(scenario.OrderId);
            var applies = harness.ReservationChanges.ObservedApplies.Count;
            var requests = harness.DocumentRevalidations.ObservedRequests.Count;
            var accepts = harness.ChangeQuotes.ObservedSelections.Count;

            var replay = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(scenario.OrderId);
            var ticket = await TicketAsync(scenario.OrderId, scenario.TicketId);

            Assert.True(replay.IsReplay);
            Assert.Equal(applies, harness.ReservationChanges.ObservedApplies.Count);
            Assert.Equal(requests, harness.DocumentRevalidations.ObservedRequests.Count);
            Assert.Equal(accepts, harness.ChangeQuotes.ObservedSelections.Count);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);

            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
            Assert.Single(ticket.Revalidations);
            Assert.Equal(settled.CommercialVersion, after.CommercialVersion);
            Assert.Equal(settled.OrderServices.Count, after.OrderServices.Count);

            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- H/I. claim and version protection

        [Fact]
        public async Task H2_needs_reconciliation_keeps_the_order_blocked()
        {
            await using var harness = NewHarness();
            var scenario = await ScenarioAsync(harness);

            harness.DocumentRevalidations.RevalidationOutcome = ProviderOperationOutcome.Rejected;

            var result = await harness.VoluntaryChange.ChangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, result.OperationStatus);

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task I1_a_commercial_version_mismatch_on_resume_fails_closed()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"crash-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await ScenarioAsync(setup);
            var key = NewKey();

            setup.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.PendingEvidence;

            await setup.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            var operationId = setup.ChangeQuotes.ObservedSelections[^1].OperationId;

            await BumpCommercialVersionAsync(scenario.OrderId);

            await using var resume = NewHarness(caller);

            var resumed = await resume.VoluntaryChange.ChangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, resumed.OperationStatus);
            Assert.Empty(resume.ChangeQuotes.ObservedSelections);
            Assert.Empty(resume.DocumentChangeEligibilities.ObservedRequests);
            Assert.Empty(resume.ReservationChanges.ObservedApplies);
            Assert.Empty(resume.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resume.DocumentRevalidations.ObservedRequests);

            var plan = await resume.ChangePlans.FindAsync(operationId);

            Assert.Equal(scenario.CommercialVersion, plan!.ExpectedCommercialVersion);
            Assert.DoesNotContain(
                (await ReloadAsync(scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.VoluntaryChange);
        }

        // ---------------------------------------------------------------- fixture

        private const int ClaimConflict = 2700;

        private static async Task<int?> SecondOperationCodeAsync(OrderSliceHarness harness, Scenario scenario)
        {
            try
            {
                await harness.VoluntaryChange.ChangeAsync(scenario.Execution(NewKey()));

                return null;
            }
            catch (BusinessException exception)
            {
                return exception.Code;
            }
        }

        private sealed record Scenario(
            long OrderId,
            long ServiceId,
            long TicketId,
            long CouponId,
            int CommercialVersion,
            int DocumentVersion)
        {
            public VoluntaryChangeExecution Execution(string key)
                => new(OrderId, ServiceId, QuoteId, key, CommercialVersion);
        }

        private async Task<Scenario> ScenarioAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var created = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            var order = await ReloadAsync(created.Id);
            var ticket = (await TicketsAsync(order.Id)).OrderBy(candidate => candidate.Id).First();
            var coupon = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).First();

            harness.ChangeQuotes.Quote(QuoteOf(order, ticket, coupon), AcceptedOf(order, ticket, coupon));

            return new Scenario(
                order.Id,
                coupon.CurrentOrderServiceId,
                ticket.Id,
                coupon.Id,
                order.CommercialVersion,
                ticket.DocumentVersion);
        }

        private async Task<AcceptedChangePlan> PlanAsync(OrderSliceHarness harness)
        {
            var operationId = harness.ChangeQuotes.ObservedSelections[^1].OperationId;
            var plan = await harness.ChangePlans.FindAsync(operationId);

            Assert.NotNull(plan);

            return plan!;
        }

        private async Task BumpCommercialVersionAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[Orders] SET [CommercialVersion] = [CommercialVersion] + 1 WHERE [Id] = {0}",
                orderId);
        }

        private static ChangeQuote QuoteOf(Order order, ElectronicTicket ticket, TicketCoupon coupon)
            => new(
                SourceSystem, QuoteId, TargetRef, PricingSource.PricingEngine, order.Id,
                order.CommercialVersion, order.CurrencyId, ticket.Id,
                coupon.CurrentOrderServiceId, coupon.Id,
                order.OrderServices.Where(s => s.Id != coupon.CurrentOrderServiceId).Select(s => s.Id).ToList(),
                Replacement(order, coupon), ChangeMonetaryOutcome.Even, DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedVoluntaryChange AcceptedOf(Order order, ElectronicTicket ticket, TicketCoupon coupon)
            => new(
                SourceSystem, QuoteId, TargetRef, PricingSource.PricingEngine, order.Id,
                order.CommercialVersion, order.CurrencyId, ticket.Id,
                coupon.CurrentOrderServiceId, coupon.Id,
                order.OrderServices.Where(s => s.Id != coupon.CurrentOrderServiceId).Select(s => s.Id).ToList(),
                Replacement(order, coupon), ChangeMonetaryOutcome.Even, DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedChangeReplacement Replacement(Order order, TicketCoupon coupon)
        {
            var service = order.OrderServices.Single(candidate => candidate.Id == coupon.CurrentOrderServiceId);
            var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

            return new AcceptedChangeReplacement(
                "REPLACEMENT-1",
                service.ServiceCode,
                service.Name,
                new AcceptedSegment(
                    "REPLACEMENT-SEG-1",
                    segment.Sequence,
                    segment.FlightId,
                    segment.FlightVersion,
                    segment.Number,
                    segment.OriginAirportId,
                    segment.OriginAirportTerminalId,
                    segment.DestinationAirportId,
                    segment.DestinationAirportTerminalId,
                    segment.MarketingAirlineId,
                    segment.OperatingAirlineId,
                    segment.DepartureDateTime.AddDays(2),
                    segment.ArrivalDateTime.AddDays(2),
                    segment.Duration,
                    segment.AircraftId,
                    segment.CabinClassId,
                    segment.RbdId,
                    ReplacementBookingClass,
                    segment.BookingClassCode,
                    ReplacementCapacityReference,
                    segment.AirFareId,
                    []),
                new AcceptedAirTransportDetail("REPLACEMENT-SEG-1"),
                service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList());
        }

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AirlineUser(7401, $"crash-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<ElectronicTicket> TicketAsync(long orderId, long ticketId)
            => (await TicketsAsync(orderId)).Single(ticket => ticket.Id == ticketId);

        private async Task<IReadOnlyList<ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }
    }
}
