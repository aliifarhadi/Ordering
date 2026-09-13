using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation
{
    public sealed record ServicingOperationSnapshot(
        long OperationId,
        long OrderId,
        ServicingOperationKind Kind,
        ServicingOperationStatus Status,
        long ClaimGeneration,
        int? ExpectedCommercialVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string? CallerScope,
        string? IdempotencyKey,
        CommandReceiptStatus? ReceiptStatus);
}
