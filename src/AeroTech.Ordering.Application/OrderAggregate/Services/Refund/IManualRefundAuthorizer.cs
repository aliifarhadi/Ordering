using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public interface IManualRefundAuthorizer
    {
        Task AuthorizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ManualRefundAuthority authority,
            decimal approvedRefundAmount,
            CancellationToken cancellationToken = default);
    }
}
