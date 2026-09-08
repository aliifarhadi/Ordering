using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public sealed record GenericServiceSchema(
        string SchemaName,
        string SchemaVersion,
        OrderServiceType ServiceType,
        IReadOnlyList<string> RequiredAttributes,
        bool RequiresReservation,
        bool RequiresDocument,
        ServiceDocumentKind? DocumentKind);
}
