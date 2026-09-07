using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class TicketNumberGenerator : ITicketNumberGenerator
    {
        private readonly TicketNumberOptions _options;

        public TicketNumberGenerator(IOptions<TicketNumberOptions> options) => _options = options.Value;

        public string Generate()
        {
            var serialLength = Math.Max(1, _options.SerialLength);
            var serial = new char[serialLength];

            for (var index = 0; index < serialLength; index++)
                serial[index] = (char)('0' + Random.Shared.Next(10));

            return $"{_options.Prefix}{new string(serial)}";
        }
    }
}
