namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public interface IAncillaryExchangeDispositionPort
    {
        Task<AncillaryExchangeDispositionResult> DecideAsync(
            AncillaryExchangeDispositionRequest request,
            CancellationToken cancellationToken = default);
    }
}
