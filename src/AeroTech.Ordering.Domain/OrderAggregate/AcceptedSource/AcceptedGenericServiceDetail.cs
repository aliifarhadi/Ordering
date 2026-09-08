namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedGenericServiceDetail(
        string SchemaName,
        string SchemaVersion,
        string AttributesJson) : AcceptedServiceDetail;
}
