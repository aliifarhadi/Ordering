using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public sealed record ExchangeFundingResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null);
}
