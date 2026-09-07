namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record TrafficDocumentReassigned(
        long DocumentId,
        string DocumentNumber,
        long SourceOrderId,
        long NewOrderId,
        long NewTravellerId) : BaseIntegrationEvent;
}
