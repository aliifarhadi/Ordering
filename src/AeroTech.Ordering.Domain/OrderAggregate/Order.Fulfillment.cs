using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void AcceptCommercially()
        {
            foreach (var service in _orderServices)
                service.Activate();

            RecomputeCommercialSummary();
        }

        public void ApplyReservationOutcome(
            IReadOnlyCollection<long> confirmedServiceIds,
            string? externalReservationRef,
            DateTimeOffset? holdUntil,
            IIdGenerator idGenerator,
            IClock clock)
        {
            foreach (var service in _orderServices.Where(service => confirmedServiceIds.Contains(service.Id)))
                service.MarkReservationConfirmed();

            if (!string.IsNullOrWhiteSpace(externalReservationRef))
                RecordExternalReference(
                    ExternalReferenceType.ProviderReservation,
                    "Inventory",
                    externalReservationRef,
                    idGenerator,
                    clock);

            if (holdUntil is { } dueAt)
            {
                TimeToLive = dueAt;
                AddTimeLimit(TimeLimitType.Ticketing, dueAt, externalReservationRef, idGenerator);
            }

            RecomputeCommercialSummary();
        }

        public void ApplyReservationReleased(IReadOnlyCollection<long> serviceIds, IClock clock)
        {
            foreach (var service in _orderServices.Where(service => serviceIds.Contains(service.Id)))
                service.MarkReservationReleased();

            MeetTimeLimit(TimeLimitType.Ticketing, clock);
            TimeToLive = null;

            RecomputeCommercialSummary();
        }

        public IReadOnlyCollection<long> RequiredDocumentServiceIds()
            => _orderServices
                .Where(service => service.RequiresDocument && service.Status != OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToList();

        public IReadOnlyCollection<long> RequiredElectronicTicketServiceIds()
            => _orderServices
                .Where(RequiresElectronicTicket)
                .Where(service => service.Status != OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToList();

        public IReadOnlyCollection<long> DocumentedServiceIds()
            => _orderServices
                .Where(service => service.DocumentStatus == OrderServiceDocumentStatus.Issued)
                .Select(service => service.Id)
                .ToList();

        public IReadOnlyCollection<long> DocumentedElectronicTicketServiceIds()
            => _orderServices
                .Where(RequiresElectronicTicket)
                .Where(service => service.DocumentStatus == OrderServiceDocumentStatus.Issued)
                .Select(service => service.Id)
                .ToList();

        public bool IsElectronicTicketingComplete()
        {
            var required = RequiredElectronicTicketServiceIds();
            var documented = DocumentedElectronicTicketServiceIds();

            return required.Count > 0 && required.All(documented.Contains);
        }

        public IReadOnlyCollection<long> RequiredElectronicMiscDocumentServiceIds()
            => _orderServices
                .Where(RequiresElectronicMiscDocument)
                .Where(service => service.Status != OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToList();

        public IReadOnlyCollection<long> DocumentedElectronicMiscDocumentServiceIds()
            => _orderServices
                .Where(RequiresElectronicMiscDocument)
                .Where(service => service.ElectronicMiscDocumentId.HasValue)
                .Select(service => service.Id)
                .ToList();

        public void RecordIssuedMiscellaneousDocuments(IReadOnlyCollection<IssuedServiceMiscellaneousDocument> documents)
        {
            foreach (var document in documents)
            {
                var service = _orderServices.SingleOrDefault(candidate => candidate.Id == document.OrderServiceId);
                service?.MarkMiscellaneousDocumented(document.ElectronicMiscDocumentId, document.EmdCouponId);
            }

            RecomputeCommercialSummary();
        }

        internal static bool RequiresElectronicMiscDocument(Entities.OrderService service)
            => service.RequiresDocument && service.DocumentKind == ServiceDocumentKind.ElectronicMiscDocument;

        internal static bool RequiresElectronicTicket(Entities.OrderService service)
            => service.RequiresDocument && service.DocumentKind == ServiceDocumentKind.ElectronicTicket;

        public void RecordIssuedDocuments(IReadOnlyCollection<IssuedServiceDocument> documents)
        {
            foreach (var document in documents)
            {
                var service = _orderServices.SingleOrDefault(candidate => candidate.Id == document.OrderServiceId);
                service?.MarkDocumented(document.ElectronicTicketId, document.TicketCouponId);
            }

            RecomputeCommercialSummary();
        }

        public void CompleteTicketing(IClock clock)
        {
            if (!IsElectronicTicketingComplete())
                return;

            MeetTimeLimit(TimeLimitType.Ticketing, clock);
            TimeToLive = null;

            RecomputeCommercialSummary();
        }

        public void WithdrawBeforeTicketing(
            IReadOnlyCollection<long> serviceIds,
            VoidReason reason,
            long withdrawnBy,
            IIdGenerator idGenerator,
            IClock clock)
        {
            foreach (var service in _orderServices.Where(service => serviceIds.Contains(service.Id)))
                service.MarkCancelled();

            RollUpCancelledItems(serviceIds);
            TimeToLive = null;
            MeetTimeLimit(TimeLimitType.Ticketing, clock);

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            Causes(new OrderWithdrawn(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                AirlineOfficeId,
                CustomerId,
                CommercialVersion,
                NextEventOrdinal(),
                CommercialSummary,
                reason,
                withdrawnBy,
                serviceIds.ToList()));
        }
    }

    public sealed record IssuedServiceDocument(long OrderServiceId, long ElectronicTicketId, long TicketCouponId);

    public sealed record IssuedServiceMiscellaneousDocument(long OrderServiceId, long ElectronicMiscDocumentId, long EmdCouponId);
}
