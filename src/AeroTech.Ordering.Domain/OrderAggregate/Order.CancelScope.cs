using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public CancelledScope CancelScope(AcceptedScopeCancellationArgs args, IIdGenerator idGenerator, IClock clock)
            => AttachScopeCancellation(StageScopeCancellation(args, idGenerator, clock.GetDateTime()), idGenerator, clock.GetDateTime());

        public IReadOnlyCollection<long> ServiceIdsOfItem(long orderItemId)
            => _orderServices
                .Where(service => service.OrderItemId == orderItemId && service.Status != OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToList();

        public void EnsureScopeCanBeCancelled(OrderChangeType intent, long? orderItemId, IReadOnlyCollection<long> serviceIds)
        {
            if (CommercialSummary is CommercialSummary.Cancelled or CommercialSummary.Closed)
                throw ExceptionFactory.OrderScopeNotCancellable(Id, CommercialSummary);

            if (serviceIds.Count == 0)
                throw ExceptionFactory.CancellationScopeIsEmpty(Id);

            foreach (var serviceId in serviceIds)
            {
                var service = _orderServices.FirstOrDefault(candidate => candidate.Id == serviceId)
                              ?? throw ExceptionFactory.CancellationScopeServiceNotInOrder(serviceId, Id);

                if (service.Status == OrderServiceStatus.Cancelled)
                    throw ExceptionFactory.CancellationScopeServiceAlreadyCancelled(serviceId);
            }

            EnsureNoSurvivingServiceDependsOnScope(serviceIds);

            if (intent == OrderChangeType.Cancel)
                EnsureItemScopeIsComplete(orderItemId, serviceIds);
            else
                EnsureRemovalLeavesTheOrderMeaningful(serviceIds);
        }

        private void EnsureItemScopeIsComplete(long? orderItemId, IReadOnlyCollection<long> serviceIds)
        {
            if (orderItemId is not { } itemId)
                throw ExceptionFactory.ItemCancellationRequiresItem(Id);

            if (_items.All(item => item.Id != itemId))
                throw ExceptionFactory.CancellationScopeItemNotInOrder(itemId, Id);

            var outstanding = ServiceIdsOfItem(itemId).Except(serviceIds).ToList();

            if (outstanding.Count > 0)
                throw ExceptionFactory.ItemCancellationMustCoverTheWholeItem(itemId, outstanding.Count);

            if (serviceIds.Any(serviceId =>
                    _orderServices.Single(service => service.Id == serviceId).OrderItemId != itemId))
                throw ExceptionFactory.CancellationScopeLeavesTheItem(itemId);
        }

        private void EnsureRemovalLeavesTheOrderMeaningful(IReadOnlyCollection<long> serviceIds)
        {
            var surviving = _orderServices
                .Where(service => service.Status != OrderServiceStatus.Cancelled && !serviceIds.Contains(service.Id))
                .ToList();

            if (surviving.Count == 0)
                throw ExceptionFactory.ServiceRemovalCannotEmptyTheOrder(Id);
        }

        private void EnsureNoSurvivingServiceDependsOnScope(IReadOnlyCollection<long> serviceIds)
        {
            foreach (var service in _orderServices.Where(candidate =>
                         candidate.Status != OrderServiceStatus.Cancelled && !serviceIds.Contains(candidate.Id)))
            {
                var covered = service.CoveredServices
                    .Select(coverage => coverage.CoveredOrderServiceId)
                    .FirstOrDefault(serviceIds.Contains);

                if (covered != 0)
                    throw ExceptionFactory.CancellationScopeHasDependentService(covered, service.Id);
            }
        }

        private StagedScopeCancellation StageScopeCancellation(
            AcceptedScopeCancellationArgs args,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(args);

            var accepted = args.Accepted;

            if (args.Intent is not (OrderChangeType.Cancel or OrderChangeType.RemoveService))
                throw ExceptionFactory.CancellationScopeIntentNotSupported(args.Intent);

            var serviceIds = accepted.CancelledOrderServiceIds.Distinct().ToList();

            EnsureScopeCanBeCancelled(args.Intent, args.OrderItemId, serviceIds);

            var changeArgs = new AcceptedPriceChangeArgs(
                args.Intent,
                PriceChangeReason.Cancellation,
                accepted.PricingSource,
                [],
                SourcePricingRef: accepted.SourcePricingReference,
                ChangeReason: accepted.QuotedCancellationId,
                ExternalReference: accepted.QuotedCancellationId,
                ActorScope: args.ActorScope,
                ActorId: args.ActorId,
                OperationId: args.OperationId);

            var change = StageOrderChange(changeArgs, idGenerator, now);

            if (accepted.PricingLines.Count == 0)
                return new StagedScopeCancellation(change, serviceIds, null);

            var lines = accepted.PricingLines.Select(MapCancellationPricingLine).ToList();
            var priceChange = StagePriceChange(change, changeArgs with { Lines = lines }, idGenerator, now);

            return new StagedScopeCancellation(change, serviceIds, priceChange);
        }

        private AcceptedPricingLineArgs MapCancellationPricingLine(AcceptedCancellationPricingLine line)
        {
            if (line.LineRole == PricingLineRole.Reversal && line.ReversesPricingLineId is null)
                throw ExceptionFactory.CancellationReversalRequiresOriginalLine(line.Code ?? "-");

            if (line.ReversesPricingLineId is { } originalId
                && _pricingLines.All(candidate => candidate.Id != originalId))
                throw ExceptionFactory.CancellationReversalTargetNotInOrder(originalId, Id);

            return new AcceptedPricingLineArgs(
                line.ComponentType,
                line.Effect,
                line.Direction,
                line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                line.BasisType,
                line.Refundability,
                OrderItemId: line.OrderItemId,
                Code: line.Code,
                Description: line.Description,
                ExchangeRate: line.ExchangeRate,
                ApplicationLevel: line.ApplicationLevel,
                BasisReferenceId: line.BasisReferenceId,
                SourceLineRef: line.SourceLineRef,
                OccurrenceKey: line.OccurrenceKey,
                OriginalPricingLineId: line.ReversesPricingLineId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);
        }

        private CancelledScope AttachScopeCancellation(
            StagedScopeCancellation staged,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            if (staged.PriceChange is null)
                _changes.Add(staged.Change);

            var changeSet = staged.PriceChange is null ? null : AttachPriceChange(staged.PriceChange, now);

            foreach (var service in _orderServices.Where(service => staged.ServiceIds.Contains(service.Id)))
                service.MarkCancelled();

            RollUpCancelledItems(staged.ServiceIds);

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            var cancelledItemIds = _items
                .Where(item => item.CommercialStatus == OrderItemCommercialStatus.Cancelled)
                .Select(item => item.Id)
                .ToList();

            Causes(new OrderScopeCancelled(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                now,
                Id,
                OwnerAirlineId,
                CustomerId,
                staged.Change.Id,
                staged.Change.ChangeType,
                staged.Change.OperationId,
                CommercialVersion,
                NextEventOrdinal(),
                CommercialSummary,
                Status,
                staged.ServiceIds,
                cancelledItemIds,
                changeSet?.Id,
                CurrencyId,
                CustomerTotal));

            if (changeSet is not null)
                RaisePricingChanged(staged.Change, changeSet, idGenerator, now);

            return new CancelledScope(
                staged.Change.Id,
                staged.Change.ChangeType,
                changeSet?.Id,
                staged.ServiceIds,
                cancelledItemIds,
                changeSet?.FinancialSequence ?? FinancialSequence);
        }

        private sealed record StagedScopeCancellation(
            OrderChange Change,
            IReadOnlyList<long> ServiceIds,
            StagedPriceChange? PriceChange);
    }

    public sealed record CancelledScope(
        long OrderChangeId,
        OrderChangeType Intent,
        long? PriceChangeSetId,
        IReadOnlyList<long> CancelledServiceIds,
        IReadOnlyList<long> CancelledItemIds,
        long FinancialSequence);
}
