using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Query._Shared.DbContexts;
using MassTransit;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderCreated;

namespace AeroTech.Ordering.Consumers.Ordering.OrderAggregate.WhenOrderCreated
{
    public sealed class SyncQueryDbWhenOrderCreated : IConsumer<IntegrationEvent>
    {
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly OrderQueryDbContext _queryDbContext;

        public SyncQueryDbWhenOrderCreated(IOrderQueryDbSynchronizer synchronizer, OrderQueryDbContext queryDbContext)
        {
            _synchronizer = synchronizer;
            _queryDbContext = queryDbContext;
        }

        public async Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            var @event = context.Message;

            await _synchronizer.ProjectCreatedAsync(new OrderReadModelSnapshot(
                @event.OrderId,
                @event.UniqueIdentifierId,
                null,
                @event.Status,
                @event.Type,
                @event.Channel,
                @event.CustomerId,
                @event.AirlineOfficeId,
                @event.CreatorUserId,
                @event.CurrencyId,
                @event.Pax,
                @event.GrandTotal,
                @event.TotalTax,
                @event.CommissionAmount,
                @event.CommissionRate,
                @event.CommercialVersion,
                @event.LinkedOrderId,
                @event.LinkedPNR,
                @event.TimeToLive,
                @event.CreationDate,
                @event.TimeOfOccurrence), context.CancellationToken);

            await _queryDbContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
