using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class ServicingFeeDocumentPolicyTests
    {
        private const string PenaltyRef = "EXC:PENALTY";
        private const string TaxRef = "EXC:PENALTY-TAX";
        private const string ServiceFeeRef = "EXC:SERVICE-FEE";
        private const string TransferRef = "EXC:IN:C1";
        private const string DocumentRef = "FEE-DOC-1";
        private const string SourceRef = "FEE-SOURCE-1";
        private const string Rfic = "C";
        private const string Rfisc = "98A";
        private const long Issuer = 7401;
        private const int Currency = 1;
        private const decimal PenaltyAmount = 250_000m;
        private const decimal TaxAmount = 45_000m;

        // ---------------------------------------------- nothing is inferred

        [Fact]
        public void A_penalty_and_fee_lines_alone_request_no_document()
        {
            var accepted = Accepted();

            Assert.Empty(ServicingFeeDocumentPolicy.Accept(accepted));
            Assert.Empty(ServicingFeeDocumentPolicy.Accept(accepted with { FeeDocuments = [] }));
        }

        // ---------------------------------------------- an explicit instruction is accepted whole

        [Fact]
        public void B_an_explicit_penalty_instruction_is_accepted_with_its_source_facts()
        {
            var accepted = WithDocuments(Accepted(), Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)));

            var document = Assert.Single(ServicingFeeDocumentPolicy.Accept(accepted));

            Assert.Equal(DocumentRef, document.DocumentReference);
            Assert.Equal(SourceRef, document.SourceReference);
            Assert.Equal(Issuer, document.IssuerCarrierId);
            Assert.Equal(Rfic, document.ReasonForIssuanceCode);
            Assert.Equal(Currency, document.CurrencyId);
            Assert.Equal(PenaltyAmount, document.TotalAmount);
            Assert.Equal(Rfisc, Assert.Single(document.Coupons).ReasonForIssuanceSubCode);
            Assert.Equal($"emd-fee:{DocumentRef}", document.LegIdentity);
            Assert.Equal($"Emd:emd-fee:{DocumentRef}", document.StockRole);

            // nothing is executed by acceptance
            Assert.Null(document.AllocatedDocumentNumber);
            Assert.Null(document.IssuanceOutcome);
            Assert.Null(document.ElectronicMiscDocumentId);
            Assert.Null(document.SettledAt);
            Assert.False(document.IsSettled);
        }

        [Fact]
        public void C_a_fee_component_is_documentable_exactly_like_a_penalty()
        {
            var accepted = WithDocuments(
                Accepted(penaltyComponent: PricingComponentType.Fee),
                Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)));

            Assert.Equal(PenaltyAmount, Assert.Single(ServicingFeeDocumentPolicy.Accept(accepted)).TotalAmount);
        }

        [Fact]
        public void D_an_explicit_tax_attribution_is_carried_without_being_calculated()
        {
            var accepted = WithDocuments(
                Accepted(withTax: true),
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        PenaltyAmount + TaxAmount,
                        [
                            new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount),
                            new AcceptedServicingFeeAttribution(TaxRef, TaxAmount)
                        ])));

            var document = Assert.Single(ServicingFeeDocumentPolicy.Accept(accepted));
            var coupon = Assert.Single(document.Coupons);

            Assert.Equal(PenaltyAmount + TaxAmount, coupon.DocumentedAmount);
            Assert.Equal(2, coupon.Attributions.Count);
            Assert.Equal(PenaltyAmount + TaxAmount, document.TotalAmount);
        }

        [Fact]
        public void E_multiple_documents_are_accepted_in_a_deterministic_order()
        {
            var accepted = WithDocuments(
                Accepted(extraLines: [Line(PricingComponentType.Fee, sourceLineRef: ServiceFeeRef)]),
                Document("FEE-DOC-9", Coupon(ServiceFeeRef, PenaltyAmount)),
                Document("FEE-DOC-1", Coupon(PenaltyRef, PenaltyAmount)));

            Assert.Equal(
                ["FEE-DOC-1", "FEE-DOC-9"],
                ServicingFeeDocumentPolicy.Accept(accepted).Select(document => document.DocumentReference).ToList());
        }

        [Fact]
        public void F_one_line_may_fund_two_documents_while_it_stays_within_its_accepted_amount()
        {
            var accepted = WithDocuments(
                Accepted(),
                Document("FEE-DOC-1", Coupon(PenaltyRef, PenaltyAmount - 1m)),
                Document("FEE-DOC-2", Coupon(PenaltyRef, 1m)));

            Assert.Equal(2, ServicingFeeDocumentPolicy.Accept(accepted).Count);
        }

        // ---------------------------------------------- the document-shape matrix

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void G_a_blank_document_reference_is_refused(string reference)
            => AssertRefused(20324, Document(reference, Coupon(PenaltyRef, PenaltyAmount)));

        [Fact]
        public void H_a_duplicate_document_reference_is_refused()
            => AssertRefused(
                20324,
                Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)),
                Document(DocumentRef, Coupon(PenaltyRef, 1m)));

        [Fact]
        public void I_a_blank_source_reference_is_refused()
            => AssertRefused(
                20324,
                Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)) with { SourceReference = " " });

        [Fact]
        public void J_a_document_with_no_coupon_is_refused()
            => AssertRefused(
                20324,
                new AcceptedServicingFeeDocument(DocumentRef, SourceRef, Issuer, null, Rfic, Currency, []));

        [Fact]
        public void K_a_blank_reason_for_issuance_code_is_refused()
            => AssertRefused(
                20324,
                Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)) with { ReasonForIssuanceCode = " " });

        [Fact]
        public void L_a_blank_reason_for_issuance_sub_code_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        " ",
                        PenaltyRef,
                        PenaltyAmount,
                        [new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount)])));

        [Fact]
        public void M_an_invalid_issuer_is_refused()
            => AssertRefused(
                20324, Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)) with { IssuerCarrierId = 0 });

        [Fact]
        public void N_an_invalid_traveller_is_refused()
            => AssertRefused(
                20324, Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)) with { TravelerId = 0 });

        [Fact]
        public void O_a_missing_currency_is_refused()
            => AssertRefused(20324, Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)) with { CurrencyId = 0 });

        [Fact]
        public void P_a_coupon_with_no_primary_line_reference_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        " ",
                        PenaltyAmount,
                        [new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount)])));

        [Fact]
        public void Q_a_coupon_with_no_attribution_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(Rfisc, PenaltyRef, PenaltyAmount, [])));

        [Fact]
        public void R_a_primary_line_absent_from_its_own_attributions_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        TaxAmount,
                        [new AcceptedServicingFeeAttribution(TaxRef, TaxAmount)])),
                withTax: true);

        [Fact]
        public void S_a_non_positive_documented_amount_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc, PenaltyRef, 0m, [new AcceptedServicingFeeAttribution(PenaltyRef, 0m)])));

        [Fact]
        public void T_a_non_positive_attribution_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        PenaltyAmount,
                        [
                            new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount + 10m),
                            new AcceptedServicingFeeAttribution(TaxRef, -10m)
                        ])),
                withTax: true);

        [Fact]
        public void U_repeating_one_line_inside_a_coupon_is_refused()
            => AssertRefused(
                20324,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        PenaltyAmount,
                        [
                            new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount - 1m),
                            new AcceptedServicingFeeAttribution(PenaltyRef, 1m)
                        ])));

        [Fact]
        public void V_repeating_one_primary_line_across_coupons_is_refused()
            => AssertRefused(
                20324,
                new AcceptedServicingFeeDocument(
                    DocumentRef,
                    SourceRef,
                    Issuer,
                    null,
                    Rfic,
                    Currency,
                    [Coupon(PenaltyRef, PenaltyAmount - 1m), Coupon(PenaltyRef, 1m)]));

        [Fact]
        public void W_an_ordering_derived_pricing_source_is_refused()
        {
            var accepted = WithDocuments(
                Accepted() with { PricingSource = PricingSource.OrderingDerived },
                Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)));

            Assert.Equal(20324, Assert.Throws<BusinessException>(() => ServicingFeeDocumentPolicy.Accept(accepted)).Code);
        }

        // ---------------------------------------------- the pricing-line eligibility matrix

        [Fact]
        public void X_a_line_outside_the_accepted_pricing_result_is_refused()
            => AssertRefused(20325, Document(DocumentRef, Coupon("EXC:NOT-A-LINE", PenaltyAmount)));

        [Fact]
        public void Y_a_tax_only_primary_line_is_refused()
            => AssertRefused(20325, Document(DocumentRef, Coupon(TaxRef, TaxAmount)), withTax: true);

        [Fact]
        public void Z_a_credit_primary_line_is_refused()
            => AssertRefusedAgainst(
                20325,
                Line(PricingComponentType.Penalty, direction: OrderPricingLineDirection.Credit));

        [Fact]
        public void AA_a_settlement_only_primary_line_is_refused()
            => AssertRefusedAgainst(20325, Line(PricingComponentType.Fee, effect: PricingEffect.SettlementOnly));

        [Fact]
        public void AB_an_informational_primary_line_is_refused()
            => AssertRefusedAgainst(20325, Line(PricingComponentType.Fee, effect: PricingEffect.Informational));

        [Fact]
        public void AC_a_transfer_role_primary_line_is_refused()
            => AssertRefusedAgainst(20325, Line(PricingComponentType.Fee, role: PricingLineRole.Transfer));

        [Theory]
        [InlineData(PricingComponentType.Commission)]
        [InlineData(PricingComponentType.Discount)]
        [InlineData(PricingComponentType.Fare)]
        [InlineData(PricingComponentType.Adjustment)]
        public void AD_a_forbidden_component_type_is_refused(PricingComponentType component)
            => AssertRefusedAgainst(20325, Line(component));

        [Fact]
        public void AE_a_forbidden_additional_attribution_component_is_refused()
        {
            var extra = Line(PricingComponentType.Commission, sourceLineRef: "EXC:COMMISSION");

            var accepted = WithDocuments(
                Accepted(extraLines: [extra]),
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        PenaltyAmount + 1m,
                        [
                            new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount),
                            new AcceptedServicingFeeAttribution("EXC:COMMISSION", 1m)
                        ])));

            Assert.Equal(20325, Assert.Throws<BusinessException>(() => ServicingFeeDocumentPolicy.Accept(accepted)).Code);
        }

        [Fact]
        public void AF_an_attribution_in_another_currency_is_refused()
        {
            var foreign = Line(PricingComponentType.Tax, sourceLineRef: "EXC:FOREIGN-TAX", currencyId: 2);

            var accepted = WithDocuments(
                Accepted(extraLines: [foreign]),
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        PenaltyAmount + TaxAmount,
                        [
                            new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount),
                            new AcceptedServicingFeeAttribution("EXC:FOREIGN-TAX", TaxAmount)
                        ])));

            Assert.Equal(20325, Assert.Throws<BusinessException>(() => ServicingFeeDocumentPolicy.Accept(accepted)).Code);
        }

        [Fact]
        public void AG_a_document_currency_that_is_not_the_line_currency_is_refused()
            => AssertRefused(
                20325, Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)) with { CurrencyId = 99 });

        [Fact]
        public void AH_a_duplicated_source_line_ref_in_the_accepted_result_is_refused()
        {
            var duplicate = Line(PricingComponentType.Penalty);

            var accepted = WithDocuments(
                Accepted(extraLines: [duplicate]),
                Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount)));

            Assert.Equal(20325, Assert.Throws<BusinessException>(() => ServicingFeeDocumentPolicy.Accept(accepted)).Code);
        }

        // ---------------------------------------------- the conservation matrix

        [Fact]
        public void AI_attributions_that_do_not_sum_to_the_documented_amount_are_refused()
            => AssertRefused(
                20326,
                Document(
                    DocumentRef,
                    new AcceptedServicingFeeDocumentCoupon(
                        Rfisc,
                        PenaltyRef,
                        PenaltyAmount,
                        [new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount - 1m)])));

        [Fact]
        public void AJ_over_attributing_one_accepted_line_is_refused()
            => AssertRefused(20326, Document(DocumentRef, Coupon(PenaltyRef, PenaltyAmount + 1m)));

        [Fact]
        public void AK_over_attributing_one_line_across_two_documents_is_refused()
            => AssertRefused(
                20326,
                Document("FEE-DOC-1", Coupon(PenaltyRef, PenaltyAmount)),
                Document("FEE-DOC-2", Coupon(PenaltyRef, 1m)));

        // ---------------------------------------------- helpers

        private static void AssertRefused(
            int expectedCode,
            AcceptedServicingFeeDocument document,
            bool withTax = false)
            => Assert.Equal(
                expectedCode,
                Assert.Throws<BusinessException>(
                    () => ServicingFeeDocumentPolicy.Accept(
                        WithDocuments(Accepted(withTax: withTax), document))).Code);

        private static void AssertRefused(
            int expectedCode,
            AcceptedServicingFeeDocument first,
            AcceptedServicingFeeDocument second)
            => Assert.Equal(
                expectedCode,
                Assert.Throws<BusinessException>(
                    () => ServicingFeeDocumentPolicy.Accept(
                        WithDocuments(Accepted(), first, second))).Code);

        private static void AssertRefusedAgainst(int expectedCode, AcceptedExchangePricingLine primary)
        {
            var accepted = WithDocuments(
                Accepted(extraLines: [primary with { SourceLineRef = "EXC:CANDIDATE" }]),
                Document(DocumentRef, Coupon("EXC:CANDIDATE", primary.SaleAmount)));

            Assert.Equal(
                expectedCode,
                Assert.Throws<BusinessException>(() => ServicingFeeDocumentPolicy.Accept(accepted)).Code);
        }

        private static AcceptedExchange WithDocuments(
            AcceptedExchange accepted,
            params AcceptedServicingFeeDocument[] documents)
            => accepted with { FeeDocuments = documents };

        private static AcceptedServicingFeeDocument Document(
            string documentReference,
            AcceptedServicingFeeDocumentCoupon coupon)
            => new(documentReference, SourceRef, Issuer, null, Rfic, Currency, [coupon]);

        private static AcceptedServicingFeeDocumentCoupon Coupon(string sourceLineRef, decimal amount)
            => new(Rfisc, sourceLineRef, amount, [new AcceptedServicingFeeAttribution(sourceLineRef, amount)]);

        private static AcceptedExchange Accepted(
            PricingComponentType penaltyComponent = PricingComponentType.Penalty,
            bool withTax = false,
            IReadOnlyList<AcceptedExchangePricingLine>? extraLines = null)
        {
            var lines = new List<AcceptedExchangePricingLine>
            {
                Line(penaltyComponent),
                Line(
                    PricingComponentType.Fare,
                    role: PricingLineRole.Transfer,
                    sourceLineRef: TransferRef,
                    amount: 1_000_000m)
            };

            if (withTax)
                lines.Add(Line(PricingComponentType.Tax, sourceLineRef: TaxRef, amount: TaxAmount));

            if (extraLines is not null)
                lines.AddRange(extraLines);

            return new AcceptedExchange(
                "PricingEngine",
                "QOFFER-1",
                "TARGET-1",
                PricingSource.PricingEngine,
                1,
                1,
                Currency,
                1,
                [],
                [],
                ChangeMonetaryOutcome.AddCollect,
                lines,
                DateTimeOffset.UtcNow.AddHours(1));
        }

        private static AcceptedExchangePricingLine Line(
            PricingComponentType component,
            PricingEffect effect = PricingEffect.CustomerBalance,
            OrderPricingLineDirection direction = OrderPricingLineDirection.Debit,
            PricingLineRole role = PricingLineRole.Original,
            string sourceLineRef = PenaltyRef,
            decimal amount = PenaltyAmount,
            int currencyId = Currency)
            => new(
                component,
                effect,
                direction,
                role,
                amount,
                currencyId,
                amount,
                currencyId,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                sourceLineRef,
                TransferGroupId: role == PricingLineRole.Transfer ? "TG-1" : null,
                Code: "PEN");
    }
}
