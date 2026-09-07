using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        private readonly List<OrderTimeLimit> _timeLimits = new();
        private readonly List<OrderExternalReference> _externalReferences = new();

        public CommercialSummary CommercialSummary { get; private set; }

        public OrderLineage Lineage { get; private set; } = default!;

        public long ObligationVersion { get; private set; }

        public DateTimeOffset? ClosedAt { get; private set; }

        public IReadOnlyCollection<OrderTimeLimit> TimeLimits => _timeLimits.AsReadOnly();

        public IReadOnlyCollection<OrderExternalReference> ExternalReferences => _externalReferences.AsReadOnly();

        public void AddTimeLimit(TimeLimitType type, DateTimeOffset dueAt, string? sourceReference, IIdGenerator idGenerator)
        {
            foreach (var superseded in _timeLimits.Where(limit => limit.Type == type && limit.Status == TimeLimitStatus.Active))
                superseded.Cancel(dueAt);

            _timeLimits.Add(new OrderTimeLimit(idGenerator.NewId(), Id, type, dueAt, sourceReference));
        }

        public void MeetTimeLimit(TimeLimitType type, IClock clock)
        {
            foreach (var limit in _timeLimits.Where(limit => limit.Type == type && limit.Status == TimeLimitStatus.Active))
                limit.Meet(clock.GetDateTime());
        }

        public OrderTimeLimit? ActiveTimeLimit(TimeLimitType type)
            => _timeLimits.SingleOrDefault(limit => limit.Type == type && limit.Status == TimeLimitStatus.Active);

        public void RecordExternalReference(
            ExternalReferenceType type,
            string sourceSystem,
            string reference,
            IIdGenerator idGenerator,
            IClock clock)
        {
            if (_externalReferences.Any(existing =>
                    existing.Type == type
                    && existing.SourceSystem == sourceSystem
                    && existing.Reference == reference))
                return;

            _externalReferences.Add(new OrderExternalReference(
                idGenerator.NewId(),
                Id,
                type,
                sourceSystem,
                reference,
                clock.GetDateTime()));
        }

        public void AdvanceObligationVersion() => ObligationVersion++;

        internal void RecomputeCommercialSummary()
        {
            CommercialSummary = DeriveCommercialSummary();
            Status = DeriveLegacyStatus();
        }

        private CommercialSummary DeriveCommercialSummary()
        {
            if (ClosedAt is not null)
                return CommercialSummary.Closed;

            if (_orderServices.Count == 0)
                return CommercialSummary.Draft;

            if (_orderServices.All(service => service.Status == OrderServiceStatus.Cancelled))
                return CommercialSummary.Cancelled;

            if (_orderServices.Any(service => service.Status is OrderServiceStatus.Active or OrderServiceStatus.Fulfilled
                    or OrderServiceStatus.PartiallyActive or OrderServiceStatus.PartiallyFulfilled))
                return CommercialSummary.Active;

            if (_orderServices.All(service => service.Status is OrderServiceStatus.Cancelled or OrderServiceStatus.Failed))
                return CommercialSummary.Inactive;

            return CommercialSummary.Draft;
        }

        private OrderStatus DeriveLegacyStatus()
        {
            if (CommercialSummary == CommercialSummary.Cancelled)
                return OrderStatus.Cancelled;

            if (_orderServices.Count > 0 && _orderServices.All(service =>
                    service.DocumentStatus == OrderServiceDocumentStatus.Issued
                    || service.Status == OrderServiceStatus.Cancelled))
                return OrderStatus.Ticketed;

            if (_orderServices.Any(service => service.FulfillmentStatus == OrderFulfillmentStatus.Confirmed))
                return OrderStatus.Confirmed;

            return OrderStatus.Created;
        }
    }
}
