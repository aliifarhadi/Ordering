using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.ProductAddition;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.ProductAddition
{
    public sealed record AddProductOutcome(
        long OrderId,
        long OperationId,
        long OrderChangeId,
        long PriceChangeSetId,
        long OrderItemId,
        IReadOnlyList<long> ServiceIds,
        long FinancialSequence,
        int CommercialVersion,
        long ObligationVersion,
        decimal CustomerTotal,
        int CurrencyId,
        bool IsReplay);

    public interface IAddProductService
    {
        Task<AddProductOutcome> AddProductAsync(
            long orderId,
            string sourceReference,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }

    public sealed class AddProductService : IAddProductService
    {
        public const string ProviderStep = "add-product";

        private readonly IOrderRepository _orders;
        private readonly IAcceptedProductAdditionPort _additions;
        private readonly IOrderOperationCoordinator _operations;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public AddProductService(
            IOrderRepository orders,
            IAcceptedProductAdditionPort additions,
            IOrderOperationCoordinator operations,
            ICallerContext callerContext,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _additions = additions;
            _operations = operations;
            _callerContext = callerContext;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<AddProductOutcome> AddProductAsync(
            long orderId,
            string sourceReference,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);

            if (expectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(orderId);

            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.AddProduct,
                idempotencyKey,
                new
                {
                    Operation = "AddProduct",
                    OrderId = orderId,
                    SourceReference = sourceReference,
                    ExpectedCommercialVersion = expectedCommercialVersion
                },
                expectedCommercialVersion,
                cancellationToken);

            var committed = CommittedAddition(order, operation.OperationId);

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

                var accepted = await _additions.GetAcceptedAdditionAsync(
                    new AcceptedProductAdditionRequest(
                        _operations.ProviderOperationKey(operation, ProviderStep),
                        orderId,
                        operation.OperationId,
                        sourceReference,
                        order.CurrencyId),
                    cancellationToken);

                added = order.AddProduct(
                    new AcceptedProductAdditionArgs(
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

            return new AddProductOutcome(
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

        private static OrderChange? CommittedAddition(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.AddProduct);

        private async Task<AddProductOutcome> ReplayAsync(
            Order order,
            OrderOperation operation,
            OrderChange committed,
            CancellationToken cancellationToken)
        {
            var links = order.ItemServiceLinks
                .Where(link => link.LinkedByChangeId == committed.Id)
                .ToList();

            var changeSet = order.PriceChangeSets.Single(set => set.ChangeId == committed.Id);

            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AddProductOutcome(
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
