using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.RefundValueCorrection;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public sealed class RefundValueCorrectionCoordinator : IRefundValueCorrectionCoordinator
    {
        public const string ValueStep = "cancel-refund-value";

        private readonly IRefundValueCorrectionPort _value;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IClock _clock;

        public RefundValueCorrectionCoordinator(
            IRefundValueCorrectionPort value,
            IOrderOperationCoordinator operations,
            IClock clock)
        {
            _value = value;
            _operations = operations;
            _clock = clock;
        }

        public async Task<ProviderOperationOutcome> SettleAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            RefundValueDispatch dispatch,
            CancellationToken cancellationToken = default)
        {
            var result = dispatch == RefundValueDispatch.FirstAttempt
                ? await RequestAsync(orderId, operation, ticket, refund, cancellationToken)
                : await RecoverThenRequestAsync(orderId, operation, ticket, refund, cancellationToken);

            ticket.RecordRefundCorrectionValueMovement(
                operation.OperationId,
                result.Outcome,
                result.ValueMovementReference,
                result.Detail,
                _clock);

            return result.Outcome;
        }

        private async Task<RefundValueCorrectionResult> RecoverThenRequestAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancellationToken cancellationToken)
        {
            RefundValueCorrectionRecovery recovery;

            try
            {
                recovery = await _value.RecoverAsync(
                    new RefundValueCorrectionRecoveryRequest(
                        ValueKey(operation, refund),
                        orderId,
                        operation.OperationId,
                        refund.Id),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                return new RefundValueCorrectionResult(ProviderOperationOutcome.Unknown, null, exception.Message);
            }

            return recovery.WasDispatched
                ? new RefundValueCorrectionResult(recovery.Outcome, recovery.ValueMovementReference, recovery.Detail)
                : await RequestAsync(orderId, operation, ticket, refund, cancellationToken);
        }

        private async Task<RefundValueCorrectionResult> RequestAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _value.RequestAsync(
                    new RefundValueCorrectionRequest(
                        ValueKey(operation, refund),
                        orderId,
                        operation.OperationId,
                        refund.Id,
                        refund.OperationId,
                        refund.ValueMovementReference,
                        ticket.DocumentNumber,
                        refund.ApprovedAmount,
                        refund.CurrencyId,
                        refund.ApprovedDisposition,
                        refund.DispositionReference),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                return new RefundValueCorrectionResult(ProviderOperationOutcome.Unknown, null, exception.Message);
            }
        }

        private string ValueKey(OrderOperation operation, DocumentRefundRecord refund)
            => _operations.ProviderOperationKey(operation, $"{ValueStep}:{refund.Id}");
    }
}
