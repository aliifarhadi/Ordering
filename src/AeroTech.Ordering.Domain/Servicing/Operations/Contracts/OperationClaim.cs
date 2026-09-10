namespace AeroTech.Ordering.Domain._Shared.Operations.Contracts
{
    public sealed record OperationClaim(
        long OperationId,
        long OrderId,
        long Generation,
        DateTimeOffset RecoveryLeaseUntil);
}
