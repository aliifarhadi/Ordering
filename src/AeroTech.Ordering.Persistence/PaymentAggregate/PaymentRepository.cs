using AeroTech.Ordering.Domain.PaymentAggregate;
using AeroTech.Ordering.Domain.PaymentAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.PaymentAggregate
{
    public sealed class PaymentRepository : IPaymentRepository
    {
        private readonly OrderingDbContext _dbContext;

        public PaymentRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
            => await _dbContext.Set<Payment>().AddAsync(payment, cancellationToken);

        public Task<Payment?> GetAsync(long id, CancellationToken cancellationToken = default)
            => _dbContext.Set<Payment>().FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);
    }
}
