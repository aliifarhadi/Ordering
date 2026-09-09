using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public interface IRefundValueCorrectionCoordinator
    {
        Task<ProviderOperationOutcome> SettleAsync(
            long orderId,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            RefundValueDispatch dispatch,
            CancellationToken cancellationToken = default);
    }
}
