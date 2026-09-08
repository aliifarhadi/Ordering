using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderServiceEmdIssuanceSnapshot : Entity<long>
    {
        private OrderServiceEmdIssuanceSnapshot()
        {
        }

        internal OrderServiceEmdIssuanceSnapshot(
            long id,
            long orderServiceId,
            ElectronicMiscDocumentType emdType,
            string reasonForIssuanceCode,
            string reasonForIssuanceSubCode,
            DateTimeOffset capturedAt,
            long? associatedAirOrderServiceId = null,
            string? documentGroupReference = null,
            string? sourceSystem = null,
            string? sourceReference = null)
        {
            if (string.IsNullOrWhiteSpace(reasonForIssuanceCode))
                throw ExceptionFactory.ReasonForIssuanceCodeRequired();

            if (string.IsNullOrWhiteSpace(reasonForIssuanceSubCode))
                throw ExceptionFactory.ReasonForIssuanceSubCodeRequired(orderServiceId);

            if (emdType == ElectronicMiscDocumentType.Associated && associatedAirOrderServiceId is null)
                throw ExceptionFactory.AssociatedServiceReferenceMissing(orderServiceId);

            if (emdType == ElectronicMiscDocumentType.Standalone && associatedAirOrderServiceId is not null)
                throw ExceptionFactory.StandaloneDocumentCannotAssociateTicketCoupon(orderServiceId);

            Id = id;
            OrderServiceId = orderServiceId;
            EmdType = emdType;
            ReasonForIssuanceCode = reasonForIssuanceCode.Trim();
            ReasonForIssuanceSubCode = reasonForIssuanceSubCode.Trim();
            AssociatedAirOrderServiceId = associatedAirOrderServiceId;
            DocumentGroupReference = documentGroupReference;
            SourceSystem = sourceSystem;
            SourceReference = sourceReference;
            CapturedAt = capturedAt;
        }

        public long OrderServiceId { get; private set; }

        public ElectronicMiscDocumentType EmdType { get; private set; }

        public string ReasonForIssuanceCode { get; private set; } = default!;

        public string ReasonForIssuanceSubCode { get; private set; } = default!;

        public long? AssociatedAirOrderServiceId { get; private set; }

        public string? DocumentGroupReference { get; private set; }

        public string? SourceSystem { get; private set; }

        public string? SourceReference { get; private set; }

        public DateTimeOffset CapturedAt { get; private set; }
    }
}
