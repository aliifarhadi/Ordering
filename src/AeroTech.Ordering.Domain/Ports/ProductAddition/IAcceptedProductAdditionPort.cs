using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;

namespace AeroTech.Ordering.Domain.Ports.ProductAddition
{
    public interface IAcceptedProductAdditionPort
    {
        Task<AcceptedProductAddition> GetAcceptedAdditionAsync(
            AcceptedProductAdditionRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record AcceptedProductAdditionRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string SourceReference,
        int SaleCurrencyId);
}
