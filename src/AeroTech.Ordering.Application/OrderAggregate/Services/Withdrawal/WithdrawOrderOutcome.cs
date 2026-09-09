using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal
{
    public sealed record WithdrawOrderOutcome(
        long OrderId,
        long OperationId,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<long> WithdrawnServiceIds,
        ProviderOperationOutcome ReservationReleaseOutcome,
        ProviderOperationOutcome FundingReleaseOutcome);
}
