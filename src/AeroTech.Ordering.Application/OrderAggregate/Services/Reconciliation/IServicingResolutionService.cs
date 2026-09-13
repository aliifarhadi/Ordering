namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation
{
    public interface IServicingResolutionService
    {
        Task<ServicingResolutionOutcome> RecordAsync(
            ServicingResolutionExecution execution,
            CancellationToken cancellationToken = default);
    }
}
