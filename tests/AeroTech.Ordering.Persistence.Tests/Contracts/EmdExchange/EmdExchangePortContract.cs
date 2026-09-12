using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdExchange
{
    public abstract class EmdExchangePortContract
    {
        protected abstract IEmdExchangePort Port();

        [Fact]
        public async Task A_confirmed_exchange_names_the_successor_document_it_created()
        {
            var request = EmdExchangePortFixture.Request();
            var result = await Port().ExchangeAsync(request);

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));
            Assert.NotNull(result.Successor);
            Assert.False(string.IsNullOrWhiteSpace(result.Successor!.DocumentNumber));
            Assert.Equal(request.SuccessorType, result.Successor.Type);
            Assert.Equal(request.CurrencyId, result.Successor.CurrencyId);
            Assert.Equal(request.SuccessorReasonForIssuanceCode, result.Successor.ReasonForIssuanceCode);
            Assert.Equal(request.SuccessorCoupons.Count, result.Successor.Coupons.Count);
        }

        [Fact]
        public async Task A_successor_document_number_is_never_supplied_by_ordering()
        {
            var request = EmdExchangePortFixture.Request();
            var result = await Port().ExchangeAsync(request);

            if (result.Successor is not { } successor)
                return;

            Assert.NotEqual(request.SourceDocumentNumber, successor.DocumentNumber);
            Assert.DoesNotContain(
                EmdExchangePortFixture.OrderId.ToString(),
                successor.DocumentNumber,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                EmdExchangePortFixture.OperationId.ToString(),
                successor.DocumentNumber,
                StringComparison.Ordinal);
        }

        [Fact]
        public async Task Recovering_an_exchange_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.NeverDispatchedKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.Successor);
            Assert.Null(recovery.ProviderReference);
        }

        [Fact]
        public async Task A_dispatched_exchange_is_recoverable_under_its_own_operation_key()
        {
            var port = Port();
            var dispatched = await port.ExchangeAsync(EmdExchangePortFixture.Request());

            var recovery = await port.RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.True(Enum.IsDefined(recovery.Outcome));
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
            {
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
                Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
                Assert.Equal(dispatched.Successor?.DocumentNumber, recovery.Successor?.DocumentNumber);
            }
        }

        [Fact]
        public async Task Exchanging_the_same_operation_key_twice_never_creates_a_second_successor()
        {
            var port = Port();

            var first = await port.ExchangeAsync(EmdExchangePortFixture.Request());
            var second = await port.ExchangeAsync(EmdExchangePortFixture.Request());

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.Successor?.DocumentNumber, second.Successor?.DocumentNumber);
        }

        [Fact]
        public async Task An_operation_key_cannot_be_reused_for_a_different_exchange()
        {
            var port = Port();

            await port.ExchangeAsync(EmdExchangePortFixture.Request());

            foreach (var conflicting in EmdExchangePortFixture.ConflictingRequests())
                await Assert.ThrowsAnyAsync<Exception>(() => port.ExchangeAsync(conflicting));
        }

        [Fact]
        public async Task One_operation_key_can_never_consume_another_operations_exchange()
        {
            var port = Port();
            var mine = await port.ExchangeAsync(EmdExchangePortFixture.Request());

            var other = await port.RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.OtherKey));

            Assert.False(other.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, other.Outcome);
            Assert.NotEqual(mine.ProviderReference, other.ProviderReference);
        }

        [Fact]
        public async Task A_read_back_never_rewrites_the_evidence_of_a_dispatched_exchange()
        {
            var port = Port();

            await port.ExchangeAsync(EmdExchangePortFixture.Request());

            var first = await port.RecoverAsync(EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));
            var second = await port.RecoverAsync(EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));

            Assert.True(first.WasDispatched);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.Successor?.DocumentNumber, second.Successor?.DocumentNumber);
        }

        [Fact]
        public async Task A_coupled_residual_obligation_is_answered_in_the_same_operation()
        {
            var result = await Port().ExchangeAsync(EmdExchangePortFixture.ResidualRequest());

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.NotNull(result.Residual);
            Assert.Equal(5_000m, result.Residual!.Amount);
            Assert.Equal(EmdExchangePortFixture.CurrencyId, result.Residual.CurrencyId);
            Assert.False(string.IsNullOrWhiteSpace(result.Residual.DocumentNumber));
        }

        [Fact]
        public async Task An_exchange_without_a_coupled_obligation_returns_no_residual_document()
        {
            var result = await Port().ExchangeAsync(EmdExchangePortFixture.Request());

            if (result.Outcome == ProviderOperationOutcome.Confirmed)
                Assert.Null(result.Residual);
        }
    }
}
