using AeroTech.Ordering.Application.OrderAggregate.Commands.RefundDocument;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public static class RefundDocumentRequestMapper
    {
        public static RefundDocumentCommand ToCommand(
            long orderId,
            long documentId,
            RefundDocumentRequest request,
            string idempotencyKey)
            => new(
                orderId,
                documentId,
                request.TicketCouponIds ?? [],
                idempotencyKey,
                request.ExpectedCommercialVersion,
                request.QuotedRefundId,
                ToInstruction(request.Manual));

        private static ManualRefundInstruction? ToInstruction(ManualRefundRequest? manual)
            => manual is null
                ? null
                : new ManualRefundInstruction(
                    manual.AuthorityReference,
                    manual.Reason,
                    manual.ApprovedRefundAmount,
                    manual.ApprovedDisposition,
                    manual.PricingLines ?? [],
                    manual.DispositionReference,
                    manual.SourcePricingReference,
                    manual.SourceRefundType,
                    manual.SourceEvidence);
    }
}
