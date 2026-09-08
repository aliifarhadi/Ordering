using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Split
{
    public sealed class OrderSplitService : IOrderSplitService
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyHoldBatchRemap = new Dictionary<string, string>();

        private readonly IOrderRepository _orderRepository;
        private readonly IFlightFlowProvider _flightFlowProvider;
        private readonly IRecordLocatorAllocator _recordLocatorAllocator;
        private readonly ITrafficDocumentRepository _trafficDocumentRepository;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDistributedLock _distributedLock;
        private readonly FulfillmentOptions _fulfillmentOptions;

        public OrderSplitService(
            IOrderRepository orderRepository,
            IFlightFlowProvider flightFlowProvider,
            IRecordLocatorAllocator recordLocatorAllocator,
            ITrafficDocumentRepository trafficDocumentRepository,
            IOrderQueryDbSynchronizer synchronizer,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork,
            IDistributedLock distributedLock,
            IOptions<FulfillmentOptions> fulfillmentOptions)
        {
            _orderRepository = orderRepository;
            _flightFlowProvider = flightFlowProvider;
            _recordLocatorAllocator = recordLocatorAllocator;
            _trafficDocumentRepository = trafficDocumentRepository;
            _synchronizer = synchronizer;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
            _distributedLock = distributedLock;
            _fulfillmentOptions = fulfillmentOptions.Value;
        }

        public async Task<SplitOrderResult> SplitAsync(long sourceOrderId, IReadOnlyCollection<long> travellerIds, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-split:{sourceOrderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            if (lockHandle is null)
                throw ExceptionFactory.OrderOperationInProgress(sourceOrderId);

            var order = await _orderRepository.GetAsync(sourceOrderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(sourceOrderId);

            order.EnsureCanSplit(travellerIds);

            var idempotencyKey = $"split:{sourceOrderId}";

            var now = _clock.GetDateTime();
            var newRecordLocator = await _recordLocatorAllocator.AllocateAsync(cancellationToken);

            var newOrder = order.Status == OrderStatus.Ticketed
                ? await SplitTicketedAsync(order, travellerIds, newRecordLocator, now, cancellationToken)
                : await SplitUnticketedAsync(order, travellerIds, newRecordLocator, idempotencyKey, now, cancellationToken);

            await _synchronizer.ProjectSplitAsync(order.ToReadModelSnapshot(now), cancellationToken);
            await _synchronizer.ProjectSplitAsync(newOrder.ToReadModelSnapshot(now), cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SplitOrderResult(order.Id, newOrder.Id, order.Status, newOrder.Status);
        }

        private async Task<Order> SplitUnticketedAsync(
            Order order,
            IReadOnlyCollection<long> travellerIds,
            string newRecordLocator,
            string idempotencyKey,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var holdBatchRemap = await SplitProviderHoldsAsync(order, travellerIds, newRecordLocator, idempotencyKey, now, cancellationToken);

            var newOrder = order.SplitOff(travellerIds, newRecordLocator, holdBatchRemap, _idGenerator, _clock).NewOrder;
            await _orderRepository.AddAsync(newOrder, cancellationToken);

            return newOrder;
        }

        private async Task<Order> SplitTicketedAsync(
            Order order,
            IReadOnlyCollection<long> travellerIds,
            string newRecordLocator,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var documents = await _trafficDocumentRepository.GetByOrderAndTravellersAsync(order.Id, travellerIds, cancellationToken);

            foreach (var document in documents)
                document.EnsureCanBeReassigned();

            var split = order.SplitOff(travellerIds, newRecordLocator, EmptyHoldBatchRemap, _idGenerator, _clock);
            await _orderRepository.AddAsync(split.NewOrder, cancellationToken);

            foreach (var document in documents)
                document.ReassignToOrder(split.NewOrder.Id, split.TravellerMap[document.TravellerId], split.ServiceMap, _idGenerator, now);

            return split.NewOrder;
        }

        private async Task<IReadOnlyDictionary<string, string>> SplitProviderHoldsAsync(
            Order order,
            IReadOnlyCollection<long> travellerIds,
            string reference,
            string idempotencyKey,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var movedBatches = order.OrderServices
                .Where(service => service.IsAirTransport
                    && travellerIds.Contains(service.SoleBeneficiaryId)
                    && !string.IsNullOrWhiteSpace(service.HoldBatchId)
                    && !string.IsNullOrWhiteSpace(service.SeatHoldReference))
                .GroupBy(service => service.HoldBatchId!)
                .ToList();

            var holdBatchRemap = new Dictionary<string, string>();

            foreach (var batch in movedBatches)
            {
                var seatHoldReferences = batch.Select(service => service.SeatHoldReference!).Distinct().ToList();

                var request = new SplitHeldSeatsRequest(
                    batch.Key,
                    seatHoldReferences,
                    $"{idempotencyKey}:{batch.Key}",
                    reference,
                    order.TimeToLive ?? now);

                try
                {
                    var result = await _flightFlowProvider.SplitHeldAsync(request, cancellationToken);
                    holdBatchRemap[batch.Key] = result.NewHoldBatchId;
                }
                catch (ProviderRequestException exception)
                {
                    throw ExceptionFactory.ProviderRequestFailed(exception.Message);
                }
            }

            return holdBatchRemap;
        }
    }
}
