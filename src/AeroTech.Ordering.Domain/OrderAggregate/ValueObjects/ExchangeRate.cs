using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class ExchangeRate : ValueObject
    {
        private ExchangeRate()
        {
        }

        public ExchangeRate(ExchangeRateArgs args)
        {
            RateOfExchange = args.RateOfExchange;
            NumberOfDecimalPlaces = args.NumberOfDecimalPlaces;
            RateOfExchangeId = args.RateOfExchangeId;
            RoundingFactor = args.RoundingFactor;
        }

        public decimal RateOfExchange { get; private set; }

        public int NumberOfDecimalPlaces { get; private set; }

        public string? RateOfExchangeId { get; private set; }

        public int RoundingFactor { get; private set; }

        public ExchangeRate Copy() => new()
        {
            RateOfExchange = RateOfExchange,
            NumberOfDecimalPlaces = NumberOfDecimalPlaces,
            RateOfExchangeId = RateOfExchangeId,
            RoundingFactor = RoundingFactor
        };

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return RateOfExchange;
            yield return NumberOfDecimalPlaces;
            yield return RateOfExchangeId;
            yield return RoundingFactor;
        }
    }
}
