using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

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

        public static string OutRef(long pricingLineId) => $"EXC:OUT:{pricingLineId}";

        public static string InRef(long pricingLineId) => $"EXC:IN:{pricingLineId}";

        public static AcceptedExchange Accepted(
            Order order,
            ElectronicTicket predecessor,
            TicketCoupon coupon,
            ChangeMonetaryOutcome monetaryOutcome = ChangeMonetaryOutcome.Even)
        {
            var lines = EvenTransferLines(order, predecessor, coupon);

            return new AcceptedExchange(
                SourceSystem,
                QuoteId,
                TargetRef,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                order.CurrencyId,
                predecessor.Id,
                coupon.CurrentOrderServiceId,
                coupon.Id,
                order.OrderServices
                    .Where(service => service.Id != coupon.CurrentOrderServiceId)
                    .Select(service => service.Id)
                    .ToList(),
                Replacement(order, coupon),
                monetaryOutcome,
                lines,
                SuccessorCoupon(lines, order.CurrencyId),
                DateTimeOffset.UtcNow.AddHours(1),
                PricingReference);
        }

        public static ExchangeQuote ToQuote(AcceptedExchange accepted)
            => new(
                accepted.SourceSystem,
                accepted.QuotedExchangeId,
                accepted.TargetSelectionRef,
                accepted.PricingSource,
                accepted.OrderId,
                accepted.ExpectedCommercialVersion,
                accepted.SaleCurrencyId,
                accepted.PredecessorElectronicTicketId,
                accepted.PredecessorOrderServiceId,
                accepted.PredecessorTicketCouponId,
                accepted.ContinuedOrderServiceIds,
                accepted.Replacement,
                accepted.MonetaryOutcome,
                accepted.PricingLines,
                accepted.SuccessorCoupon,
                accepted.ExpiresAt,
                accepted.SourcePricingReference);

        public static IReadOnlyList<AcceptedExchangePricingLine> EvenTransferLines(
            Order order,
            ElectronicTicket predecessor,
            TicketCoupon coupon)
        {
            var carried = predecessor.CarriedPricingLineIds();
            var lines = new List<AcceptedExchangePricingLine>();

            foreach (var line in order.PricingLines.Where(candidate => carried.Contains(candidate.Id)).OrderBy(candidate => candidate.Id))
            {
                lines.Add(TransferLine(line, OrderPricingLineDirection.Credit, OutRef(line.Id), coupon.CurrentOrderServiceId));
                lines.Add(TransferLine(line, OrderPricingLineDirection.Debit, InRef(line.Id), null));
            }

            return lines;
        }

        public static AcceptedExchangePricingLine TransferLine(
            OrderPricingLine original,
            OrderPricingLineDirection direction,
            string sourceLineRef,
            long? basisServiceId)
            => new(
                original.ComponentType,
                PricingEffect.CustomerBalance,
                direction,
                PricingLineRole.Transfer,
                original.SaleAmount,
                original.SaleCurrencyId,
                original.SaleAmount,
                original.SaleCurrencyId,
                PricingBasisType.OrderService,
                original.Refundability,
                sourceLineRef,
                BasisReferenceId: basisServiceId,
                OrderItemId: original.OrderItemId,
                OriginalPricingLineId: original.Id,
                TransferGroupId: TransferGroup,
                Code: original.Code,
                Description: original.Description,
                ApplicationLevel: original.ApplicationLevel);

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
            int currencyId)
        {
            var carriedIn = lines
                .Where(line => line.LineRole == PricingLineRole.Transfer && line.Direction == OrderPricingLineDirection.Debit)
                .ToList();

            return new AcceptedSuccessorCoupon(
                carriedIn.Sum(line => line.SaleAmount),
                SuccessorFareBasis,
                carriedIn
                    .Select(line => new SuccessorDocumentPriceLink(line.SourceLineRef, line.SaleAmount, currencyId))
                    .ToList());
        }

        public static AcceptedChangeReplacement Replacement(Order order, TicketCoupon coupon)
        {
            var service = order.OrderServices.Single(candidate => candidate.Id == coupon.CurrentOrderServiceId);
            var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

            return new AcceptedChangeReplacement(
                "EXCHANGE-REPLACEMENT-1",
                service.ServiceCode,
                service.Name,
                new AcceptedSegment(
                    "EXCHANGE-REPLACEMENT-SEG-1",
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
                new AcceptedAirTransportDetail("EXCHANGE-REPLACEMENT-SEG-1", SuccessorFareBasis),
                service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList());
        }
    }
}
