using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Documents;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class ElectronicMiscDocumentIdentityPolicyTests
    {
        private const long OperationId = 9_600_001L;
        private const long BeneficiaryId = 9_700_001L;
        private const long SourceDocumentId = 9_800_001L;
        private const string SourceDocumentNumber = "M9500001";
        private const string SuccessorDocumentNumber = "M9500001X";
        private const int CurrencyId = 1;
        private const decimal Value = 60_000m;

        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_exact_replay_of_the_same_confirmed_successor_is_not_a_conflict()
            => Assert.Null(Conflict());

        [Fact]
        public void A_document_minted_by_another_servicing_operation_is_never_this_replay()
            => Assert.Contains(
                "origin servicing operation",
                Conflict(existingOperationId: OperationId + 1)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_for_another_beneficiary_is_a_conflict()
            => Assert.Contains(
                "beneficiary",
                Conflict(existingBeneficiaryId: BeneficiaryId + 1)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_of_another_type_is_a_conflict()
            => Assert.Contains(
                "document type",
                Conflict(returnedType: ElectronicMiscDocumentType.Standalone)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_in_another_currency_is_a_conflict()
            => Assert.Contains(
                "currency",
                Conflict(returnedCurrencyId: CurrencyId + 7)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_from_another_issuer_is_a_conflict()
            => Assert.Contains(
                "issuer carrier",
                Conflict(returnedIssuerCarrierId: 99L)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_from_another_issuing_office_is_a_conflict()
            => Assert.Contains(
                "issuing office",
                Conflict(returnedIssuingOfficeId: 77L)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_under_another_authority_is_a_conflict()
            => Assert.Contains(
                "document authority",
                Conflict(returnedAuthority: DocumentAuthority.External)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_with_another_reason_for_issuance_is_a_conflict()
            => Assert.Contains(
                "reason for issuance",
                Conflict(returnedReasonForIssuanceCode: "Z")!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_with_another_coupon_count_is_a_conflict()
            => Assert.Contains(
                "coupon count",
                Conflict(returnedCouponCount: 2)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_missing_the_returned_coupon_number_is_a_conflict()
            => Assert.Contains(
                "coupon 9",
                Conflict(returnedCouponNumber: 9)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_coupon_with_another_value_is_a_conflict()
            => Assert.Contains(
                "identity",
                Conflict(returnedValue: Value + 1m)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_coupon_with_another_sub_code_is_a_conflict()
            => Assert.Contains(
                "identity",
                Conflict(returnedSubCode: "0ZZ")!,
                StringComparison.Ordinal);

        [Fact]
        public void A_coupon_carrying_another_predecessor_is_a_conflict()
            => Assert.Contains(
                "exchange lineage",
                Conflict(existingPredecessorCouponNumber: 4)!,
                StringComparison.Ordinal);

        [Fact]
        public void An_associated_coupon_bound_to_another_ticket_coupon_is_a_conflict()
            => Assert.Contains(
                "ticket association",
                Conflict(existingAssociatedTicketCouponId: 123_456L)!,
                StringComparison.Ordinal);

        [Fact]
        public void An_exact_replay_of_a_standalone_successor_is_not_a_conflict()
            => Assert.Null(
                Conflict(
                    successorType: ElectronicMiscDocumentType.Standalone,
                    returnedType: ElectronicMiscDocumentType.Standalone));

        private string? Conflict(
            long? existingOperationId = null,
            long? existingBeneficiaryId = null,
            ElectronicMiscDocumentType? returnedType = null,
            int? returnedCurrencyId = null,
            long? returnedIssuerCarrierId = null,
            long? returnedIssuingOfficeId = null,
            DocumentAuthority? returnedAuthority = null,
            string? returnedReasonForIssuanceCode = null,
            int? returnedCouponCount = null,
            int? returnedCouponNumber = null,
            decimal? returnedValue = null,
            string? returnedSubCode = null,
            int? existingPredecessorCouponNumber = null,
            long? existingAssociatedTicketCouponId = null,
            ElectronicMiscDocumentType successorType = ElectronicMiscDocumentType.Associated)
        {
            const long ticketCouponId = 555_001L;
            var associated = successorType == ElectronicMiscDocumentType.Associated;

            var existing = ElectronicMiscDocument.IssueProviderConfirmed(
                1L,
                10L,
                existingBeneficiaryId ?? BeneficiaryId,
                existingOperationId ?? OperationId,
                SuccessorDocumentNumber,
                successorType,
                "A",
                1L,
                null,
                DocumentAuthority.Local,
                CurrencyId,
                [
                    new EmdCouponIssuance(
                        EmdCouponPurpose.Fee,
                        "0DF",
                        Value,
                        [],
                        PricingLineId: 42L,
                        AssociatedTicketCouponId: associated
                            ? existingAssociatedTicketCouponId ?? ticketCouponId
                            : existingAssociatedTicketCouponId,
                        PredecessorElectronicMiscDocumentId: SourceDocumentId,
                        PredecessorDocumentNumber: SourceDocumentNumber,
                        PredecessorCouponNumber: existingPredecessorCouponNumber ?? 1)
                ],
                [3],
                _ids,
                _clock);

            var group = new AcceptedExchangeAncillaryExchangeGroup(
                "EMDX-1",
                SourceDocumentId,
                SourceDocumentNumber,
                [1],
                successorType,
                "A",
                CurrencyId,
                [
                    new AcceptedExchangeAncillarySuccessorCoupon(
                        EmdCouponPurpose.Fee,
                        "0DF",
                        Value,
                        CurrencyId,
                        associated ? 1 : null,
                        associated ? ticketCouponId : null)
                ],
                "ANC-DISPOSITION",
                "ANC-EXCHANGE-SOURCE",
                PricingSource.Supplier,
                Array.Empty<AcceptedRefundPricingLine>());

            var coupons = Enumerable
                .Range(0, returnedCouponCount ?? 1)
                .Select(index => new SuccessorEmdCouponIdentity(
                    index == 0 ? returnedCouponNumber ?? 3 : 4 + index,
                    EmdCouponPurpose.Fee,
                    returnedSubCode ?? "0DF",
                    returnedValue ?? Value,
                    CurrencyId,
                    associated ? 1 : null))
                .ToList();

            var successor = new SuccessorEmdIdentity(
                SuccessorDocumentNumber,
                returnedType ?? successorType,
                returnedIssuerCarrierId ?? 1L,
                returnedIssuingOfficeId,
                returnedAuthority ?? DocumentAuthority.Local,
                returnedReasonForIssuanceCode ?? "A",
                returnedCurrencyId ?? CurrencyId,
                coupons,
                BeneficiaryId,
                associated ? "T9500099" : null);

            return ElectronicMiscDocumentIdentityPolicy.Conflict(
                existing, group, successor, OperationId, BeneficiaryId);
        }
    }
}
