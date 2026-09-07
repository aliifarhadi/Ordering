namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class RecordLocator
    {
        private RecordLocator()
        {
        }

        public RecordLocator(string value) => Value = value;

        public string Value { get; private set; } = default!;
    }
}
