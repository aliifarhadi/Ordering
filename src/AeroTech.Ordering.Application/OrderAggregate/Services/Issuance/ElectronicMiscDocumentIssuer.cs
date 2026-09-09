using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class ElectronicMiscDocumentIssuer : IElectronicMiscDocumentIssuer
    {
        public const string IssueStep = "issue-emd";

        private readonly IElectronicMiscDocumentRepository _documents;
        private readonly IEmdIssuancePort _provider;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ElectronicMiscDocumentIssuer(
            IElectronicMiscDocumentRepository documents,
            IEmdIssuancePort provider,
            IOrderOperationCoordinator operations,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _documents = documents;
            _provider = provider;
            _operations = operations;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<ElectronicMiscDocumentIssuanceOutcome> IssueAsync(
            ElectronicMiscDocumentIssuanceRequest request,
            CancellationToken cancellationToken = default)
        {
            var order = request.Order;
            var operation = request.Operation;
            var stock = request.Stock;

            var summaries = request.AlreadyIssued.ToList();
            var outstanding = request.Scope.ToHashSet();
            var alreadyIrreversible = request.AlreadyIrreversible;

            foreach (var plan in BuildPlans(order, request.Scope, request.Tickets))
            {
                var role = DocumentRole(plan.GroupKey);
                var priorAttempt = stock.FindAllocation(operation.OperationId, role);

                DocumentIssuanceResult result;
                DocumentStockAllocation allocation;

                if (priorAttempt is { State: StockNumberState.Reserved })
                {
                    allocation = priorAttempt;

                    result = await _provider.RecoverAsync(
                        new DocumentRecoveryRequest(
                            _operations.ProviderOperationKey(operation, $"{IssueStep}:{plan.GroupKey}"),
                            order.Id,
                            operation.OperationId,
                            allocation.DocumentNumber),
                        cancellationToken);
                }
                else
                {
                    allocation = stock.Allocate(operation.OperationId, role, _idGenerator, _clock);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    result = await _provider.IssueAsync(
                        BuildIssuanceRequest(order, operation, plan, allocation, request.OwnerAirlineId, request.Tickets),
                        cancellationToken);
                }

                if (result.Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                    return new ElectronicMiscDocumentIssuanceOutcome(result.Outcome, summaries, outstanding, alreadyIrreversible, result.Detail);

                if (result.Outcome == ProviderOperationOutcome.Rejected)
                {
                    if (!alreadyIrreversible)
                        stock.Retire(operation.OperationId, role, _clock);

                    return new ElectronicMiscDocumentIssuanceOutcome(result.Outcome, summaries, outstanding, alreadyIrreversible, result.Detail);
                }

                var document = ElectronicMiscDocument.Issue(
                    _idGenerator.NewId(),
                    order.Id,
                    plan.TravelerId,
                    operation.OperationId,
                    allocation.DocumentNumber,
                    plan.Type,
                    plan.ReasonForIssuanceCode,
                    request.OwnerAirlineId,
                    order.AirlineOfficeId,
                    DocumentAuthority.Local,
                    order.CurrencyId,
                    plan.Coupons,
                    _idGenerator,
                    _clock);

                document.RecordProviderConfirmation(result.ProviderReference);

                await _documents.AddAsync(document, cancellationToken);
                stock.MarkIssued(operation.OperationId, role, _clock);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var evidence = DocumentEvidenceFrom(document);
                order.RecordIssuedMiscellaneousDocuments(evidence);

                foreach (var issued in evidence)
                    outstanding.Remove(issued.OrderServiceId);

                summaries.Add(Summarize(document));
                alreadyIrreversible = true;
            }

            return new ElectronicMiscDocumentIssuanceOutcome(
                ProviderOperationOutcome.Confirmed,
                summaries,
                outstanding,
                alreadyIrreversible,
                null);
        }

        public static IReadOnlyCollection<IssuedServiceMiscellaneousDocument> DocumentEvidenceFrom(ElectronicMiscDocument document)
            => document.Coupons
                .Where(coupon => coupon.OrderServiceId.HasValue)
                .Select(coupon => new IssuedServiceMiscellaneousDocument(coupon.OrderServiceId!.Value, document.Id, coupon.Id))
                .ToList();

        public static IssuedMiscellaneousDocumentSummary Summarize(ElectronicMiscDocument document)
            => new(document.Id, document.DocumentNumber, document.Type, document.ReasonForIssuanceCode, document.Coupons.Count);

        private static string DocumentRole(string groupKey) => $"Emd:{groupKey}";

        private EmdIssuanceRequest BuildIssuanceRequest(
            Order order,
            OrderOperation operation,
            MiscellaneousDocumentPlan plan,
            DocumentStockAllocation allocation,
            long ownerAirlineId,
            IReadOnlyList<ElectronicTicket> tickets)
        {
            var couponNumber = 1;

            return new EmdIssuanceRequest(
                _operations.ProviderOperationKey(operation, $"{IssueStep}:{plan.GroupKey}"),
                order.Id,
                operation.OperationId,
                plan.TravelerId,
                allocation.DocumentNumber,
                plan.Type,
                plan.ReasonForIssuanceCode,
                ownerAirlineId,
                order.CurrencyId,
                plan.Coupons.Sum(coupon => coupon.IssuanceValue),
                plan.Coupons
                    .Select(coupon => new EmdCouponRequest(
                        couponNumber++,
                        coupon.Purpose,
                        coupon.ReasonForIssuanceSubCode,
                        coupon.IssuanceValue,
                        coupon.OrderServiceId,
                        AssociatedDocumentNumber(tickets, coupon.AssociatedTicketCouponId),
                        AssociatedCouponNumber(tickets, coupon.AssociatedTicketCouponId),
                        coupon.ExternalValueReference))
                    .ToList());
        }

        private static string? AssociatedDocumentNumber(IReadOnlyList<ElectronicTicket> tickets, long? ticketCouponId)
            => ticketCouponId is { } id
                ? tickets.FirstOrDefault(ticket => ticket.Coupons.Any(coupon => coupon.Id == id))?.DocumentNumber
                : null;

        private static int? AssociatedCouponNumber(IReadOnlyList<ElectronicTicket> tickets, long? ticketCouponId)
            => ticketCouponId is { } id
                ? tickets.SelectMany(ticket => ticket.Coupons).FirstOrDefault(coupon => coupon.Id == id)?.CouponNumber
                : null;

        private static IReadOnlyList<MiscellaneousDocumentPlan> BuildPlans(
            Order order,
            IReadOnlyList<long> scope,
            IReadOnlyList<ElectronicTicket> tickets)
        {
            var obligations = order.OrderServices
                .Where(service => scope.Contains(service.Id))
                .Select(service => new
                {
                    Service = service,
                    Snapshot = service.EmdIssuanceSnapshot
                        ?? throw ExceptionFactory.MiscellaneousDocumentIssuanceProfileMissing(service.Id)
                })
                .ToList();

            return obligations
                .GroupBy(obligation => new
                {
                    TravelerId = SoleBeneficiary(obligation.Service),
                    GroupKey = obligation.Snapshot.DocumentGroupReference ?? $"svc-{obligation.Service.Id}"
                })
                .OrderBy(group => group.Key.GroupKey, StringComparer.Ordinal)
                .Select(group => BuildPlan(order, group.Key.TravelerId, group.Key.GroupKey, group.Select(item => item.Service).ToList(), tickets))
                .ToList();
        }

        private static MiscellaneousDocumentPlan BuildPlan(
            Order order,
            long? travelerId,
            string groupKey,
            IReadOnlyList<OrderService> services,
            IReadOnlyList<ElectronicTicket> tickets)
        {
            var codes = services.Select(service => service.EmdIssuanceSnapshot!.ReasonForIssuanceCode).Distinct(StringComparer.Ordinal).ToList();

            if (codes.Count > 1)
                throw ExceptionFactory.MiscellaneousDocumentRequiresSingleReasonForIssuance(codes[0], codes[1]);

            var types = services.Select(service => service.EmdIssuanceSnapshot!.EmdType).Distinct().ToList();

            if (types.Count > 1)
                throw ExceptionFactory.MiscellaneousDocumentRequiresSingleReasonForIssuance(types[0], types[1]);

            var coupons = services
                .OrderBy(service => service.Id)
                .Select(service => BuildCoupon(order, service, types[0], tickets))
                .ToList();

            return new MiscellaneousDocumentPlan(groupKey, travelerId, types[0], codes[0], coupons);
        }

        private static EmdCouponIssuance BuildCoupon(
            Order order,
            OrderService service,
            ElectronicMiscDocumentType type,
            IReadOnlyList<ElectronicTicket> tickets)
        {
            var snapshot = service.EmdIssuanceSnapshot!;

            var attributions = order.ServiceValueAttributions(service.Id).ToList();

            if (attributions.Count == 0)
                throw ExceptionFactory.EmdCouponValueNotAttributable(service.Id);

            return new EmdCouponIssuance(
                EmdCouponPurpose.Service,
                snapshot.ReasonForIssuanceSubCode,
                attributions.Sum(attribution => attribution.SignedSaleAmount),
                attributions
                    .Select(attribution => new EmdCouponPriceLink(
                        attribution.PricingLineId,
                        attribution.AllocationId,
                        attribution.SignedSaleAmount))
                    .ToList(),
                OrderServiceId: service.Id,
                AssociatedTicketCouponId: type == ElectronicMiscDocumentType.Associated
                    ? ResolveTicketCoupon(snapshot.AssociatedAirOrderServiceId, service.Id, tickets)
                    : null);
        }

        private static long ResolveTicketCoupon(
            long? associatedAirOrderServiceId,
            long orderServiceId,
            IReadOnlyList<ElectronicTicket> tickets)
        {
            if (associatedAirOrderServiceId is not { } airServiceId)
                throw ExceptionFactory.AssociatedServiceReferenceMissing(orderServiceId);

            var candidates = tickets
                .SelectMany(ticket => ticket.Coupons)
                .Where(coupon => coupon.CurrentOrderServiceId == airServiceId
                                 && coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                .ToList();

            if (candidates.Count != 1)
                throw ExceptionFactory.TicketCouponAssociationNotResolvable(airServiceId, candidates.Count);

            return candidates[0].Id;
        }

        private static long? SoleBeneficiary(OrderService service)
            => service.Beneficiaries.Count == 1 ? service.Beneficiaries.First().OrderTravellerId : null;

        private sealed record MiscellaneousDocumentPlan(
            string GroupKey,
            long? TravelerId,
            ElectronicMiscDocumentType Type,
            string ReasonForIssuanceCode,
            IReadOnlyList<EmdCouponIssuance> Coupons);
    }
}
