using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public OrderSplitResult SplitOff(
            IReadOnlyCollection<long> travellerIds,
            string newRecordLocator,
            IReadOnlyDictionary<string, string> holdBatchRemap,
            IIdGenerator idGenerator,
            IClock clock)
        {
            EnsureCanSplit(travellerIds);

            var movingIds = travellerIds.ToHashSet();

            var newOrder = new Order(
                idGenerator.NewId(),
                Guid.NewGuid(),
                OwnerAirlineId,
                CustomerId,
                CreatorUserId,
                AirlineOfficeId,
                Channel,
                Type,
                CurrencyId,
                movingIds.Count,
                clock.GetDateTime(),
                TimeToLive);

            newOrder.Status = Status;
            newOrder.RecordLocator = new RecordLocator(newRecordLocator);
            newOrder.LinkedOrderId = Id;
            newOrder.LinkedPNR = RecordLocator?.Value;

            var itineraryMap = new Dictionary<long, long>();
            var segmentMap = new Dictionary<long, long>();
            var travellerMap = new Dictionary<long, long>();
            var itemMap = new Dictionary<long, long>();
            var serviceMap = new Dictionary<long, long>();

            var movedServices = _orderServices
                .Where(service => service.IsAirTransport && movingIds.Contains(service.SoleBeneficiaryId))
                .ToList();

            var movedSegments = _segments
                .Where(segment => movedServices.Any(service => service.SoldSegmentId == segment.Id))
                .ToList();

            var movedItineraryIds = movedSegments.Select(segment => segment.OrderItineraryId).ToHashSet();
            foreach (var itinerary in _itineraries.Where(itinerary => movedItineraryIds.Contains(itinerary.Id)))
            {
                var newItineraryId = idGenerator.NewId();
                itineraryMap[itinerary.Id] = newItineraryId;
                newOrder.AddItinerary(itinerary.CopyTo(newItineraryId, newOrder.Id));
            }

            foreach (var segment in movedSegments)
            {
                var newSegmentId = idGenerator.NewId();
                segmentMap[segment.Id] = newSegmentId;
                newOrder.AddSegment(segment.CopyTo(newSegmentId, newOrder.Id, itineraryMap[segment.OrderItineraryId], idGenerator));
            }

            if (Contact is not null)
                newOrder.SetContact(Contact.CopyTo(idGenerator.NewId(), newOrder.Id, idGenerator));

            var movingTravellers = _travellers.Where(traveller => movingIds.Contains(traveller.Id)).ToList();
            foreach (var traveller in movingTravellers)
                travellerMap[traveller.Id] = idGenerator.NewId();

            foreach (var traveller in movingTravellers)
            {
                long? newParentId = traveller.ParentTravellerId is { } parentId && travellerMap.TryGetValue(parentId, out var mappedParent)
                    ? mappedParent
                    : null;
                newOrder.AddTraveller(traveller.CopyTo(travellerMap[traveller.Id], newOrder.Id, newParentId, idGenerator));
            }

            var movedItems = _items
                .Where(item => movedServices.Any(service => service.OrderItemId == item.Id))
                .ToList();
            foreach (var item in movedItems)
            {
                var newItemId = idGenerator.NewId();
                itemMap[item.Id] = newItemId;
                newOrder.AddItem(item.CopyTo(newItemId, newOrder.Id, idGenerator));
            }

            foreach (var service in movedServices)
            {
                var newServiceId = idGenerator.NewId();
                serviceMap[service.Id] = newServiceId;

                var newHoldBatchId = service.HoldBatchId is { } holdBatchId && holdBatchRemap.TryGetValue(holdBatchId, out var remapped)
                    ? remapped
                    : service.HoldBatchId;

                newOrder.AddOrderService(service.CopyTo(
                    newServiceId,
                    newOrder.Id,
                    itemMap[service.OrderItemId],
                    segmentMap,
                    travellerMap,
                    newHoldBatchId,
                    idGenerator.NewId));
            }

            var movedPricingLines = _pricingLines
                .Where(line => MovesWith(line, serviceMap, itemMap))
                .ToList();

            var transferGroupId = idGenerator.NewId().ToString();

            var transferLines = movedPricingLines
                .Select(line => TransferLine(line, newOrder.Id, transferGroupId, serviceMap, itemMap, travellerMap, segmentMap, itineraryMap))
                .ToList();

            _orderServices.RemoveAll(service => serviceMap.ContainsKey(service.Id));
            _pricingLines.RemoveAll(line => movedPricingLines.Contains(line));
            _items.RemoveAll(item => itemMap.ContainsKey(item.Id));
            _travellers.RemoveAll(traveller => movingIds.Contains(traveller.Id));

            if (transferLines.Count > 0)
                newOrder.CommitPriceChange(
                    new AcceptedPriceChangeArgs(
                        OrderChangeType.Split,
                        PriceChangeReason.SplitTransfer,
                        PricingSource.PricingEngine,
                        transferLines),
                    idGenerator,
                    clock);

            RecomputeAmountCache();
            Pax = _travellers.Count;

            if (PaymentSummary is not null)
            {
                var settledAt = clock.GetDateTime();

                newOrder.PaymentSummary = new OrderPaymentSummary(
                    PaymentSummary.PaymentId,
                    PaymentSummary.Status,
                    newOrder.Amount.GrandTotal,
                    PaymentSummary.ProviderReference,
                    PaymentSummary.FormOfPayment,
                    settledAt);

                PaymentSummary = new OrderPaymentSummary(
                    PaymentSummary.PaymentId,
                    PaymentSummary.Status,
                    Amount.GrandTotal,
                    PaymentSummary.ProviderReference,
                    PaymentSummary.FormOfPayment,
                    settledAt);
            }

            var splitAt = clock.GetDateTime();

            var newOrderLines = newOrder.BuildPricingLines();

            IncrementCommercialVersion();

            Causes(new OrderSplit(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                splitAt,
                Id,
                AirlineOfficeId,
                RecordLocator?.Value,
                UniqueIdentifierId,
                CommercialVersion,
                NextEventOrdinal(),
                Status,
                Type,
                Channel,
                CustomerId,
                splitAt,
                movingIds.ToList(),
                Status == OrderStatus.Ticketed,
                Amount.GrandTotal,
                CurrencyId,
                BuildPricingLines(),
                new SplitNewOrderSnapshot(
                    newOrder.Id,
                    newOrder.RecordLocator?.Value,
                    newOrder.UniqueIdentifierId,
                    newOrder.CommercialVersion,
                    newOrder.Amount.GrandTotal,
                    newOrderLines)));

            return new OrderSplitResult(newOrder, serviceMap, travellerMap);
        }

        public void EnsureCanSplit(IReadOnlyCollection<long> travellerIds)
        {
            if (Status is not (OrderStatus.Confirmed or OrderStatus.Ticketed))
                throw ExceptionFactory.OrderCannotBeSplit(Id, Status);

            if (travellerIds.Count == 0)
                throw ExceptionFactory.AtLeastOneTravellerMustBeSelectedToSplit();

            var movingIds = travellerIds.ToHashSet();

            var moving = _travellers.Where(traveller => movingIds.Contains(traveller.Id)).ToList();
            if (moving.Count != movingIds.Count)
                throw ExceptionFactory.SelectedTravellersDoNotBelongToOrder();

            if (moving.Count >= _travellers.Count)
                throw ExceptionFactory.CannotSplitOffAllTravellers();

            foreach (var traveller in _travellers)
                if (traveller.ParentTravellerId is { } parentId && movingIds.Contains(traveller.Id) != movingIds.Contains(parentId))
                    throw ExceptionFactory.InfantAndParentMustBeSplitTogether();
        }

        private static bool MovesWith(
            OrderPricingLine line,
            IReadOnlyDictionary<long, long> serviceMap,
            IReadOnlyDictionary<long, long> itemMap)
        {
            if (line.CommercialAllocations().Any(allocation =>
                    (allocation.OrderServiceId is { } serviceId && serviceMap.ContainsKey(serviceId))
                    || (allocation.OrderItemIdAtAllocation is { } allocatedItemId && itemMap.ContainsKey(allocatedItemId))))
                return true;

            if (line.BasisType == PricingBasisType.OrderService && line.BasisReferenceId is { } basisServiceId)
                return serviceMap.ContainsKey(basisServiceId);

            if (line.BasisType == PricingBasisType.OrderItem && line.BasisReferenceId is { } basisItemId)
                return itemMap.ContainsKey(basisItemId);

            return line.OrderItemId is { } ownerItemId && itemMap.ContainsKey(ownerItemId);
        }

        private static AcceptedPricingLineArgs TransferLine(
            OrderPricingLine line,
            long newOrderId,
            string transferGroupId,
            IReadOnlyDictionary<long, long> serviceMap,
            IReadOnlyDictionary<long, long> itemMap,
            IReadOnlyDictionary<long, long> travellerMap,
            IReadOnlyDictionary<long, long> segmentMap,
            IReadOnlyDictionary<long, long> itineraryMap)
            => new(
                line.ComponentType,
                line.Effect,
                line.Direction,
                PricingLineRole.Transfer,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                line.BasisType,
                line.Refundability,
                OrderItemId: Mapped(line.OrderItemId, itemMap),
                Code: line.Code,
                Description: line.Description,
                ExchangeRate: line.ExchangeRate?.Copy(),
                ApplicationLevel: line.ApplicationLevel,
                Quantity: line.Quantity,
                UnitOfMeasure: line.UnitOfMeasure,
                UnitPrice: line.UnitPrice,
                BasisReferenceId: TransferredBasisReference(line, newOrderId, serviceMap, itemMap),
                SourceLineRef: line.SourceLineRef,
                OriginalPricingLineId: line.Id,
                TransferGroupId: transferGroupId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory,
                AllocationSets: TransferredAllocationSets(line, newOrderId, serviceMap, itemMap, travellerMap, segmentMap, itineraryMap));

        private static long? TransferredBasisReference(
            OrderPricingLine line,
            long newOrderId,
            IReadOnlyDictionary<long, long> serviceMap,
            IReadOnlyDictionary<long, long> itemMap)
            => line.BasisType switch
            {
                PricingBasisType.Order => newOrderId,
                PricingBasisType.OrderService => Mapped(line.BasisReferenceId, serviceMap),
                PricingBasisType.OrderItem => Mapped(line.BasisReferenceId, itemMap),
                _ => null
            };

        private static IReadOnlyList<AcceptedPricingAllocationSetArgs>? TransferredAllocationSets(
            OrderPricingLine line,
            long newOrderId,
            IReadOnlyDictionary<long, long> serviceMap,
            IReadOnlyDictionary<long, long> itemMap,
            IReadOnlyDictionary<long, long> travellerMap,
            IReadOnlyDictionary<long, long> segmentMap,
            IReadOnlyDictionary<long, long> itineraryMap)
        {
            var source = line.ActiveAllocationSet(PricingAllocationPurpose.CommercialValue);

            if (source is null || source.Allocations.Count == 0)
                return null;

            var moved = source.Allocations
                .Where(allocation => allocation.OrderServiceId is not { } serviceId || serviceMap.ContainsKey(serviceId))
                .Select(allocation => new AcceptedPricingAllocationArgs(
                    allocation.SaleAmount,
                    allocation.SaleCurrencyId,
                    Mapped(allocation.OrderItemIdAtAllocation, itemMap),
                    Mapped(allocation.OrderServiceId, serviceMap),
                    Mapped(allocation.TravellerId, travellerMap),
                    Mapped(allocation.ItineraryIdAtAllocation, itineraryMap),
                    Mapped(allocation.SegmentIdAtAllocation, segmentMap),
                    allocation.CoveragePortionRef,
                    allocation.OriginalAmount,
                    allocation.OriginalCurrencyId,
                    allocation.ExchangeRate?.Copy(),
                    allocation.Id))
                .ToList();

            if (moved.Count == 0)
                return null;

            var completeness = moved.Sum(allocation => allocation.SaleAmount) == line.SaleAmount
                ? PricingAllocationCompleteness.Complete
                : PricingAllocationCompleteness.Partial;

            return
            [
                new AcceptedPricingAllocationSetArgs(
                    PricingAllocationPurpose.CommercialValue,
                    source.Source,
                    source.Method,
                    completeness,
                    moved,
                    source.PricingContextRef,
                    source.PolicyVersion)
            ];
        }

        private static long? Mapped(long? id, IReadOnlyDictionary<long, long> map)
            => id is { } value && map.TryGetValue(value, out var mapped) ? mapped : null;
    }
}
