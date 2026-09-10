using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class OrderExchangeTests
    {
        private const long OperationId = 5_001;
        private const long SuccessorTicketId = 7_701;
        private const long SuccessorCouponId = 7_702;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void Prepare_stages_one_change_without_attaching_anything()
        {
            var (order, predecessor, accepted) = Scenario();
            var changes = order.Changes.Count;
            var sets = order.PriceChangeSets.Count;
            var lines = order.PricingLines.Count;
            var services = order.OrderServices.Count;
            var events = order.GetEvents().Count();

            var staged = order.PrepareExchange(Args(accepted, predecessor), _ids, _clock);

            Assert.Equal(changes, order.Changes.Count);
            Assert.Equal(sets, order.PriceChangeSets.Count);
            Assert.Equal(lines, order.PricingLines.Count);
            Assert.Equal(services, order.OrderServices.Count);
            Assert.Equal(events, order.GetEvents().Count());
            Assert.Equal(1, order.CommercialVersion);
            Assert.Equal(accepted.PricingLines.Count, staged.PricingLineIdsBySourceRef.Count);
            Assert.All(accepted.PricingLines, line => Assert.Contains(line.SourceLineRef, staged.PricingLineIdsBySourceRef.Keys));
            Assert.Equal(SuccessorTicketId, staged.SuccessorElectronicTicketId);
            Assert.Equal(SuccessorCouponId, staged.SuccessorTicketCouponId);
        }

        [Fact]
        public void Commit_attaches_exactly_one_exchange_change_owning_the_transfer_history()
        {
            var (order, predecessor, accepted) = Scenario();
            var replacedId = accepted.PredecessorOrderServiceId;
            var financialSequence = order.FinancialSequence;
            var customerTotal = order.CustomerTotal;
            var obligationVersion = order.ObligationVersion;
            var pricingEvents = order.GetEvents().OfType<OrderPricingChanged>().Count();

            var staged = order.PrepareExchange(Args(accepted, predecessor), _ids, _clock);
            var exchanged = order.CommitExchange(staged, _ids, _clock);

            var change = Assert.Single(order.Changes, candidate => candidate.ChangeType == OrderChangeType.Exchange);
            var set = Assert.Single(order.PriceChangeSets, candidate => candidate.ChangeId == change.Id);
            var lines = order.PricingLines.Where(line => line.PriceChangeSetId == set.Id).ToList();
            var replaced = order.OrderServices.Single(service => service.Id == replacedId);
            var replacement = order.OrderServices.Single(service => service.Id == exchanged.ReplacementOrderServiceId);

            Assert.Equal(OperationId, change.OperationId);
            Assert.Equal(PriceChangeReason.Exchange, set.Reason);
            Assert.Equal(PricingSource.PricingEngine, set.Source);
            Assert.Equal(financialSequence + 1, order.FinancialSequence);
            Assert.Equal(2, order.CommercialVersion);
            Assert.Equal(customerTotal, order.CustomerTotal);
            Assert.Equal(obligationVersion, order.ObligationVersion);
            Assert.All(lines, line => Assert.Equal(PricingLineRole.Transfer, line.LineRole));
            Assert.Equal(0m, lines.Sum(line => line.SignedSaleAmount));
            Assert.Equal(pricingEvents + 1, order.GetEvents().OfType<OrderPricingChanged>().Count());

            Assert.Equal(OrderServiceDocumentStatus.Exchanged, replaced.DocumentStatus);
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, replaced.CommercialStatus);
            Assert.Equal(predecessor.Id, replaced.ElectronicTicketId);
            Assert.Equal(SuccessorTicketId, replacement.ElectronicTicketId);
            Assert.Equal(SuccessorCouponId, replacement.TicketCouponId);
            Assert.Equal(OrderServiceDocumentStatus.Issued, replacement.DocumentStatus);
            Assert.Equal(exchanged.PriceChangeSetId, set.Id);
            Assert.Equal(lines.Select(line => line.Id).Order(), exchanged.PricingLineIdsBySourceRef.Values.Order());
        }

        [Fact]
        public void A_transfer_naming_a_line_outside_the_predecessor_document_is_refused()
        {
            var (order, predecessor, accepted) = Scenario();
            var foreign = order.PricingLines.First(line => !predecessor.CarriedPricingLineIds().Contains(line.Id));
            var shaped = accepted with
            {
                PricingLines = accepted.PricingLines.Select((line, index) => index == 0 ? line with { OriginalPricingLineId = foreign.Id } : line).ToList()
            };

            var refusal = Assert.Throws<BusinessException>(() => order.PrepareExchange(Args(shaped, predecessor), _ids, _clock));

            Assert.Equal(2982, refusal.Code);
        }

        [Theory]
        [InlineData("no-transfer", 2981)]
        [InlineData("no-group", 2981)]
        [InlineData("duplicate-ref", 2981)]
        [InlineData("ordering-derived", 2981)]
        [InlineData("manual", 2981)]
        [InlineData("unresolved-attribution", 2983)]
        [InlineData("foreign-currency-attribution", 2981)]
        public void Malformed_exchange_pricing_is_refused_by_policy(string shape, int code)
        {
            var (_, _, accepted) = Scenario();
            var shaped = shape switch
            {
                "no-transfer" => accepted with { PricingLines = accepted.PricingLines.Select(line => line with { LineRole = PricingLineRole.Original }).ToList() },
                "no-group" => accepted with { PricingLines = accepted.PricingLines.Select(line => line with { TransferGroupId = " " }).ToList() },
                "duplicate-ref" => accepted with { PricingLines = accepted.PricingLines.Select(line => line with { SourceLineRef = "SAME" }).ToList() },
                "ordering-derived" => accepted with { PricingSource = PricingSource.OrderingDerived },
                "manual" => accepted with { PricingSource = PricingSource.Manual },
                "unresolved-attribution" => accepted with { SuccessorCoupon = accepted.SuccessorCoupon with { PriceLinks = [new SuccessorDocumentPriceLink("MISSING", 1m, accepted.SaleCurrencyId)] } },
                _ => accepted with { SuccessorCoupon = accepted.SuccessorCoupon with { PriceLinks = accepted.SuccessorCoupon.PriceLinks.Select(link => link with { CurrencyId = 99 }).ToList() } }
            };

            var refusal = Assert.Throws<BusinessException>(() => ExchangePricingPolicy.EnsureWellFormed(shaped));

            Assert.Equal(code, refusal.Code);
        }

        [Theory]
        [InlineData("add-collect", "AddCollect")]
        [InlineData("penalty", "Penalty")]
        [InlineData("fee", "Fee")]
        [InlineData("unbalanced", "NonZeroCustomerBalance")]
        public void Unsupported_monetary_shapes_are_deferred_not_transformed(string shape, string reason)
        {
            var (_, _, accepted) = Scenario();
            var currency = accepted.SaleCurrencyId;
            var shaped = shape switch
            {
                "add-collect" => accepted with { MonetaryOutcome = ChangeMonetaryOutcome.AddCollect },
                "penalty" => accepted with { PricingLines = [.. accepted.PricingLines, ExchangeSourceFactory.PenaltyLine(currency)] },
                "fee" => accepted with { PricingLines = [.. accepted.PricingLines, ExchangeSourceFactory.PenaltyLine(currency) with { ComponentType = PricingComponentType.Fee, SourceLineRef = "EXC:FEE" }] },
                _ => accepted with { PricingLines = accepted.PricingLines.Where(line => line.Direction == OrderPricingLineDirection.Debit).ToList() }
            };

            Assert.Equal(reason, ExchangePricingPolicy.DeferralReason(shaped));
            Assert.Null(ExchangePricingPolicy.DeferralReason(accepted));
        }

        [Fact]
        public void The_predecessor_records_lineage_and_the_successor_points_back()
        {
            var (order, predecessor, accepted) = Scenario();
            var coupon = Assert.Single(predecessor.Coupons);
            var version = predecessor.DocumentVersion;
            var staged = order.PrepareExchange(Args(accepted, predecessor), _ids, _clock);

            var record = predecessor.MarkExchanged(
                new ExchangeProvenance(OperationId, accepted.QuotedExchangeId, accepted.TargetSelectionRef, accepted.SourcePricingReference, "EXCH-1", 7, "test"),
                coupon.Id,
                SuccessorTicketId,
                "T999",
                SuccessorCouponId,
                1,
                staged.ReplacementOrderServiceId,
                _ids,
                _clock);

            var exchanged = order.CommitExchange(staged, _ids, _clock);
            var successor = ElectronicTicket.IssueSuccessor(Issuance(order, predecessor, accepted, exchanged), _ids, _clock);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus);
            Assert.Equal(version + 1, predecessor.DocumentVersion);
            Assert.Equal(SuccessorTicketId, record.SuccessorElectronicTicketId);
            Assert.Equal("T999", record.SuccessorDocumentNumber);
            Assert.Equal(coupon.Id, Assert.Single(record.Coupons).PredecessorTicketCouponId);
            Assert.Equal(SuccessorCouponId, Assert.Single(record.Coupons).SuccessorTicketCouponId);

            Assert.Equal(SuccessorTicketId, successor.Id);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);
            Assert.Equal(OperationId, successor.PredecessorExchangeOperationId);
            Assert.Equal(ElectronicTicketStatus.Issued, successor.StatusSummary);
            Assert.Equal(1, successor.DocumentVersion);
            Assert.Equal(coupon.Id, Assert.Single(successor.Coupons).PredecessorTicketCouponId);
            Assert.Equal(accepted.SuccessorCoupon.PriceLinks.Count, successor.PriceLinks.Count);
            Assert.All(successor.PriceLinks, link => Assert.Contains(link.PricingLineId, exchanged.PricingLineIdsBySourceRef.Values));
            Assert.All(successor.PriceLinks, link => Assert.Null(link.AllocationId));
            Assert.DoesNotContain(successor.PriceLinks, link => predecessor.CarriedPricingLineIds().Contains(link.PricingLineId));

            var again = Assert.Throws<BusinessException>(() => predecessor.EnsureCanBeExchanged(coupon.Id, coupon.CurrentOrderServiceId));

            Assert.Equal(2977, again.Code);
        }

        [Fact]
        public void A_multi_coupon_document_cannot_be_exchanged_in_this_phase()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var ticket = Ticketed(order).First();

            var refusal = Assert.Throws<BusinessException>(() => ticket.EnsureCanBeExchanged(ticket.Coupons.First().Id, ticket.Coupons.First().CurrentOrderServiceId));

            Assert.Equal(2975, refusal.Code);
        }

        // ---------------------------------------------------------------- support

        private (Order Order, ElectronicTicket Predecessor, AcceptedExchange Accepted) Scenario()
        {
            var order = MultiPassengerOrderFactory.CreateOneWay(_ids, _clock);
            var predecessor = Ticketed(order).First();
            var coupon = Assert.Single(predecessor.Coupons);

            return (order, predecessor, ExchangeSourceFactory.Accepted(order, predecessor, coupon));
        }

        private static AcceptedExchangeArgs Args(AcceptedExchange accepted, ElectronicTicket predecessor)
            => new(accepted, 6_601, 6_602, SuccessorTicketId, SuccessorCouponId, predecessor.CarriedPricingLineIds(), OperationId, 7, "test");

        private IReadOnlyList<ElectronicTicket> Ticketed(Order order)
        {
            var serviceIds = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(serviceIds, "PNR-1", null, _ids, _clock);

            var tickets = new List<ElectronicTicket>();
            var documents = new List<IssuedServiceDocument>();

            foreach (var group in order.OrderServices.Where(service => service.IsAirTransport).GroupBy(service => service.SoleBeneficiaryId).OrderBy(group => group.Key))
            {
                var ticketId = _ids.NewId();
                var coupons = group.Select(service =>
                {
                    var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);
                    var attributions = order.ServiceValueAttributions(service.Id).ToList();

                    return new TicketCouponIssuance(
                        service.Id,
                        segment.Id,
                        new IssuedSegmentSnapshot(segment.MarketingAirlineId, segment.Number, segment.OriginAirportId, segment.DestinationAirportId, segment.DepartureDateTime, segment.ArrivalDateTime, segment.BookingClass),
                        MultiPassengerOrderFactory.FareBasis,
                        attributions.Sum(attribution => attribution.SignedSaleAmount),
                        attributions.Select(attribution => new TicketCouponPriceLink(attribution.PricingLineId, attribution.AllocationId, attribution.SignedSaleAmount)).ToList());
                }).ToList();

                var ticket = ElectronicTicket.Issue(ticketId, order.Id, group.Key, 4_000, $"T{ticketId}", 1, null, DocumentAuthority.Local, null, order.CurrencyId, coupons, _ids, _clock);

                tickets.Add(ticket);
                documents.AddRange(ticket.Coupons.Select(coupon => new IssuedServiceDocument(coupon.OrderServiceId, ticket.Id, coupon.Id)));
            }

            order.RecordIssuedDocuments(documents);
            order.CompleteTicketing(_clock);

            return tickets;
        }

        private static SuccessorTicketIssuance Issuance(Order order, ElectronicTicket predecessor, AcceptedExchange accepted, Domain.OrderAggregate.Dto.ExchangedOrder exchanged)
        {
            var segment = accepted.Replacement.Segment;

            return new SuccessorTicketIssuance(
                SuccessorTicketId,
                SuccessorCouponId,
                predecessor.Id,
                Assert.Single(predecessor.Coupons).Id,
                OperationId,
                order.Id,
                predecessor.TravelerId,
                "T999",
                1,
                1,
                null,
                DocumentAuthority.Local,
                null,
                order.CurrencyId,
                exchanged.ReplacementOrderServiceId,
                exchanged.ReplacementOrderSegmentId,
                new IssuedSegmentSnapshot(segment.MarketingAirlineId, segment.FlightNumber, segment.OriginAirportId, segment.DestinationAirportId, segment.DepartureAt, segment.ArrivalAt, segment.BookingClass),
                accepted.SuccessorCoupon.FareBasis,
                accepted.SuccessorCoupon.IssuanceValue,
                accepted.SuccessorCoupon.PriceLinks.Select(link => new TicketCouponPriceLink(exchanged.PricingLineIdsBySourceRef[link.SourceLineRef], null, link.AttributedValue)).ToList());
        }
    }
}
