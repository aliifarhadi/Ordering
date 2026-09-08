using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate
{
    public sealed class FulfillmentPlanner : IFulfillmentPlanner
    {
        private const int ReserveInventorySequence = 1;
        private const int IssueTicketSequence = 2;
        private const int VoidTicketSequence = 3;
        private const int CancelSequence = 4;

        private readonly IIdGenerator _idGenerator;
        private readonly FulfillmentOptions _options;

        public FulfillmentPlanner(IIdGenerator idGenerator, IOptions<FulfillmentOptions> options)
        {
            _idGenerator = idGenerator;
            _options = options.Value;
        }

        public IReadOnlyList<FulfillmentTask> PlanReservation(Order order, string idempotencyKey)
        {
            var airServices = order.OrderServices
                .Where(service => service.RequiresReservation && service.ServiceType == OrderServiceType.AirTransportation)
                .ToList();

            if (airServices.Count == 0)
                return Array.Empty<FulfillmentTask>();

            var task = FulfillmentTask.Create(
                _idGenerator.NewId(),
                order.Id,
                OrderFulfillmentTaskType.ReserveInventory,
                OrderProviderType.Airline,
                idempotencyKey,
                _options.ReserveInventoryMaxAttempts,
                ReserveInventorySequence);

            foreach (var service in airServices)
                task.AddTarget(
                    _idGenerator.NewId(),
                    OrderFulfillmentTargetType.OrderService,
                    OrderFulfillmentTargetAction.Reserve,
                    service.Id,
                    service.OrderItemId);

            return new[] { task };
        }

        public FulfillmentTask PlanIssue(Order order, string holdBatchId, string idempotencyKey)
        {
            var task = FulfillmentTask.Create(
                _idGenerator.NewId(),
                order.Id,
                OrderFulfillmentTaskType.IssueTicket,
                OrderProviderType.Airline,
                idempotencyKey,
                _options.IssueTicketMaxAttempts,
                IssueTicketSequence,
                purpose: OrderFulfillmentPurpose.InitialTicketing);

            var services = order.OrderServices
                .Where(service => service.RequiresReservation
                    && service.ServiceType == OrderServiceType.AirTransportation
                    && service.HoldBatchId == holdBatchId)
                .ToList();

            foreach (var service in services)
                task.AddTarget(
                    _idGenerator.NewId(),
                    OrderFulfillmentTargetType.OrderService,
                    OrderFulfillmentTargetAction.Issue,
                    service.Id,
                    service.OrderItemId,
                    holdBatchId);

            return task;
        }

        public FulfillmentTask PlanVoid(Order order, TrafficDocument document, VoidReason reason, string idempotencyKey)
        {
            var task = FulfillmentTask.Create(
                _idGenerator.NewId(),
                order.Id,
                OrderFulfillmentTaskType.CancelConfirmed,
                OrderProviderType.Airline,
                idempotencyKey,
                _options.VoidTicketMaxAttempts,
                VoidTicketSequence,
                purpose: OrderFulfillmentPurpose.Cancellation,
                cancellationReason: reason);

            foreach (var coupon in document.Coupons)
            {
                var service = order.OrderServices.FirstOrDefault(candidate => candidate.Id == coupon.OrderServiceId);
                task.AddTarget(
                    _idGenerator.NewId(),
                    OrderFulfillmentTargetType.OrderService,
                    OrderFulfillmentTargetAction.Void,
                    coupon.OrderServiceId,
                    null,
                    service?.HoldBatchId,
                    service?.SeatHoldReference);
            }

            return task;
        }

        public FulfillmentTask PlanCancel(Order order, string holdId, string idempotencyKey)
        {
            var airServices = order.OrderServices
                .Where(service => service.RequiresReservation
                    && service.ServiceType == OrderServiceType.AirTransportation
                    && service.HoldBatchId == holdId)
                .ToList();

            var task = FulfillmentTask.Create(
                _idGenerator.NewId(),
                order.Id,
                OrderFulfillmentTaskType.ReleaseReserved,
                OrderProviderType.Airline,
                idempotencyKey,
                _options.ReleaseInventoryMaxAttempts,
                CancelSequence,
                purpose: OrderFulfillmentPurpose.Cancellation);

            foreach (var service in airServices)
                task.AddTarget(
                    _idGenerator.NewId(),
                    OrderFulfillmentTargetType.OrderService,
                    OrderFulfillmentTargetAction.Cancel,
                    service.Id,
                    service.OrderItemId,
                    holdId);

            return task;
        }
    }
}
