using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class TicketNumberAllocator : ITicketNumberAllocator
    {
        private readonly ITicketNumberGenerator _generator;
        private readonly ITrafficDocumentRepository _trafficDocumentRepository;
        private readonly TicketNumberOptions _options;

        public TicketNumberAllocator(
            ITicketNumberGenerator generator,
            ITrafficDocumentRepository trafficDocumentRepository,
            IOptions<TicketNumberOptions> options)
        {
            _generator = generator;
            _trafficDocumentRepository = trafficDocumentRepository;
            _options = options.Value;
        }

        public async Task<string> AllocateAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < _options.MaxAllocationAttempts; attempt++)
            {
                var candidate = _generator.Generate();
                if (!await _trafficDocumentRepository.DocumentNumberExistsAsync(candidate, cancellationToken))
                    return candidate;
            }

            throw ExceptionFactory.CouldNotGenerateTicketNumber();
        }
    }
}
