namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public interface IPricingProvider
    {
        Task<AirFareBoundReservationValidationResult> ReservationValidationAsync(
            AirFareBoundReservationValidationRequest request,
            CancellationToken cancellationToken = default);
    }
}
