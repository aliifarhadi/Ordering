namespace AeroTech.Ordering.Domain._Shared.Versioning
{
    public sealed class CommercialEventSequence
    {
        private int _ordinal;

        public int Current => _ordinal;

        public int Next() => ++_ordinal;

        public void Reset() => _ordinal = 0;
    }
}
