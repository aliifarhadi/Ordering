using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed class RecordLocatorAllocator : IRecordLocatorAllocator
    {
        private readonly IRecordLocatorGenerator _recordLocatorGenerator;
        private readonly IOrderRepository _orderRepository;
        private readonly RecordLocatorOptions _options;

        public RecordLocatorAllocator(
            IRecordLocatorGenerator recordLocatorGenerator,
            IOrderRepository orderRepository,
            IOptions<RecordLocatorOptions> options)
        {
            _recordLocatorGenerator = recordLocatorGenerator;
            _orderRepository = orderRepository;
            _options = options.Value;
        }

        public async Task<string> AllocateAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < _options.MaxAllocationAttempts; attempt++)
            {
                var candidate = _recordLocatorGenerator.Generate();
                if (!await _orderRepository.RecordLocatorExistsAsync(candidate, cancellationToken))
                    return candidate;
            }

            throw ExceptionFactory.CouldNotGenerateRecordLocator();
        }
    }
}
