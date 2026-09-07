using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public interface IOrderIssuanceService
    {
        Task<FulfillmentOutcome> IssueAsync(long orderId, CancellationToken cancellationToken = default);

        Task<OrderStatus> FinalizeIssuanceAsync(long orderId, CancellationToken cancellationToken = default);

        Task<OrderStatus> FailIssuanceAsync(long orderId, string reason, CancellationToken cancellationToken = default);
    }
}
