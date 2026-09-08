using AeroTech.Ordering.Domain._Shared.Resources;
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

            var movedServices = _orderServices.OfType<OrderAirTransportService>()
                .Where(service => movingIds.Contains(service.TravellerId))
                .ToList();

            var movedSegments = _segments
                .Where(segment => movedServices.Any(service => service.OrderSegmentId == segment.Id))
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
                    segmentMap[service.OrderSegmentId],
                    travellerMap[service.TravellerId],
                    newHoldBatchId));
            }

            var movedPricingLines = _pricingLines
                .Where(line => line.Allocations.Any(allocation =>
                    (allocation.OrderServiceId.HasValue && serviceMap.ContainsKey(allocation.OrderServiceId.Value))
                    || (allocation.OrderItemId.HasValue && itemMap.ContainsKey(allocation.OrderItemId.Value))))
                .ToList();
            var pricingLineMap = new Dictionary<long, long>();
            foreach (var line in movedPricingLines)
            {
                var newLineId = idGenerator.NewId();
                pricingLineMap[newLineId] = line.Id;
                newOrder.AddPricingLine(line.CopyTo(newLineId, newOrder.Id, serviceMap, itemMap, idGenerator));
            }

            _orderServices.RemoveAll(service => serviceMap.ContainsKey(service.Id));
            _pricingLines.RemoveAll(line => movedPricingLines.Contains(line));
            _items.RemoveAll(item => itemMap.ContainsKey(item.Id));
            _travellers.RemoveAll(traveller => movingIds.Contains(traveller.Id));

            RecalculateTotal();
            newOrder.RecalculateTotal();
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

            var newOrderLines = newOrder.BuildPricingLines()
                .Select(line => line with { OriginalLineId = pricingLineMap[line.LineId] })
                .ToList();

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
    }
}
