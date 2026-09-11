using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P1
{
    public sealed class DocumentStockTests
    {
        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void Numbers_are_sequential_within_the_configured_range_and_never_random()
        {
            var stock = NewStock(rangeFrom: 100, rangeTo: 105);

            var first = stock.Allocate(1, "Ticket:1", _ids, _clock);
            var second = stock.Allocate(1, "Ticket:2", _ids, _clock);

            Assert.Equal("999" + "0000000100", first.DocumentNumber);
            Assert.Equal("999" + "0000000101", second.DocumentNumber);
            Assert.Equal(102, stock.NextNumber);
        }

        [Fact]
        public void A_retried_allocation_for_the_same_operation_and_role_reuses_its_number()
        {
            var stock = NewStock();

            var first = stock.Allocate(7, "Ticket:1", _ids, _clock);
            var retry = stock.Allocate(7, "Ticket:1", _ids, _clock);

            Assert.Equal(first.DocumentNumber, retry.DocumentNumber);
            Assert.Single(stock.Allocations);
        }

        [Fact]
        public void Different_roles_in_one_operation_receive_different_numbers()
        {
            var stock = NewStock();

            var first = stock.Allocate(7, "Ticket:1", _ids, _clock);
            var second = stock.Allocate(7, "Ticket:2", _ids, _clock);

            Assert.NotEqual(first.DocumentNumber, second.DocumentNumber);
        }

        [Fact]
        public void An_exhausted_stock_refuses_further_allocation()
        {
            var stock = NewStock(rangeFrom: 1, rangeTo: 1);

            stock.Allocate(1, "Ticket:1", _ids, _clock);

            var error = Assert.Throws<BusinessException>(() => stock.Allocate(2, "Ticket:1", _ids, _clock));

            Assert.Equal(20081, error.Code);
            Assert.Equal(DocumentStockStatus.Exhausted, stock.Status);
        }

        [Fact]
        public void A_retired_number_is_not_recycled()
        {
            var stock = NewStock();

            var first = stock.Allocate(1, "Ticket:1", _ids, _clock);
            stock.Retire(1, "Ticket:1", _clock);

            var next = stock.Allocate(2, "Ticket:1", _ids, _clock);

            Assert.NotEqual(first.DocumentNumber, next.DocumentNumber);
            Assert.Equal(StockNumberState.Retired, stock.Allocations.Single(a => a.Id == first.Id).State);
        }

        [Fact]
        public void An_unconfigured_check_digit_profile_is_refused_rather_than_invented()
        {
            var stock = DocumentStock.Define(1, 1, null, "ETKT", "999", 10, "IATA-MOD7", 1, 10);

            var error = Assert.Throws<BusinessException>(() => stock.Allocate(1, "Ticket:1", _ids, _clock));

            Assert.Equal(20084, error.Code);
        }

        private DocumentStock NewStock(long rangeFrom = 100, long rangeTo = 999) =>
            DocumentStock.Define(1, 1, null, "ETKT", "999", 10, DocumentStock.NoCheckDigitProfile, rangeFrom, rangeTo);
    }
}
