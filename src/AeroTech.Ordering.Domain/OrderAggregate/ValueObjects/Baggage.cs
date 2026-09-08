using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class Baggage : ValueObject
    {
        private Baggage()
        {
        }

        public Baggage(decimal weight, BaggageWeightUnit unit, int pieces)
        {
            Weight = weight;
            Unit = unit;
            Pieces = pieces;
        }

        public decimal Weight { get; private set; }

        public BaggageWeightUnit Unit { get; private set; }

        public int Pieces { get; private set; }

        public Baggage Copy() => new()
        {
            Weight = Weight,
            Unit = Unit,
            Pieces = Pieces
        };

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Weight;
            yield return Unit;
            yield return Pieces;
        }
    }
}
