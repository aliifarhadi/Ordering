namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public interface IFlightFlowProvider
    {
        Task<FlightHeldSeatsResult> CreateHoldAsync(HoldSeatsRequest request, CancellationToken cancellationToken = default);

        Task ConfirmHoldAsync(ConfirmHoldRequest request, CancellationToken cancellationToken = default);

        Task<ReleaseHeldSeatsResult> ReleaseHeldAsync(ReleaseHeldSeatsRequest request, CancellationToken cancellationToken = default);

        Task<CancelConfirmedSeatsResult> CancelConfirmedAsync(CancelConfirmedSeatsRequest request, CancellationToken cancellationToken = default);

        Task<SplitHeldSeatsResult> SplitHeldAsync(SplitHeldSeatsRequest request, CancellationToken cancellationToken = default);

        Task ExtendHeldAsync(ExtendHeldSeatsRequest request, CancellationToken cancellationToken = default);
    }
}
