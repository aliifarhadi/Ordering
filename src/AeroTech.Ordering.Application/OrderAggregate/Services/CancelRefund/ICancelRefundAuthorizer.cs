using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public interface ICancelRefundAuthorizer
    {
        Task AuthorizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancelRefundExecution execution,
            CancellationToken cancellationToken = default);
    }
}
