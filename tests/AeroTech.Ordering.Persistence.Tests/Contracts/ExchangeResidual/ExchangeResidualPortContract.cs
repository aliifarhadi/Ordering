using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeResidual
{
    public abstract class ExchangeResidualPortContract
    {
        protected abstract IExchangeResidualValuePort Port();

        [Fact]
        public async Task A_confirmed_residual_identifies_the_instrument_it_created()
        {
            var request = ExchangeResidualPortFixture.Request();
            var result = await Port().FulfillAsync(request);

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));
            Assert.False(string.IsNullOrWhiteSpace(result.InstrumentReference));
            Assert.NotNull(result.Instrument);
            Assert.True(Enum.IsDefined(result.Instrument!.Value));
            Assert.Equal(request.Amount, result.Amount);
            Assert.Equal(request.CurrencyId, result.CurrencyId);
        }

        [Fact]
        public async Task Recovering_a_residual_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.NeverDispatchedKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.InstrumentReference);
        }

        [Fact]
        public async Task A_dispatched_residual_is_recoverable_under_its_own_operation_key()
        {
            var port = Port();
            var dispatched = await port.FulfillAsync(ExchangeResidualPortFixture.Request());

            var recovery = await port.RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
            {
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
                Assert.Equal(dispatched.InstrumentReference, recovery.InstrumentReference);
                Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
            }
        }

        [Fact]
        public async Task Fulfilling_the_same_operation_key_twice_never_creates_a_second_instrument()
        {
            var port = Port();

            var first = await port.FulfillAsync(ExchangeResidualPortFixture.Request());
            var second = await port.FulfillAsync(ExchangeResidualPortFixture.Request());

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.InstrumentReference, second.InstrumentReference);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.Amount, second.Amount);
        }

        [Fact]
        public async Task An_operation_key_cannot_be_reused_for_a_different_obligation()
        {
            var port = Port();

            await port.FulfillAsync(ExchangeResidualPortFixture.Request());

            foreach (var conflicting in ExchangeResidualPortFixture.ConflictingRequests())
                await Assert.ThrowsAnyAsync<Exception>(() => port.FulfillAsync(conflicting));
        }

        [Fact]
        public async Task One_operation_key_can_never_consume_another_operations_instrument()
        {
            var port = Port();
            var mine = await port.FulfillAsync(ExchangeResidualPortFixture.Request());

            var other = await port.RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.OtherKey));

            Assert.False(other.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, other.Outcome);
            Assert.NotEqual(mine.InstrumentReference, other.InstrumentReference);
        }

        [Fact]
        public async Task A_read_back_never_rewrites_the_evidence_of_a_dispatched_residual()
        {
            var port = Port();
            var dispatched = await port.FulfillAsync(ExchangeResidualPortFixture.Request());

            var first = await port.RecoverAsync(ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));
            var second = await port.RecoverAsync(ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));

            Assert.True(first.WasDispatched);
            Assert.Equal(first.Amount, second.Amount);
            Assert.Equal(first.CurrencyId, second.CurrencyId);
            Assert.Equal(first.InstrumentReference, second.InstrumentReference);

            if (dispatched.Amount is not null)
                Assert.Equal(dispatched.Amount, first.Amount);
        }
    }
}
