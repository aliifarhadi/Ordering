namespace AeroTech.Ordering.Domain.Providers.Pricing
{
    public interface IPricingProvider
    {
        Task<AirFareBoundReservationValidationResult> ReservationValidationAsync(
            AirFareBoundReservationValidationRequest request,
            CancellationToken cancellationToken = default);
    }
}
