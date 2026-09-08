using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Offers;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Creation
{
    public sealed record CreateOrderOutcome(
        long OrderId,
        long ReceiptId,
        int CommercialVersion,
        CommercialSummary CommercialSummary,
        bool IsReplay);

    public interface ICreateOrderService
    {
        Task<CreateOrderOutcome> CreateAsync(
            CreateOrderArgs args,
            OfferDetail offer,
            string idempotencyKey,
            CancellationToken cancellationToken = default);
    }

    public sealed class CreateOrderService : ICreateOrderService
    {
        private readonly IOrderRepository _orders;
        private readonly ICommandReceiptStore _receipts;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;
        private readonly IHomeOperatorProvider _homeOperator;

        public CreateOrderService(
            IOrderRepository orders,
            ICommandReceiptStore receipts,
            IOrderOperationCoordinator operations,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector,
            IHomeOperatorProvider homeOperator)
        {
            _orders = orders;
            _receipts = receipts;
            _operations = operations;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
            _homeOperator = homeOperator;
        }

        public async Task<CreateOrderOutcome> CreateAsync(
            CreateOrderArgs args,
            OfferDetail offer,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            var receipt = await _receipts.AcquireAsync(
                ServicingOperationKind.CreateOrder.ToString(),
                idempotencyKey,
                _operations.Fingerprint(new
                {
                    Operation = "CreateOrder",
                    args.CustomerId,
                    args.AirlineOfficeId,
                    args.OfferId,
                    args.CommissionRate,
                    Travellers = args.Travellers
                        .OrderBy(traveller => traveller.Index)
                        .Select(traveller => new { traveller.Index, traveller.FirstName, traveller.SurName, traveller.PassengerType })
                        .ToArray()
                }),
                cancellationToken);

            if (receipt.IsReplay && receipt.OrderId is { } existingOrderId)
            {
                var existing = await _orders.GetAsync(existingOrderId, cancellationToken)
                               ?? throw ExceptionFactory.OrderNotFound(existingOrderId);

                return new CreateOrderOutcome(
                    existing.Id,
                    receipt.ReceiptId,
                    existing.CommercialVersion,
                    existing.CommercialSummary,
                    IsReplay: true);
            }

            var ownerAirlineId = await _homeOperator.GetOwnerAirlineIdAsync(cancellationToken);
            var order = Order.Create(args, offer, ownerAirlineId, _idGenerator, _clock);

            await _orders.AddAsync(order, cancellationToken);
            await _receipts.AttachOrderAsync(receipt.ReceiptId, order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _projector.ProjectAsync(order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateOrderOutcome(
                order.Id,
                receipt.ReceiptId,
                order.CommercialVersion,
                order.CommercialSummary,
                IsReplay: false);
        }
    }
}
