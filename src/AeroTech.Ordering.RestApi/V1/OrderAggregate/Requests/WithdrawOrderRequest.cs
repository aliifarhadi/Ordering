using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record WithdrawOrderRequest(VoidReason Reason, int? ExpectedCommercialVersion);
}
