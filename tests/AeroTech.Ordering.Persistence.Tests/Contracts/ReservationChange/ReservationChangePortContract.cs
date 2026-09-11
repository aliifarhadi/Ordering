using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ReservationChange;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ReservationChange
{
    public abstract class ReservationChangePortContract
    {
        protected abstract IReservationChangePort Port();

        [Fact]
        public async Task Applying_a_reservation_change_reports_a_defined_provider_outcome()
        {
            var result = await Port().ApplyAsync(ReservationChangePortFixture.Request());

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome == ProviderOperationOutcome.Confirmed)
                Assert.False(string.IsNullOrWhiteSpace(result.ExternalReservationRef));
        }

        [Fact]
        public void A_reservation_change_names_only_replaced_services_and_their_replacements()
        {
            var request = ReservationChangePortFixture.Request();

            Assert.NotEmpty(request.Items);
            Assert.False(string.IsNullOrWhiteSpace(request.OperationKey));
            Assert.Equal(
                request.Items.Select(item => item.ReplacedOrderServiceId).Distinct().Count(),
                request.Items.Count);
            Assert.All(request.Items, item =>
            {
                Assert.NotEqual(item.ReplacedOrderServiceId, item.ReplacementOrderServiceId);
                Assert.True(item.ReplacementOrderSegmentId > 0);
                Assert.True(item.ReplacementFlightCapacityId > 0);
                Assert.True(item.TravelerId > 0);
            });
        }

        [Fact]
        public async Task Recovering_an_operation_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverAsync(ReservationChangePortFixture.UnknownRecovery());

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
        }

        [Fact]
        public async Task Recovering_a_dispatched_operation_reports_that_it_was_dispatched()
        {
            var port = Port();

            await port.ApplyAsync(ReservationChangePortFixture.Request());

            var recovery = await port.RecoverAsync(ReservationChangePortFixture.Recovery());

            Assert.True(recovery.WasDispatched);
            Assert.True(Enum.IsDefined(recovery.Outcome));
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);
        }

        [Fact]
        public async Task An_unresolved_operation_is_read_back_rather_than_applied_again()
        {
            var port = Port();

            await port.ApplyAsync(ReservationChangePortFixture.Request());

            var first = await port.RecoverAsync(ReservationChangePortFixture.Recovery());
            var second = await port.RecoverAsync(ReservationChangePortFixture.Recovery());

            Assert.Equal(first.WasDispatched, second.WasDispatched);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ExternalReservationRef, second.ExternalReservationRef);
        }
    }
}
