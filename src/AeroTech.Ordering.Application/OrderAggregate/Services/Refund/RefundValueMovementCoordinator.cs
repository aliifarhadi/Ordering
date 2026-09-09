using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Ports.RefundValue;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed class RefundValueMovementCoordinator : IRefundValueMovementCoordinator
    {
        public const string ValueStep = "refund-value";

        private readonly IRefundValuePort _value;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IClock _clock;

        public RefundValueMovementCoordinator(
            IRefundValuePort value,
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
            AcceptedRefund accepted,
            RefundValueDispatch dispatch,
            CancellationToken cancellationToken = default)
        {
            var result = dispatch == RefundValueDispatch.FirstAttempt
                ? await RequestAsync(orderId, operation, ticket, accepted, cancellationToken)
                : await RecoverThenRequestAsync(orderId, operation, ticket, accepted, cancellationToken);

            ticket.RecordRefundValueMovement(
                operation.OperationId,
                result.Outcome,
                result.ValueMovementReference,
                result.Detail,
                _clock);

            return result.Outcome;
        }

        private async Task<RefundValueResult> RecoverThenRequestAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedRefund accepted,
            CancellationToken cancellationToken)
        {
            RefundValueRecovery recovery;

            try
            {
                recovery = await _value.RecoverAsync(
                    new RefundValueRecoveryRequest(
                        ValueKey(operation, ticket),
                        orderId,
                        operation.OperationId),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                return new RefundValueResult(ProviderOperationOutcome.Unknown, null, exception.Message);
            }

            return recovery.WasDispatched
                ? new RefundValueResult(recovery.Outcome, recovery.ValueMovementReference, recovery.Detail)
                : await RequestAsync(orderId, operation, ticket, accepted, cancellationToken);
        }

        private async Task<RefundValueResult> RequestAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedRefund accepted,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _value.RequestAsync(
                    new RefundValueRequest(
                        ValueKey(operation, ticket),
                        orderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        accepted.ApprovedRefundAmount,
                        accepted.SaleCurrencyId,
                        accepted.ApprovedDisposition,
                        accepted.DispositionReference),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                return new RefundValueResult(ProviderOperationOutcome.Unknown, null, exception.Message);
            }
        }

        private string ValueKey(OrderOperation operation, ElectronicTicket ticket)
            => _operations.ProviderOperationKey(operation, $"{ValueStep}:{ticket.Id}");
    }
}
