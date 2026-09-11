using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.Ports.Exchange;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class ExchangeSourceFactory
    {
        public const string SourceSystem = "AirPrice";
        public const string QuoteId = "EXC-QUOTE-1";
        public const string TargetRef = "AIRPRICE-EXCHANGE-TARGET-1";
        public const string PricingReference = "AIRPRICE-EXCHANGE-PRICING-1";
        public const string TransferGroup = "XFER-1";
        public const string SuccessorFareBasis = "YOW";
        public const long ReplacementCapacityReference = 987_654L;
        public const string ReplacementBookingClass = "Q";
        public const string ReplacementFlightNumber = "W5 1236";
        public const decimal AddCollectAmount = 250_000m;
        public const decimal NegativeBalanceAmount = 180_000m;
        public const string FundingMethodRef = "FOP-CONTRACT-1";
        public const string ResidualDisposition = "ResidualCredit";

        public static string OutRef(string correlationRef) => $"EXC:OUT:{correlationRef}";

        public static string InRef(string correlationRef) => $"EXC:IN:{correlationRef}";

        public static AcceptedExchange Compose(
            ExchangeQuoteRequest request,
            IReadOnlyDictionary<long, AcceptedChangeReplacement> replacements,
            ChangeMonetaryOutcome monetaryOutcome = ChangeMonetaryOutcome.Even,
            string quotedExchangeId = QuoteId,
            decimal addCollectAmount = AddCollectAmount,
            decimal settlementAmount = NegativeBalanceAmount)
        {
            var reissued = request.ExchangeScope.Select(coupon => coupon.CouponNumber).ToHashSet();
            var carried = request.PredecessorPricing.Where(evidence => reissued.Contains(evidence.CouponNumber)).ToList();
            var lines = EvenTransferLines(carried);

            var coupons = request.ExchangeScope
                .OrderBy(coupon => coupon.CouponNumber)
                .Select(coupon => new AcceptedExchangeCoupon(
                    coupon.PredecessorTicketCouponId,
                    coupon.CouponNumber,
                    coupon.CurrentOrderServiceId,
                    coupon.ServiceIsChanging ? ExchangeCouponDisposition.Replaced : ExchangeCouponDisposition.Continued,
                    coupon.ServiceIsChanging ? replacements[coupon.CurrentOrderServiceId] : null,
                    SuccessorCoupon(lines, carried, coupon.CouponNumber, request.SaleCurrencyId)))
                .ToList();

            var addCollect = monetaryOutcome == ChangeMonetaryOutcome.AddCollect
                ? new AcceptedAddCollect(addCollectAmount, request.SaleCurrencyId)
                : null;

            var refundDue = monetaryOutcome == ChangeMonetaryOutcome.Refund
                ? new AcceptedRefundDue(
                    settlementAmount, request.SaleCurrencyId, AcceptedRefundDue.OriginalFormOfPayment)
                : null;

            var residual = monetaryOutcome == ChangeMonetaryOutcome.Residual
                ? new AcceptedResidual(
                    settlementAmount, request.SaleCurrencyId, ResidualDisposition, ResidualInstrumentKind.Mco)
                : null;

            var pricingLines = monetaryOutcome switch
            {
                ChangeMonetaryOutcome.AddCollect => [.. lines, PenaltyLine(request.SaleCurrencyId, addCollect!.Amount)],
                ChangeMonetaryOutcome.Refund => [.. lines, ReturnedValueLine(request.SaleCurrencyId, refundDue!.Amount)],
                ChangeMonetaryOutcome.Residual => [.. lines, ReturnedValueLine(request.SaleCurrencyId, residual!.Amount)],
                _ => lines
            };

            return new AcceptedExchange(
                SourceSystem,
                quotedExchangeId,
                TargetRef,
                PricingSource.PricingEngine,
                request.OrderId,
                request.CommercialVersion,
                request.SaleCurrencyId,
                request.PredecessorElectronicTicketId,
                request.ChangedOrderServiceIds,
                coupons,
                monetaryOutcome,
                pricingLines,
                DateTimeOffset.UtcNow.AddHours(1),
                PricingReference,
                addCollect,
                refundDue,
                residual);
        }

        public static IReadOnlyList<AcceptedExchangePricingLine> EvenTransferLines(
            IReadOnlyList<PredecessorPricingEvidence> evidence)
        {
            var lines = new List<AcceptedExchangePricingLine>();

            foreach (var carried in evidence.OrderBy(item => item.CouponNumber).ThenBy(item => item.CorrelationRef, StringComparer.Ordinal))
            {
                lines.Add(TransferLine(carried, OrderPricingLineDirection.Credit, OutRef(carried.CorrelationRef)));
                lines.Add(TransferLine(carried, OrderPricingLineDirection.Debit, InRef(carried.CorrelationRef)));
            }

            return lines;
        }

        public static AcceptedExchangePricingLine TransferLine(
            PredecessorPricingEvidence carried,
            OrderPricingLineDirection direction,
            string sourceLineRef)
            => new(
                carried.ComponentType,
                PricingEffect.CustomerBalance,
                direction,
                PricingLineRole.Transfer,
                carried.SaleAmount,
                carried.SaleCurrencyId,
                carried.SaleAmount,
                carried.SaleCurrencyId,
                PricingBasisType.Order,
                RefundabilityRule.Refundable,
                sourceLineRef,
                PredecessorCorrelationRef: carried.CorrelationRef,
                TransferGroupId: TransferGroup,
                Code: carried.Code);

        public static AcceptedExchangePricingLine ReturnedValueLine(int currencyId, decimal amount)
            => new(
                PricingComponentType.Adjustment,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Credit,
                PricingLineRole.Adjustment,
                amount,
                currencyId,
                amount,
                currencyId,
                PricingBasisType.Order,
                RefundabilityRule.Refundable,
                "EXC:RETURNED-VALUE",
                Code: "RFD");

        public static AcceptedExchangePricingLine PenaltyLine(int currencyId, decimal amount = 250_000m)
            => new(
                PricingComponentType.Penalty,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                amount,
                currencyId,
                amount,
                currencyId,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                "EXC:PENALTY",
                Code: "PEN");

        public static AcceptedSuccessorCoupon SuccessorCoupon(
            IReadOnlyList<AcceptedExchangePricingLine> lines,
            IReadOnlyList<PredecessorPricingEvidence> evidence,
            int couponNumber,
            int currencyId)
        {
            var carriedIn = evidence
                .Where(item => item.CouponNumber == couponNumber)
                .Select(item => lines.Single(line => line.SourceLineRef == InRef(item.CorrelationRef)))
                .ToList();

            return new AcceptedSuccessorCoupon(
                carriedIn.Sum(line => line.SaleAmount),
                SuccessorFareBasis,
                carriedIn
                    .Select(line => new SuccessorDocumentPriceLink(line.SourceLineRef, line.SaleAmount, currencyId))
                    .ToList());
        }

        public static IReadOnlyDictionary<long, AcceptedChangeReplacement> ReplacementsFor(
            Order order,
            IEnumerable<long> changedOrderServiceIds)
            => changedOrderServiceIds.ToDictionary(serviceId => serviceId, serviceId => Replacement(order, serviceId));

        public static AcceptedChangeReplacement Replacement(Order order, long orderServiceId)
        {
            var service = order.OrderServices.Single(candidate => candidate.Id == orderServiceId);
            var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

            return new AcceptedChangeReplacement(
                $"EXCHANGE-REPLACEMENT-{orderServiceId}",
                service.ServiceCode,
                service.Name,
                new AcceptedSegment(
                    $"EXCHANGE-REPLACEMENT-SEG-{orderServiceId}",
                    segment.Sequence,
                    segment.FlightId,
                    segment.FlightVersion,
                    ReplacementFlightNumber,
                    segment.OriginAirportId,
                    segment.OriginAirportTerminalId,
                    segment.DestinationAirportId,
                    segment.DestinationAirportTerminalId,
                    segment.MarketingAirlineId,
                    segment.OperatingAirlineId,
                    segment.DepartureDateTime.AddDays(2),
                    segment.ArrivalDateTime.AddDays(2),
                    segment.Duration,
                    segment.AircraftId,
                    segment.CabinClassId,
                    segment.RbdId,
                    ReplacementBookingClass,
                    segment.BookingClassCode,
                    ReplacementCapacityReference,
                    segment.AirFareId,
                    []),
                new AcceptedAirTransportDetail($"EXCHANGE-REPLACEMENT-SEG-{orderServiceId}", SuccessorFareBasis),
                service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList());
        }
    }
}
