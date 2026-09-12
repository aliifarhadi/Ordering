using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain._Shared.Documents;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class AncillaryExchangeBeneficiaryEvidenceTests
    {
        private const long BeneficiaryId = 9_700_201L;
        private const string SuccessorTicketDocumentNumber = "T9700201";
        private const int CurrencyId = 1;
        private const decimal Value = 60_000m;

        [Fact]
        public void A_confirmed_successor_that_omits_the_expected_beneficiary_is_a_contradiction()
            => Assert.Equal(
                "the confirmed successor carries no beneficiary",
                Contradiction(expected: BeneficiaryId, returned: null));

        [Fact]
        public void A_confirmed_successor_naming_another_beneficiary_is_a_contradiction()
            => Assert.Contains(
                "names beneficiary",
                Contradiction(expected: BeneficiaryId, returned: BeneficiaryId + 1)!,
                StringComparison.Ordinal);

        [Fact]
        public void A_confirmed_successor_naming_the_expected_beneficiary_is_accepted()
            => Assert.Null(Contradiction(expected: BeneficiaryId, returned: BeneficiaryId));

        [Fact]
        public void A_null_expected_beneficiary_still_accepts_a_null_result()
            => Assert.Null(Contradiction(expected: null, returned: null));

        private static string? Contradiction(long? expected, long? returned)
        {
            var group = new AcceptedExchangeAncillaryExchangeGroup(
                "EMDX-1",
                9_800_201L,
                "M9700201",
                [1],
                ElectronicMiscDocumentType.Associated,
                "A",
                CurrencyId,
                [
                    new AcceptedExchangeAncillarySuccessorCoupon(
                        EmdCouponPurpose.Fee,
                        "0DF",
                        Value,
                        CurrencyId,
                        1,
                        555_001L)
                ],
                "ANC-DISPOSITION",
                "ANC-EXCHANGE-SOURCE",
                PricingSource.Supplier,
                Array.Empty<AcceptedRefundPricingLine>());

            var successor = new SuccessorEmdIdentity(
                "M9700201X",
                ElectronicMiscDocumentType.Associated,
                11L,
                null,
                DocumentAuthority.Local,
                "A",
                CurrencyId,
                [
                    new SuccessorEmdCouponIdentity(
                        1, EmdCouponPurpose.Fee, "0DF", Value, CurrencyId, 1)
                ],
                returned,
                SuccessorTicketDocumentNumber);

            return AncillaryExchangeEvidencePolicy.Contradiction(
                group,
                SuccessorTicketDocumentNumber,
                [1],
                expected,
                new EmdExchangeResult(ProviderOperationOutcome.Confirmed, "EMDX-REF", successor));
        }
    }
}
