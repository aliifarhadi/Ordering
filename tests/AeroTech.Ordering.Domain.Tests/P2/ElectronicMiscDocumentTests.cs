using System.Reflection;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class ElectronicMiscDocumentTests
    {
        private const long OrderId = 5_000_001;
        private const long OperationId = 900_100;
        private const long IssuerCarrierId = 77;
        private const int CurrencyId = 1;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_associated_document_is_issued_as_emd_a()
        {
            var document = Issue(ElectronicMiscDocumentType.Associated, [ServiceCoupon(associatedTicketCouponId: 900)]);

            Assert.Equal(ElectronicMiscDocumentType.Associated, document.Type);
            Assert.True(document.IsAssociated);
            Assert.Equal(900, Assert.Single(document.Coupons).AssociatedTicketCouponId);
        }

        [Fact]
        public void A_standalone_document_is_issued_as_emd_s()
        {
            var document = Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon()]);

            Assert.Equal(ElectronicMiscDocumentType.Standalone, document.Type);
            Assert.False(document.IsAssociated);
            Assert.Null(Assert.Single(document.Coupons).AssociatedTicketCouponId);
        }

        [Fact]
        public void A_document_requires_at_least_one_coupon()
        {
            var exception = Assert.Throws<BusinessException>(() => Issue(ElectronicMiscDocumentType.Standalone, []));

            Assert.Equal(2870, exception.Code);
        }

        [Fact]
        public void A_document_carries_exactly_one_reason_for_issuance_code()
        {
            var document = Issue(
                ElectronicMiscDocumentType.Standalone,
                [ServiceCoupon(subCode: "0DF"), ServiceCoupon(subCode: "0DG", orderServiceId: 4242)]);

            var reasonCodes = typeof(ElectronicMiscDocument)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Count(property => property.Name.Contains("ReasonForIssuance", StringComparison.Ordinal));

            Assert.Equal(1, reasonCodes);
            Assert.Equal("C", document.ReasonForIssuanceCode);
            Assert.Equal(["0DF", "0DG"], document.Coupons.Select(coupon => coupon.ReasonForIssuanceSubCode));
        }

        [Fact]
        public void A_blank_reason_for_issuance_code_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(
                () => Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon()], reasonForIssuanceCode: "  "));

            Assert.Equal(2871, exception.Code);
        }

        [Fact]
        public void A_blank_reason_for_issuance_sub_code_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(
                () => Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon(subCode: " ")]));

            Assert.Equal(2872, exception.Code);
        }

        [Fact]
        public void An_issued_document_is_immutable()
        {
            var writable = typeof(ElectronicMiscDocument)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .Where(property => property.Name != "RowVersion")
                .ToList();

            var mutators = typeof(ElectronicMiscDocument)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name.StartsWith("Change", StringComparison.Ordinal)
                                 || method.Name.StartsWith("Set", StringComparison.Ordinal))
                .ToList();

            Assert.Empty(writable);
            Assert.Empty(mutators);

            var couponWritable = typeof(Domain.ElectronicMiscDocumentAggregate.Entities.EmdCoupon)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .ToList();

            Assert.Empty(couponWritable);
        }

        [Fact]
        public void The_issued_total_is_the_sum_of_defensible_coupon_attribution()
        {
            var document = Issue(
                ElectronicMiscDocumentType.Standalone,
                [ServiceCoupon(value: 120_000m), ServiceCoupon(value: 80_000m, orderServiceId: 4242, subCode: "0DG")]);

            Assert.Equal(200_000m, document.IssuedTotal);
            Assert.Equal(200_000m, document.Coupons.Sum(coupon => coupon.IssuanceValue));
        }

        [Fact]
        public void Issuing_a_document_changes_no_commercial_truth()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var totalBefore = order.CustomerTotal;
            var commercialBefore = order.CommercialVersion;
            var financialBefore = order.FinancialSequence;
            var obligationBefore = order.ObligationVersion;
            var linesBefore = order.PricingLines.Count;
            var setsBefore = order.PriceChangeSets.Count;
            var changesBefore = order.Changes.Count;

            Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon()], orderId: order.Id);

            Assert.Equal(totalBefore, order.CustomerTotal);
            Assert.Equal(commercialBefore, order.CommercialVersion);
            Assert.Equal(financialBefore, order.FinancialSequence);
            Assert.Equal(obligationBefore, order.ObligationVersion);
            Assert.Equal(linesBefore, order.PricingLines.Count);
            Assert.Equal(setsBefore, order.PriceChangeSets.Count);
            Assert.Equal(changesBefore, order.Changes.Count);
        }

        [Fact]
        public void A_service_coupon_requires_the_order_service_it_documents()
        {
            var exception = Assert.Throws<BusinessException>(() => Issue(
                ElectronicMiscDocumentType.Standalone,
                [ServiceCoupon(orderServiceId: null)]));

            Assert.Equal(2875, exception.Code);
        }

        [Fact]
        public void A_service_coupon_cannot_substitute_a_pricing_line_for_the_service()
        {
            var exception = Assert.Throws<BusinessException>(() => Issue(
                ElectronicMiscDocumentType.Standalone,
                [
                    new EmdCouponIssuance(
                        EmdCouponPurpose.Service,
                        "0DF",
                        100m,
                        [],
                        OrderServiceId: null,
                        PricingLineId: 777)
                ]));

            Assert.Equal(2875, exception.Code);
        }

        [Fact]
        public void A_fee_coupon_requires_the_pricing_line_it_documents()
        {
            var exception = Assert.Throws<BusinessException>(() => Issue(
                ElectronicMiscDocumentType.Standalone,
                [new EmdCouponIssuance(EmdCouponPurpose.Fee, "0DF", 100m, [])]));

            Assert.Equal(2876, exception.Code);
        }

        [Fact]
        public void A_fee_coupon_rejects_a_fabricated_order_service()
        {
            var exception = Assert.Throws<BusinessException>(() => Issue(
                ElectronicMiscDocumentType.Standalone,
                [
                    new EmdCouponIssuance(
                        EmdCouponPurpose.Fee,
                        "0DF",
                        100m,
                        [],
                        OrderServiceId: 4242,
                        PricingLineId: 777)
                ]));

            Assert.Equal(2876, exception.Code);
        }

        [Fact]
        public void A_fee_coupon_is_valid_without_any_order_service()
        {
            var document = Issue(
                ElectronicMiscDocumentType.Standalone,
                [new EmdCouponIssuance(EmdCouponPurpose.Fee, "0DF", 25_000m, [], PricingLineId: 777)]);

            var coupon = Assert.Single(document.Coupons);

            Assert.Equal(EmdCouponPurpose.Fee, coupon.Purpose);
            Assert.Equal(777, coupon.PricingLineId);
            Assert.Null(coupon.OrderServiceId);
            Assert.Equal(25_000m, document.IssuedTotal);
        }

        [Theory]
        [InlineData(EmdCouponPurpose.Deposit)]
        [InlineData(EmdCouponPurpose.ResidualValue)]
        public void A_value_coupon_requires_an_authoritative_external_reference(EmdCouponPurpose purpose)
        {
            var exception = Assert.Throws<BusinessException>(() => Issue(
                ElectronicMiscDocumentType.Standalone,
                [new EmdCouponIssuance(purpose, "0DF", 100m, [])]));

            Assert.Equal(2877, exception.Code);
        }

        [Theory]
        [InlineData(EmdCouponPurpose.Deposit)]
        [InlineData(EmdCouponPurpose.ResidualValue)]
        public void A_value_coupon_creates_no_wallet_or_payment_state(EmdCouponPurpose purpose)
        {
            var document = Issue(
                ElectronicMiscDocumentType.Standalone,
                [new EmdCouponIssuance(purpose, "0DF", 100m, [], ExternalValueReference: "VAL-1")]);

            var coupon = Assert.Single(document.Coupons);

            Assert.Equal("VAL-1", coupon.ExternalValueReference);

            var balanceTypes = typeof(ElectronicMiscDocument).Assembly.GetTypes()
                .Where(type => type.Name.Contains("WalletBalance", StringComparison.OrdinalIgnoreCase)
                               || type.Name.Contains("StoredValueBalance", StringComparison.OrdinalIgnoreCase)
                               || type.Name.Contains("VoucherBalance", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(balanceTypes);
        }

        [Fact]
        public void An_associated_coupon_requires_a_ticket_coupon()
        {
            var exception = Assert.Throws<BusinessException>(
                () => Issue(ElectronicMiscDocumentType.Associated, [ServiceCoupon()]));

            Assert.Equal(2873, exception.Code);
        }

        [Fact]
        public void A_standalone_coupon_rejects_a_ticket_coupon_association()
        {
            var exception = Assert.Throws<BusinessException>(
                () => Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon(associatedTicketCouponId: 900)]));

            Assert.Equal(2874, exception.Code);
        }

        [Fact]
        public void Several_coupons_may_associate_with_their_own_ticket_coupons()
        {
            var document = Issue(
                ElectronicMiscDocumentType.Associated,
                [
                    ServiceCoupon(associatedTicketCouponId: 900),
                    ServiceCoupon(associatedTicketCouponId: 901, orderServiceId: 4242, subCode: "0DG")
                ]);

            Assert.Equal([900L, 901L], document.Coupons.Select(coupon => coupon.AssociatedTicketCouponId!.Value));
            Assert.Equal([1, 2], document.Coupons.Select(coupon => coupon.CouponNumber));
        }

        [Fact]
        public void A_negative_coupon_value_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(
                () => Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon(value: -1m)]));

            Assert.Equal(2885, exception.Code);
        }

        [Fact]
        public void Every_coupon_starts_open_for_use()
        {
            var document = Issue(ElectronicMiscDocumentType.Standalone, [ServiceCoupon()]);

            Assert.All(document.Coupons, coupon => Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status));
            Assert.Equal(ElectronicMiscDocumentStatus.Issued, document.StatusSummary);
            Assert.Equal(1, document.DocumentVersion);
        }

        [Fact]
        public void Price_links_are_frozen_against_accepted_pricing_lines()
        {
            var document = Issue(
                ElectronicMiscDocumentType.Standalone,
                [ServiceCoupon(value: 120_000m, priceLinks: [new EmdCouponPriceLink(555, 666, 120_000m)])]);

            var link = Assert.Single(document.PriceLinks);

            Assert.Equal(555, link.PricingLineId);
            Assert.Equal(666, link.AllocationId);
            Assert.Equal(120_000m, link.AttributedValue);
            Assert.Equal(Assert.Single(document.Coupons).Id, link.EmdCouponId);
        }

        [Fact]
        public void The_document_does_not_inherit_the_electronic_ticket_aggregate()
        {
            Assert.False(typeof(Domain.ElectronicTicketAggregate.ElectronicTicket)
                .IsAssignableFrom(typeof(ElectronicMiscDocument)));

            Assert.DoesNotContain(
                typeof(ElectronicMiscDocument).Assembly.GetTypes(),
                type => type.Name.Contains("TrafficDocumentV2", StringComparison.Ordinal));
        }

        private ElectronicMiscDocument Issue(
            ElectronicMiscDocumentType type,
            IReadOnlyList<EmdCouponIssuance> coupons,
            string reasonForIssuanceCode = "C",
            long orderId = OrderId)
            => ElectronicMiscDocument.Issue(
                _ids.NewId(),
                orderId,
                4_000_001,
                OperationId,
                "M0000000001",
                type,
                reasonForIssuanceCode,
                IssuerCarrierId,
                11,
                DocumentAuthority.Local,
                CurrencyId,
                coupons,
                _ids,
                _clock);

        private static EmdCouponIssuance ServiceCoupon(
            string subCode = "0DF",
            decimal value = 100_000m,
            long? orderServiceId = 4141,
            long? associatedTicketCouponId = null,
            IReadOnlyList<EmdCouponPriceLink>? priceLinks = null)
            => new(
                EmdCouponPurpose.Service,
                subCode,
                value,
                priceLinks ?? [],
                OrderServiceId: orderServiceId,
                AssociatedTicketCouponId: associatedTicketCouponId);
    }
}
