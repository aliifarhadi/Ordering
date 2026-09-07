using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed class ReservationApplier : IReservationApplier
    {
        private readonly IRecordLocatorAllocator _recordLocatorAllocator;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ReservationApplier(
            IRecordLocatorAllocator recordLocatorAllocator,
            IOrderQueryDbSynchronizer synchronizer,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _recordLocatorAllocator = recordLocatorAllocator;
            _synchronizer = synchronizer;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<string> CompleteAsync(Order order, FulfillmentTask reserveTask, CancellationToken cancellationToken = default)
        {
            var recordLocator = await _recordLocatorAllocator.AllocateAsync(cancellationToken);

            var serviceLinks = reserveTask.Targets
                .Where(target => target.OrderServiceId.HasValue && target.Status == OrderFulfillmentStatus.Confirmed)
                .Select(target => new ReservedServiceLink(target.OrderServiceId!.Value, target.FulfillmentReference, target.ServiceReference))
                .ToList();

            order.CompleteReserve(recordLocator, reserveTask.ExpiresAt, serviceLinks, _idGenerator, _clock);

            await _synchronizer.ProjectReservedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);

            return recordLocator;
        }

        public async Task FailAsync(Order order, string reason, CancellationToken cancellationToken = default)
        {
            order.FailReservation(reason, _idGenerator, _clock);

            await _synchronizer.ProjectReserveFailedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
        }

        public async Task MarkUnconfirmedAsync(Order order, FulfillmentTask reserveTask, FulfillmentFailureReason reason, string detail, CancellationToken cancellationToken = default)
        {
            reserveTask.RequireManualAction(_clock.GetDateTime(), detail);

            order.MarkReservationUnconfirmed(reason, detail, _idGenerator, _clock);

            await _synchronizer.ProjectReservationUnconfirmedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
        }
    }
}
