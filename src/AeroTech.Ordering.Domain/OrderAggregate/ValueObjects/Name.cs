using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class Name : ValueObject
    {
        private const int MinLength = 2;
        private const int MaxLength = 30;

        private Name()
        {
        }

        public Name(string firstName, string? surName, bool noSurname)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw ExceptionFactory.FirstNameIsRequired();

            if (firstName.Length < MinLength || HasNonEnglishChars(firstName))
                throw ExceptionFactory.FirstNameIsInvalid();

            if (string.IsNullOrWhiteSpace(surName) && !noSurname)
                throw ExceptionFactory.SurnameIsRequired();

            if (!noSurname && (surName!.Length < MinLength || HasNonEnglishChars(surName)))
                throw ExceptionFactory.SurnameIsInvalid();

            if (firstName.Length + (surName?.Length ?? 0) > MaxLength)
                throw ExceptionFactory.NameIsTooLong();

            FirstName = firstName.ToUpperInvariant();
            SurName = (surName ?? firstName).ToUpperInvariant();
            NoSurname = noSurname;
        }

        public string FirstName { get; private set; } = default!;

        public string SurName { get; private set; } = default!;

        public bool NoSurname { get; private set; }

        public Name Copy() => new()
        {
            FirstName = FirstName,
            SurName = SurName,
            NoSurname = NoSurname
        };

        private static bool HasNonEnglishChars(string value) => value.Any(character => character > 127);

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return FirstName;
            yield return SurName;
            yield return NoSurname;
        }
    }
}
