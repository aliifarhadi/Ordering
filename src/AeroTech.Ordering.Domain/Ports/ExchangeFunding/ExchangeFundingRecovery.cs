using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public sealed record ExchangeFundingRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null)
    {
        public ExchangeFundingResult AsResult() => new(Outcome, ProviderReference, Amount, CurrencyId, Detail);
    }
}
