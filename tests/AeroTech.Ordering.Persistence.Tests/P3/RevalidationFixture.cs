using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal static class RevalidationFixture
    {
        public const string SourceSystem = "AirPrice";
        public const string QuotedChangeId = "CHG-REVALIDATION-1";
        public const string TargetSelectionRef = "AIRPRICE-REVALIDATION-TARGET-1";
        public const string ReplacementRef = "REVALIDATION-REPLACEMENT-1";
        public const string ReplacementSegmentRef = "REVALIDATION-REPLACEMENT-SEG-1";
        public const string ReplacementFlightNumber = "W5 9911";
        public const string ReplacementBookingClass = "V";
        public const long ReplacementCapacityReference = 552_211L;

        public static async Task<long> RevalidateAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness harness,
            IssuedTicket issued,
            int couponNumber)
        {
            var order = await ReloadAsync(fixture, issued.OrderId);
            var ticket = await TicketAsync(fixture, issued.OrderId, issued.TicketId);
            var coupon = ticket.Coupons.Single(candidate => candidate.CouponNumber == couponNumber);
            var quote = Quote(order, ticket.Id, coupon);

            harness.ChangeQuotes.Quote(quote, Accepted(quote));

            var outcome = await harness.VoluntaryChange.ChangeAsync(new VoluntaryChangeExecution(
                order.Id,
                coupon.CurrentOrderServiceId,
                QuotedChangeId,
                NewKey(),
                order.CommercialVersion));

            return outcome.ReplacementOrderServiceId!.Value;
        }

        private static ChangeQuote Quote(Order order, long ticketId, TicketCoupon coupon)
            => new(
                SourceSystem,
                QuotedChangeId,
                TargetSelectionRef,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                order.CurrencyId,
                ticketId,
                coupon.CurrentOrderServiceId,
                coupon.Id,
                order.OrderServices
                    .Where(service => service.Id != coupon.CurrentOrderServiceId)
                    .Select(service => service.Id)
                    .ToList(),
                Replacement(order, coupon.CurrentOrderServiceId),
                ChangeMonetaryOutcome.Even,
                DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedVoluntaryChange Accepted(ChangeQuote quote)
            => new(
                quote.SourceSystem,
                quote.QuotedChangeId,
                quote.TargetSelectionRef,
                quote.PricingSource,
                quote.OrderId,
                quote.ExpectedCommercialVersion,
                quote.SaleCurrencyId,
                quote.ElectronicTicketId,
                quote.ReplacedOrderServiceId,
                quote.ReplacedTicketCouponId,
                quote.ContinuedOrderServiceIds,
                quote.Replacement,
                quote.MonetaryOutcome,
                quote.ExpiresAt);

        private static AcceptedChangeReplacement Replacement(Order order, long orderServiceId)
        {
            var service = order.OrderServices.Single(candidate => candidate.Id == orderServiceId);
            var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

            return new AcceptedChangeReplacement(
                ReplacementRef,
                service.ServiceCode,
                service.Name,
                new AcceptedSegment(
                    ReplacementSegmentRef,
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
                    segment.DepartureDateTime.AddDays(1),
                    segment.ArrivalDateTime.AddDays(1),
                    segment.Duration,
                    segment.AircraftId,
                    segment.CabinClassId,
                    segment.RbdId,
                    ReplacementBookingClass,
                    segment.BookingClassCode,
                    ReplacementCapacityReference,
                    segment.AirFareId,
                    []),
                new AcceptedAirTransportDetail(ReplacementSegmentRef),
                service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList());
        }
    }
}
