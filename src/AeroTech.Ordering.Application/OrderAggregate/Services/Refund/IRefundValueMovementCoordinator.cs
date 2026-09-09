using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public interface IRefundValueMovementCoordinator
    {
        Task<ProviderOperationOutcome> SettleAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedRefund accepted,
            RefundValueDispatch dispatch,
            CancellationToken cancellationToken = default);
    }
}
