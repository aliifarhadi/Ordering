using AeroTech.Ordering.Domain.OrderAggregate.Contracts;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class RecordLocatorGenerator : IRecordLocatorGenerator
    {
        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int Length = 6;

        public string Generate()
        {
            var characters = new char[Length];
            for (var index = 0; index < Length; index++)
                characters[index] = Alphabet[Random.Shared.Next(Alphabet.Length)];

            return new string(characters);
        }
    }
}
