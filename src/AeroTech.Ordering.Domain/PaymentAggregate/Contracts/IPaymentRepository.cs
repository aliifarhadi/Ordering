namespace AeroTech.Ordering.Domain.PaymentAggregate.Contracts
{
    public interface IPaymentRepository
    {
        Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

        Task<Payment?> GetAsync(long id, CancellationToken cancellationToken = default);
    }
}
