using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        private static readonly ProductType[] BlockedProductTypes =
        [
            ProductType.Penalty,
            ProductType.ServiceFee,
            ProductType.Credit,
            ProductType.Voucher,
            ProductType.TaxAdjustment,
            ProductType.ManualAdjustment
        ];

        private static readonly PricingBasisType[] AdditionBasisTypes =
        [
            PricingBasisType.Order,
            PricingBasisType.OrderItem,
            PricingBasisType.OrderService
        ];

        public AddedProduct AddProduct(AcceptedProductAdditionArgs args, IIdGenerator idGenerator, IClock clock)
            => AddProduct(args, idGenerator, clock.GetDateTime());

        public AddedProduct AddProduct(AcceptedProductAdditionArgs args, IIdGenerator idGenerator, DateTimeOffset now)
            => AttachProductAddition(StageProductAddition(args, idGenerator, now), idGenerator, now);

        private StagedProductAddition StageProductAddition(
            AcceptedProductAdditionArgs args,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            EnsureCanAddProduct();

            var accepted = args.Accepted;
            var product = accepted.Product;

            EnsureProductIsSellable(product);

            if (accepted.PricingLines.Count == 0)
                throw ExceptionFactory.PriceChangeSetRequiresLines();

            var changeArgs = new AcceptedPriceChangeArgs(
                OrderChangeType.AddProduct,
                PriceChangeReason.AddProduct,
                accepted.PricingSource,
                [],
                accepted.SourceOfferId,
                accepted.SourcePricingReference,
                ChangeReason: null,
                ExternalReference: args.ExternalReference ?? accepted.SourceReference,
                ActorScope: args.ActorScope,
                ActorId: args.ActorId,
                OperationId: args.OperationId);

            var orderChange = StageOrderChange(changeArgs, idGenerator, now);

            var itemId = idGenerator.NewId();

            var item = new OrderItem(
                new CreateOrderItemArgs(
                    itemId,
                    Id,
                    product.ProductType,
                    product.ProductCode,
                    product.ProductName,
                    product.Quantity,
                    product.UnitOfMeasure,
                    now),
                policySnapshot: null,
                ProductSnapshotOf(product.Snapshot, itemId, idGenerator, now),
                CommercialTermsSnapshotOf(product.CommercialTerms, itemId, idGenerator, now));

            var services = new List<OrderService>();
            var serviceIdsByRef = new Dictionary<string, long>(StringComparer.Ordinal);

            foreach (var acceptedService in product.Services)
            {
                if (!serviceIdsByRef.TryAdd(acceptedService.ServiceRef, 0))
                    throw ExceptionFactory.ProductAdditionServiceRefNotUnique(acceptedService.ServiceRef);

                var service = StageAddedService(acceptedService, itemId, idGenerator, now);

                serviceIdsByRef[acceptedService.ServiceRef] = service.Id;
                services.Add(service);
            }

            var links = services
                .Select(service => new OrderItemServiceLink(
                    idGenerator.NewId(),
                    Id,
                    itemId,
                    service.Id,
                    orderChange.Id,
                    now))
                .ToList();

            var lines = accepted.PricingLines
                .Select(line => MapAdditionPricingLine(line, product.ProductRef, itemId, serviceIdsByRef))
                .ToList();

            EnsurePriceTreatmentIsSupported(services, lines);

            var priceChange = StagePriceChange(orderChange, changeArgs with { Lines = lines }, idGenerator, now);

            return new StagedProductAddition(orderChange, item, services, links, priceChange);
        }

        private AddedProduct AttachProductAddition(
            StagedProductAddition staged,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            AddItem(staged.Item);

            foreach (var service in staged.Services)
                AddOrderService(service);

            _itemServiceLinks.AddRange(staged.Links);

            var changeSet = AttachPriceChange(staged.PriceChange, now);

            foreach (var service in staged.Services)
                service.Activate();

            IncrementCommercialVersion();
            RecomputeCommercialSummary();

            var serviceIds = staged.Services.Select(service => service.Id).ToList();

            Causes(new OrderProductAdded(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                now,
                Id,
                staged.Change.Id,
                staged.Item.Id,
                serviceIds,
                changeSet.Id,
                changeSet.FinancialSequence,
                CommercialVersion,
                NextEventOrdinal(),
                ObligationVersion,
                CustomerTotal,
                CurrencyId));

            return new AddedProduct(
                staged.Change.Id,
                staged.Item.Id,
                serviceIds,
                changeSet.Id,
                changeSet.FinancialSequence);
        }

        private void EnsureCanAddProduct()
        {
            if (CommercialSummary != CommercialSummary.Active)
                throw ExceptionFactory.OrderNotEligibleForProductAddition(Id, CommercialSummary);
        }

        private static void EnsureProductIsSellable(AcceptedAddedProduct product)
        {
            if (BlockedProductTypes.Contains(product.ProductType))
                throw ExceptionFactory.ProductTypeNotSellable(product.ProductType);

            if (product.Quantity <= 0m)
                throw ExceptionFactory.ProductAdditionQuantityMustBePositive();

            if (product.Services.Count == 0)
                throw ExceptionFactory.ProductAdditionRequiresAService();
        }

        private OrderService StageAddedService(
            AcceptedAddedService accepted,
            long orderItemId,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            if (accepted.ServiceType == OrderServiceType.AirTransportation)
                throw ExceptionFactory.AirTransportationCannotBeAdded();

            EnsureServiceTypeIsAcceptable(accepted.ServiceType);

            var profile = ResolveFulfillmentProfile(
                accepted.Detail,
                accepted.ServiceType,
                accepted.RequiresReservation,
                accepted.RequiresDocument,
                accepted.DocumentKind);

            var service = new OrderService(new CreateOrderServiceArgs(
                idGenerator.NewId(),
                Id,
                orderItemId,
                accepted.ServiceType,
                accepted.ServiceCode,
                accepted.Name,
                accepted.DeliveryModel,
                accepted.PriceTreatment,
                profile.RequiresReservation,
                accepted.RequiresSupplierConfirmation,
                profile.RequiresDocument,
                accepted.ProviderType,
                now,
                profile.DocumentKind,
                accepted.RequiresPaymentCoverage,
                accepted.SupplierCode,
                accepted.DeliveryProviderReference));

            AttachAddedBeneficiaries(service, accepted, idGenerator);
            AttachDetail(service, accepted.ServiceType, accepted.Detail, ResolveAddedTargets(accepted.Detail), idGenerator);
            AttachAddedCoverage(service, accepted, idGenerator);

            return service;
        }

        private void AttachAddedBeneficiaries(OrderService service, AcceptedAddedService accepted, IIdGenerator idGenerator)
        {
            if (accepted.BeneficiaryTravellerIds.Count == 0)
                throw ExceptionFactory.ServiceRequiresBeneficiary(accepted.ServiceRef);

            foreach (var travellerId in accepted.BeneficiaryTravellerIds)
            {
                if (_travellers.All(traveller => traveller.Id != travellerId))
                    throw ExceptionFactory.ProductAdditionReferenceNotResolved("traveller", travellerId);

                service.AddBeneficiary(idGenerator.NewId(), travellerId);
            }

            if (RequiresExactlyOneBeneficiary(accepted.ServiceType) && service.Beneficiaries.Count != 1)
                throw ExceptionFactory.ServiceRequiresExactlyOneBeneficiary(service.Id, service.Beneficiaries.Count);
        }

        private void AttachAddedCoverage(OrderService service, AcceptedAddedService accepted, IIdGenerator idGenerator)
        {
            foreach (var coveredServiceId in accepted.CoveredOrderServiceIds ?? [])
                service.CoverService(idGenerator.NewId(), RequireExistingAirService(coveredServiceId));

            foreach (var coveredSegmentId in accepted.CoveredOrderSegmentIds ?? [])
            {
                if (_segments.All(segment => segment.Id != coveredSegmentId))
                    throw ExceptionFactory.ProductAdditionReferenceNotResolved("segment", coveredSegmentId);

                service.CoverSegment(idGenerator.NewId(), coveredSegmentId);
            }
        }

        private ResolvedServiceDetailTargets ResolveAddedTargets(AcceptedServiceDetail detail)
            => detail switch
            {
                AcceptedAddedSeatDetail seat => new ResolvedServiceDetailTargets(
                    AirServiceId: RequireExistingAirService(seat.AssociatedAirOrderServiceId)),
                AcceptedAddedLoungeDetail lounge => new ResolvedServiceDetailTargets(
                    AirServiceId: lounge.RelatedAirOrderServiceId is { } relatedId
                        ? RequireExistingAirService(relatedId)
                        : null),
                _ => new ResolvedServiceDetailTargets()
            };

        private long RequireExistingAirService(long orderServiceId)
        {
            var target = _orderServices.FirstOrDefault(service => service.Id == orderServiceId);

            if (target is null || !target.IsAirTransport)
                throw ExceptionFactory.ProductAdditionReferenceNotResolved("air service", orderServiceId);

            if (target.Status == OrderServiceStatus.Cancelled)
                throw ExceptionFactory.ProductAdditionTargetIsCancelled(orderServiceId);

            return orderServiceId;
        }

        private AcceptedPricingLineArgs MapAdditionPricingLine(
            AcceptedAdditionPricingLine line,
            string productRef,
            long orderItemId,
            IReadOnlyDictionary<string, long> serviceIdsByRef)
        {
            if (line.LineRole == PricingLineRole.Reversal)
                throw ExceptionFactory.ProductAdditionCannotReverse(line.SourceLineRef ?? line.Code ?? "-");

            if (!AdditionBasisTypes.Contains(line.BasisType))
                throw ExceptionFactory.ProductAdditionBasisNotSupported(line.BasisType);

            var serviceId = ResolveAdditionServiceRef(line.ServiceRef, serviceIdsByRef);

            if (line.BasisType == PricingBasisType.OrderItem && !string.Equals(line.ProductRef, productRef, StringComparison.Ordinal))
                throw ExceptionFactory.ProductAdditionReferenceNotResolved("product", line.ProductRef ?? "-");

            if (line.BasisType == PricingBasisType.OrderService && serviceId is null)
                throw ExceptionFactory.ProductAdditionReferenceNotResolved("service", line.ServiceRef ?? "-");

            var basisReferenceId = line.BasisType switch
            {
                PricingBasisType.Order => Id,
                PricingBasisType.OrderItem => orderItemId,
                _ => serviceId
            };

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
                OrderItemId: line.BasisType == PricingBasisType.Order ? null : orderItemId,
                Code: line.Code,
                Description: line.Description,
                ExchangeRate: line.ExchangeRate,
                ApplicationLevel: line.ApplicationLevel,
                Quantity: line.Quantity,
                UnitOfMeasure: line.UnitOfMeasure,
                UnitPrice: line.UnitPrice,
                BasisReferenceId: basisReferenceId,
                SourceLineRef: line.SourceLineRef,
                OccurrenceKey: line.OccurrenceKey,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory,
                AllocationSets: MapAdditionAllocationSets(line, orderItemId, serviceIdsByRef));
        }

        private long? ResolveAdditionServiceRef(string? serviceRef, IReadOnlyDictionary<string, long> serviceIdsByRef)
        {
            if (string.IsNullOrWhiteSpace(serviceRef))
                return null;

            return serviceIdsByRef.TryGetValue(serviceRef, out var serviceId)
                ? serviceId
                : throw ExceptionFactory.ProductAdditionReferenceNotResolved("service", serviceRef);
        }

        private IReadOnlyList<AcceptedPricingAllocationSetArgs>? MapAdditionAllocationSets(
            AcceptedAdditionPricingLine line,
            long orderItemId,
            IReadOnlyDictionary<string, long> serviceIdsByRef)
        {
            if (line.AllocationSets is null || line.AllocationSets.Count == 0)
                return null;

            return line.AllocationSets
                .Select(set => new AcceptedPricingAllocationSetArgs(
                    set.Purpose,
                    set.Source,
                    set.Method,
                    set.Completeness,
                    set.Allocations
                        .Select(allocation => new AcceptedPricingAllocationArgs(
                            allocation.SaleAmount,
                            allocation.SaleCurrencyId,
                            OrderItemId: orderItemId,
                            OrderServiceId: ResolveAdditionServiceRef(allocation.ServiceRef, serviceIdsByRef),
                            TravellerId: RequireExistingTraveller(allocation.OrderTravellerId),
                            ItineraryId: null,
                            SegmentId: RequireExistingSegment(allocation.OrderSegmentId),
                            CoveragePortionRef: allocation.CoveragePortionRef,
                            OriginalAmount: allocation.OriginalAmount,
                            OriginalCurrencyId: allocation.OriginalCurrencyId,
                            ExchangeRate: allocation.ExchangeRate))
                        .ToList(),
                    set.PricingContextRef,
                    set.PolicyVersion))
                .ToList();
        }

        private long? RequireExistingTraveller(long? travellerId)
        {
            if (travellerId is not { } id)
                return null;

            if (_travellers.All(traveller => traveller.Id != id))
                throw ExceptionFactory.ProductAdditionReferenceNotResolved("traveller", id);

            return id;
        }

        private long? RequireExistingSegment(long? segmentId)
        {
            if (segmentId is not { } id)
                return null;

            if (_segments.All(segment => segment.Id != id))
                throw ExceptionFactory.ProductAdditionReferenceNotResolved("segment", id);

            return id;
        }

        private static void EnsurePriceTreatmentIsSupported(
            IReadOnlyList<OrderService> services,
            IReadOnlyList<AcceptedPricingLineArgs> lines)
        {
            foreach (var service in services.Where(service => service.PriceTreatment == ServicePriceTreatment.SeparatelyPriced))
            {
                var hasPrimaryValue = lines.Any(line =>
                    ServicePriceTreatmentPolicy.IsPrimaryCustomerValue(line.ComponentType, line.Effect, line.LineRole)
                    && line.BasisType == PricingBasisType.OrderService
                    && line.BasisReferenceId == service.Id);

                if (!hasPrimaryValue)
                    throw ExceptionFactory.SeparatelyPricedServiceRequiresValue(service.ServiceCode);
            }
        }
    }
}
