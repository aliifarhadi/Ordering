using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record CustomerModified(
        long CustomerId,
        string CustomerNumber,
        CustomerType CustomerType,
        long SubjectId,
        CustomerStatus Status,
        int? PreferredCurrencyId,
        string? PreferredLanguageCode,
        DateOnly RelationshipStartedOn,
        DateOnly? RelationshipEndedOn,
        long SourceVersion) : BaseIntegrationEvent;
}
