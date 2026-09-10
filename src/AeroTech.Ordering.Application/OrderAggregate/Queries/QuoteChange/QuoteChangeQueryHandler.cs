using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Queries.QuoteChange
{
    public sealed class QuoteChangeQueryHandler : IRequestHandler<QuoteChangeQuery, ChangeQuoteOutcome>
    {
        private readonly IVoluntaryChangeService _voluntaryChangeService;

        public QuoteChangeQueryHandler(IVoluntaryChangeService voluntaryChangeService) => _voluntaryChangeService = voluntaryChangeService;

        public Task<ChangeQuoteOutcome> Handle(QuoteChangeQuery query, CancellationToken cancellationToken)
            => _voluntaryChangeService.QuoteAsync(query.OrderId, query.OrderServiceId, cancellationToken);
    }
}
