using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.DocumentVoid
{
    public interface IDocumentVoidService
    {
        Task<DocumentVoidOutcome> VoidAsync(
            long orderId,
            long documentId,
            VoidReason reason,
            string? reasonDetail,
            long voidedBy,
            string idempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
