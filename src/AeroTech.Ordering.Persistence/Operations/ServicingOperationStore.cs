using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class ServicingOperationStore : IServicingOperationStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IHomeOperatorProvider _homeOperatorProvider;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ServicingOperationStore(
            OrderingDbContext dbContext,
            IHomeOperatorProvider homeOperatorProvider,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _dbContext = dbContext;
            _homeOperatorProvider = homeOperatorProvider;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<ServicingOperationRecord> PrepareAsync(
            long orderId,
            ServicingOperationKind kind,
            string requestHash,
            long claimGeneration,
            long? commandReceiptId = null,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

            var ownerAirlineId = await _homeOperatorProvider.GetOwnerAirlineIdAsync(cancellationToken);
            var now = _clock.GetDateTime();

            var operation = new ServicingOperation
            {
                Id = _idGenerator.NewId(),
                OwnerAirlineId = ownerAirlineId,
                OrderId = orderId,
                CommandReceiptId = commandReceiptId,
                Kind = kind,
                Status = ServicingOperationStatus.Prepared,
                RequestHash = requestHash,
                ExpectedCommercialVersion = expectedCommercialVersion,
                ClaimGeneration = claimGeneration,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Set<ServicingOperation>().Add(operation);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ServicingOperationRecord(
                operation.Id,
                operation.OwnerAirlineId,
                operation.OrderId,
                operation.Kind,
                operation.Status,
                operation.ClaimGeneration);
        }
    }
}
