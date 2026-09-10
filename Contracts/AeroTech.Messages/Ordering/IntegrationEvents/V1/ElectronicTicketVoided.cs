using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketVoided(
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        VoidReason Reason,
        string? ReasonDetail,
        long VoidedBy,
        DateTimeOffset VoidedAt,
        string? ProviderReference,
        int DocumentVersion) : BaseIntegrationEvent;
}
