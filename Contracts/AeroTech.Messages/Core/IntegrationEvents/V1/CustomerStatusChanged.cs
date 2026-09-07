using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record CustomerStatusChanged(
        long CustomerId,
        CustomerType CustomerType,
        long SubjectId,
        CustomerStatus Status,
        DateOnly? RelationshipEndedOn,
        long SourceVersion) : BaseIntegrationEvent;
}
