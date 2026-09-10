namespace AeroTech.Ordering.Domain.Servicing.Operations.Contracts
{
    public sealed record OperationClaim(
        long OperationId,
        long OrderId,
        long Generation,
        DateTimeOffset RecoveryLeaseUntil);
}
