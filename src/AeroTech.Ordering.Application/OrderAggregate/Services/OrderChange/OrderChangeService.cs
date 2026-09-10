using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange
{
    public sealed class OrderChangeService : IOrderChangeService
    {
        public const string ProviderStep = "accept-quoted-offer";

        private readonly IOrderRepository _orders;
        private readonly IOrderChangeQuoteProvider _quotes;
        private readonly IOrderOperationCoordinator _operations;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public OrderChangeService(
            IOrderRepository orders,
            IOrderChangeQuoteProvider quotes,
            IOrderOperationCoordinator operations,
            ICallerContext callerContext,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _quotes = quotes;
            _operations = operations;
            _callerContext = callerContext;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<OrderChangeOutcome> AddServiceAsync(
            long orderId,
            IReadOnlyList<SelectedQuotedOffer> acceptSelectedQuotedOfferList,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            var selection = RequireSingleSelection(acceptSelectedQuotedOfferList);

            if (expectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(orderId);

            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.AddService,
                idempotencyKey,
                new
                {
                    Operation = "OrderChange",
                    Subtype = "AddService",
                    OrderId = orderId,
                    selection.QuotedOfferId,
                    SelectedOfferItemIds = selection.SelectedOfferItemIds.Order().ToArray(),
                    ExpectedCommercialVersion = expectedCommercialVersion
                },
                expectedCommercialVersion,
                cancellationToken);

            var committed = CommittedChange(order, operation.OperationId);

            if (committed is not null)
                return await ReplayAsync(order, operation, committed, cancellationToken);

            AddedProduct added;

            try
            {
                if (expectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        expectedCommercialVersion,
                        orderId,
                        order.CommercialVersion);

                var accepted = await _quotes.AcceptSelectedQuotedOfferAsync(
                    new AcceptedQuotedOfferSelection(
                        _operations.ProviderOperationKey(operation, ProviderStep),
                        orderId,
                        operation.OperationId,
                        selection.QuotedOfferId,
                        selection.SelectedOfferItemIds.Single(),
                        order.CurrencyId),
                    cancellationToken);

                added = order.AddProduct(
                    new AcceptedAddServiceChangeArgs(
                        accepted,
                        operation.OperationId,
                        _callerContext.ActorId,
                        CallerScope.For(_callerContext)),
                    _idGenerator,
                    _clock);
            }
            catch
            {
                await TryReleaseRejectedAsync(orderId, operation, cancellationToken);
                throw;
            }

            await _operations.ResolveAsync(orderId, operation, cancellationToken);
            await _projector.ProjectAsync(orderId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new OrderChangeOutcome(
                orderId,
                operation.OperationId,
                added.OrderChangeId,
                added.PriceChangeSetId,
                added.OrderItemId,
                added.OrderServiceIds,
                added.FinancialSequence,
                order.CommercialVersion,
                order.ObligationVersion,
                order.CustomerTotal,
                order.CurrencyId,
                IsReplay: false);
        }

        private static SelectedQuotedOffer RequireSingleSelection(
            IReadOnlyList<SelectedQuotedOffer> acceptSelectedQuotedOfferList)
        {
            ArgumentNullException.ThrowIfNull(acceptSelectedQuotedOfferList);

            if (acceptSelectedQuotedOfferList.Count != 1)
                throw ExceptionFactory.OrderChangeAcceptsOneOfferItem(acceptSelectedQuotedOfferList.Count);

            var selection = acceptSelectedQuotedOfferList[0];

            ArgumentException.ThrowIfNullOrWhiteSpace(selection.QuotedOfferId);

            if (selection.SelectedOfferItemIds is not { Count: 1 })
                throw ExceptionFactory.OrderChangeAcceptsOneOfferItem(selection.SelectedOfferItemIds?.Count ?? 0);

            ArgumentException.ThrowIfNullOrWhiteSpace(selection.SelectedOfferItemIds[0]);

            return selection;
        }

        private static Entities.OrderChange? CommittedChange(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.AddProduct);

        private async Task<OrderChangeOutcome> ReplayAsync(
            Order order,
            OrderOperation operation,
            Entities.OrderChange committed,
            CancellationToken cancellationToken)
        {
            var links = order.ItemServiceLinks
                .Where(link => link.LinkedByChangeId == committed.Id)
                .ToList();

            var changeSet = order.PriceChangeSets.Single(set => set.ChangeId == committed.Id);

            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new OrderChangeOutcome(
                order.Id,
                operation.OperationId,
                committed.Id,
                changeSet.Id,
                links.Select(link => link.OrderItemId).Distinct().Single(),
                links.Select(link => link.OrderServiceId).ToList(),
                changeSet.FinancialSequence,
                order.CommercialVersion,
                order.ObligationVersion,
                order.CustomerTotal,
                order.CurrencyId,
                IsReplay: true);
        }

        private async Task TryReleaseRejectedAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken)
        {
            try
            {
                await _operations.ResolveAsync(orderId, operation, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
            }
        }
    }
}
