using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Documents;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class ResidualDocumentIdentityPolicyTests
    {
        private const long OperationId = 9_600_101L;
        private const long BeneficiaryId = 9_700_101L;
        private const long IssuerCarrierId = 11L;
        private const string DocumentNumber = "M9600101XR";
        private const string ReasonForIssuanceCode = "D";
        private const string ReasonForIssuanceSubCode = "98R";
        private const int CurrencyId = 1;
        private const decimal Amount = 10_000m;

        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_exact_existing_residual_document_is_not_a_conflict()
            => Assert.Null(Conflict());

        [Fact]
        public void An_associated_document_under_the_same_number_is_a_conflict()
            => Assert.Contains(
                "document type",
                Conflict(existingType: ElectronicMiscDocumentType.Associated)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_from_another_servicing_operation_is_a_conflict()
            => Assert.Contains(
                "origin servicing operation",
                Conflict(existingOperationId: OperationId + 1)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_for_another_beneficiary_is_a_conflict()
            => Assert.Contains(
                "beneficiary",
                Conflict(existingBeneficiaryId: BeneficiaryId + 1)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_from_another_issuer_is_a_conflict()
            => Assert.Contains(
                "issuer carrier",
                Conflict(existingIssuerCarrierId: 99L)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_from_another_issuing_office_is_a_conflict()
            => Assert.Contains(
                "issuing office",
                Conflict(existingIssuingOfficeId: 77L)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_under_another_authority_is_a_conflict()
            => Assert.Contains(
                "document authority",
                Conflict(existingAuthority: DocumentAuthority.External)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_in_another_currency_is_a_conflict()
            => Assert.Contains(
                "currency",
                Conflict(existingCurrencyId: CurrencyId + 7)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_with_another_reason_for_issuance_is_a_conflict()
            => Assert.Contains(
                "reason for issuance",
                Conflict(existingReasonForIssuanceCode: "Z")!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_with_another_reason_for_issuance_sub_code_is_a_conflict()
            => Assert.Contains(
                "reason for issuance sub code",
                Conflict(existingReasonForIssuanceSubCode: "99Z")!,
                StringComparison.Ordinal);

        [Fact]
        public void A_residual_for_another_amount_is_a_conflict()
            => Assert.Contains(
                "residual amount",
                Conflict(existingAmount: Amount + 1m)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_whose_only_coupon_is_not_a_residual_value_coupon_is_a_conflict()
            => Assert.Contains(
                "coupon purpose",
                Conflict(existingPurpose: EmdCouponPurpose.Deposit)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_document_carrying_more_than_one_coupon_is_a_conflict()
            => Assert.Contains(
                "coupon count",
                Conflict(existingCouponCount: 2)!,
                StringComparison.Ordinal);

        private string? Conflict(
            ElectronicMiscDocumentType existingType = ElectronicMiscDocumentType.Standalone,
            long? existingOperationId = null,
            long? existingBeneficiaryId = null,
            long? existingIssuerCarrierId = null,
            long? existingIssuingOfficeId = null,
            DocumentAuthority? existingAuthority = null,
            int? existingCurrencyId = null,
            string? existingReasonForIssuanceCode = null,
            string? existingReasonForIssuanceSubCode = null,
            decimal? existingAmount = null,
            EmdCouponPurpose existingPurpose = EmdCouponPurpose.ResidualValue,
            int existingCouponCount = 1)
        {
            var associated = existingType == ElectronicMiscDocumentType.Associated;

            var coupons = Enumerable
                .Range(0, existingCouponCount)
                .Select(index => new EmdCouponIssuance(
                    index == 0 ? existingPurpose : EmdCouponPurpose.ResidualValue,
                    existingReasonForIssuanceSubCode ?? ReasonForIssuanceSubCode,
                    existingAmount ?? Amount,
                    [],
                    ExternalValueReference: DocumentNumber,
                    AssociatedTicketCouponId: associated ? 555_001L : null))
                .ToList();

            var existing = ElectronicMiscDocument.Issue(
                1L,
                10L,
                existingBeneficiaryId ?? BeneficiaryId,
                existingOperationId ?? OperationId,
                DocumentNumber,
                existingType,
                existingReasonForIssuanceCode ?? ReasonForIssuanceCode,
                existingIssuerCarrierId ?? IssuerCarrierId,
                existingIssuingOfficeId,
                existingAuthority ?? DocumentAuthority.Local,
                existingCurrencyId ?? CurrencyId,
                coupons,
                _ids,
                _clock);

            var residual = new ResidualDocumentIdentity(
                DocumentNumber,
                ResidualInstrumentKind.Emd,
                Amount,
                CurrencyId,
                IssuerCarrierId,
                null,
                DocumentAuthority.Local,
                ReasonForIssuanceCode,
                ReasonForIssuanceSubCode);

            return ResidualDocumentIdentityPolicy.Conflict(existing, residual, OperationId, BeneficiaryId);
        }
    }
}
