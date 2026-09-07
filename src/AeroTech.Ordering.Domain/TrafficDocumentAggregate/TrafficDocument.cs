using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;
using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.DomainEvents;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate
{
    public abstract class TrafficDocument : AggregateRoot<long>
    {
        private readonly List<DocumentCoupon> _coupons = new();

        protected TrafficDocument()
        {
        }

        protected TrafficDocument(
            long id,
            long orderId,
            long travellerId,
            string documentNumber,
            SalesChannel issueChannel,
            long? issueUserId,
            string? issueReference,
            DocumentAmounts amounts,
            DateTimeOffset issuedAt,
            DateTimeOffset validUntil,
            DateTimeOffset? voidDeadline)
        {
            Id = id;
            OrderId = orderId;
            TravellerId = travellerId;
            DocumentNumber = documentNumber;
            DocumentUniqueCode = Guid.NewGuid().ToString("N");
            Status = TrafficDocumentStatus.Issued;
            IssuerSystem = IssuerSystem.ElectronicTicketingByAirline;
            IssuedAt = issuedAt;
            VoidDeadline = voidDeadline;
            IssueChannel = issueChannel;
            IssueUserId = issueUserId;
            IssueReference = issueReference;
            ValidFrom = issuedAt;
            ValidUntil = validUntil;
            Fare = amounts.Fare;
            TaxesTotal = amounts.TaxesTotal;
            FeesTotal = amounts.FeesTotal;
            Commission = amounts.Commission;
            TotalAmount = amounts.TotalAmount;
            CurrencyId = amounts.CurrencyId;
        }

        public long OrderId { get; private set; }

        public long TravellerId { get; private set; }

        public string DocumentNumber { get; private set; } = default!;

        public string DocumentUniqueCode { get; private set; } = default!;

        public TrafficDocumentStatus Status { get; protected set; }

        public IssuerSystem IssuerSystem { get; private set; }

        public DateTimeOffset IssuedAt { get; private set; }

        public DateTimeOffset? VoidDeadline { get; private set; }

        public VoidReason? VoidReason { get; private set; }

        public string? VoidReasonDetail { get; private set; }

        public long? VoidedBy { get; private set; }

        public DateTimeOffset? VoidedAt { get; private set; }

        public VoidReason? CancelReason { get; private set; }

        public long? CancelledBy { get; private set; }

        public DateTimeOffset? CancelledAt { get; private set; }

        public SalesChannel IssueChannel { get; private set; }

        public long? IssueUserId { get; private set; }

        public string? IssueReference { get; private set; }

        public DateTimeOffset ValidFrom { get; private set; }

        public DateTimeOffset ValidUntil { get; private set; }

        public decimal Fare { get; private set; }

        public decimal TaxesTotal { get; private set; }

        public decimal FeesTotal { get; private set; }

        public decimal Commission { get; private set; }

        public decimal TotalAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public IReadOnlyCollection<DocumentCoupon> Coupons => _coupons.AsReadOnly();

        protected void RegisterCoupon(DocumentCoupon coupon) => _coupons.Add(coupon);

        public void EnsureCanBeVoided(DateTimeOffset now)
        {
            if (Status is not (TrafficDocumentStatus.Issued or TrafficDocumentStatus.VoidUnconfirmed))
                throw ExceptionFactory.OnlyIssuedDocumentCanBeVoided();

            EnsureWithinTerminationWindow(now);
        }

        public void EnsureCanBeCancelled(DateTimeOffset now)
        {
            if (Status is not (TrafficDocumentStatus.Issued or TrafficDocumentStatus.CancelUnconfirmed))
                throw ExceptionFactory.OnlyIssuedDocumentCanBeCancelled();

            EnsureWithinTerminationWindow(now);
        }

        private void EnsureWithinTerminationWindow(DateTimeOffset now)
        {
            if (VoidDeadline is null || now >= VoidDeadline)
                throw ExceptionFactory.TerminationWindowExpired();

            if (_coupons.Any(coupon => coupon.Status is CouponStatus.Flown or CouponStatus.Used or CouponStatus.Boarded or CouponStatus.CheckedIn))
                throw ExceptionFactory.DocumentWithUsedCouponCannotBeTerminated();
        }

        public void Void(DateTimeOffset now, VoidReason reason, string? reasonDetail, long voidedBy)
        {
            EnsureCanBeVoided(now);

            Status = TrafficDocumentStatus.Voided;
            RecordVoidIntent(reason, reasonDetail, voidedBy);
            VoidedAt = now;

            foreach (var coupon in _coupons)
                coupon.Void();
        }

        public void MarkVoidUnconfirmed(VoidReason reason, string? reasonDetail, long voidedBy)
        {
            if (Status != TrafficDocumentStatus.Issued)
                throw ExceptionFactory.OnlyIssuedDocumentCanBeMarkedVoidUnconfirmed();

            Status = TrafficDocumentStatus.VoidUnconfirmed;
            RecordVoidIntent(reason, reasonDetail, voidedBy);
        }

        public void Cancel(VoidReason reason, long cancelledBy, DateTimeOffset at)
        {
            EnsureCanBeCancelled(at);

            Status = TrafficDocumentStatus.Cancelled;
            RecordCancelIntent(reason, cancelledBy);
            CancelledAt = at;

            foreach (var coupon in _coupons)
                coupon.Cancel();
        }

        public void MarkCancelUnconfirmed(VoidReason reason, long cancelledBy)
        {
            if (Status != TrafficDocumentStatus.Issued)
                throw ExceptionFactory.OnlyIssuedDocumentCanBeMarkedCancelUnconfirmed();

            Status = TrafficDocumentStatus.CancelUnconfirmed;
            RecordCancelIntent(reason, cancelledBy);
        }

        public void EnsureCanBeReassigned()
        {
            if (_coupons.Any(coupon => coupon.Status is CouponStatus.Flown or CouponStatus.Used or CouponStatus.Boarded or CouponStatus.CheckedIn))
                throw ExceptionFactory.DocumentWithUsedCouponCannotBeMoved();
        }

        public void ReassignToOrder(long newOrderId, long newTravellerId, IReadOnlyDictionary<long, long> serviceMap, IIdGenerator idGenerator, DateTimeOffset at)
        {
            EnsureCanBeReassigned();

            var previousOrderId = OrderId;

            OrderId = newOrderId;
            TravellerId = newTravellerId;

            foreach (var coupon in _coupons)
            {
                if (!serviceMap.TryGetValue(coupon.OrderServiceId, out var newServiceId))
                    throw ExceptionFactory.CouponReferencesServiceNotInSplit(coupon.Id, coupon.OrderServiceId);

                coupon.ReassignService(newServiceId);
            }

            Causes(new TrafficDocumentReassigned(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                at,
                Id,
                DocumentNumber,
                previousOrderId,
                newOrderId,
                newTravellerId));
        }

        private void RecordVoidIntent(VoidReason reason, string? reasonDetail, long voidedBy)
        {
            VoidReason = reason;
            VoidReasonDetail = reasonDetail;
            VoidedBy = voidedBy;
        }

        private void RecordCancelIntent(VoidReason reason, long cancelledBy)
        {
            CancelReason = reason;
            CancelledBy = cancelledBy;
        }
    }
}
