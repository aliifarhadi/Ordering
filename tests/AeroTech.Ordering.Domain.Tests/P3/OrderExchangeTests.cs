using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Policies;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class OrderExchangeTests
    {
        private const long OperationId = 5_001;
        private const long SuccessorTicketId = 7_701;
        private const long FirstSuccessorCouponId = 7_710;
        private const long FirstReplacementServiceId = 6_601;
        private const long FirstReplacementSegmentId = 6_701;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        // ---------------------------------------------------------------- staging

        [Fact]
        public void Prepare_stages_one_change_without_attaching_anything()
        {
            var scenario = Scenario(roundTrip: false, changedCouponNumbers: [1]);
            var order = scenario.Order;
            var changes = order.Changes.Count;
            var sets = order.PriceChangeSets.Count;
            var lines = order.PricingLines.Count;
            var services = order.OrderServices.Count;
            var events = order.GetEvents().Count();

            var staged = order.PrepareExchange(scenario.Args(), _ids, _clock);

            Assert.Equal(changes, order.Changes.Count);
            Assert.Equal(sets, order.PriceChangeSets.Count);
            Assert.Equal(lines, order.PricingLines.Count);
            Assert.Equal(services, order.OrderServices.Count);
            Assert.Equal(events, order.GetEvents().Count());
            Assert.Equal(1, order.CommercialVersion);
            Assert.Equal(scenario.Accepted.PricingLines.Count, staged.PricingLineIdsBySourceRef.Count);
            Assert.All(scenario.Accepted.PricingLines, line => Assert.Contains(line.SourceLineRef, staged.PricingLineIdsBySourceRef.Keys));
            Assert.Equal(SuccessorTicketId, staged.SuccessorElectronicTicketId);
            Assert.Equal(FirstSuccessorCouponId, Assert.Single(staged.Coupons).SuccessorTicketCouponId);
        }

        [Fact]
        public void Commit_attaches_exactly_one_exchange_change_owning_the_transfer_history()
        {
            var scenario = Scenario(roundTrip: false, changedCouponNumbers: [1]);
            var order = scenario.Order;
            var replacedId = scenario.ChangedOrderServiceIds.Single();
            var financialSequence = order.FinancialSequence;
            var customerTotal = order.CustomerTotal;
            var obligationVersion = order.ObligationVersion;
            var pricingEvents = order.GetEvents().OfType<OrderPricingChanged>().Count();

            var staged = order.PrepareExchange(scenario.Args(), _ids, _clock);
            var exchanged = order.CommitExchange(staged, _ids, _clock);

            var change = Assert.Single(order.Changes, candidate => candidate.ChangeType == OrderChangeType.Exchange);
            var set = Assert.Single(order.PriceChangeSets, candidate => candidate.ChangeId == change.Id);
            var lines = order.PricingLines.Where(line => line.PriceChangeSetId == set.Id).ToList();
            var binding = Assert.Single(exchanged.Coupons);
            var replaced = order.OrderServices.Single(service => service.Id == replacedId);
            var replacement = order.OrderServices.Single(service => service.Id == binding.OrderServiceId);

            Assert.Equal(OperationId, change.OperationId);
            Assert.Equal(PriceChangeReason.Exchange, set.Reason);
            Assert.Equal(PricingSource.PricingEngine, set.Source);
            Assert.Equal(financialSequence + 1, order.FinancialSequence);
            Assert.Equal(2, order.CommercialVersion);
            Assert.Equal(customerTotal, order.CustomerTotal);
            Assert.Equal(obligationVersion, order.ObligationVersion);
            Assert.All(lines, line => Assert.Equal(PricingLineRole.Transfer, line.LineRole));
            Assert.All(lines, line => Assert.Contains(line.OriginalPricingLineId!.Value, scenario.Predecessor.CarriedPricingLineIds()));
            Assert.Equal(0m, lines.Sum(line => line.SignedSaleAmount));
            Assert.Equal(pricingEvents + 1, order.GetEvents().OfType<OrderPricingChanged>().Count());

            Assert.Equal(ExchangeCouponDisposition.Replaced, binding.Disposition);
            Assert.Equal(replacedId, binding.ReplacedOrderServiceId);
            Assert.Equal(OrderServiceDocumentStatus.Exchanged, replaced.DocumentStatus);
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, replaced.CommercialStatus);
            Assert.Equal(scenario.Predecessor.Id, replaced.ElectronicTicketId);
            Assert.Equal(SuccessorTicketId, replacement.ElectronicTicketId);
            Assert.Equal(FirstSuccessorCouponId, replacement.TicketCouponId);
            Assert.Equal(OrderServiceDocumentStatus.Issued, replacement.DocumentStatus);
            Assert.Equal(exchanged.PriceChangeSetId, set.Id);
            Assert.Equal(lines.Select(line => line.Id).Order(), exchanged.PricingLineIdsBySourceRef.Values.Order());
        }

        [Fact]
        public void A_continued_coupon_rebinds_its_existing_service_without_creating_a_replacement()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var order = scenario.Order;
            var continuedId = scenario.CouponServiceIds[2];
            var servicesBefore = order.OrderServices.Count;
            var segmentsBefore = order.Segments.Count;
            var continuedBefore = order.OrderServices.Single(service => service.Id == continuedId);
            var statusBefore = continuedBefore.Status;
            var segmentBefore = continuedBefore.SoldSegmentId;

            var exchanged = order.CommitExchange(order.PrepareExchange(scenario.Args(), _ids, _clock), _ids, _clock);

            var continued = order.OrderServices.Single(service => service.Id == continuedId);
            var continuedBinding = exchanged.Coupons.Single(binding => binding.Disposition == ExchangeCouponDisposition.Continued);
            var replacedBinding = exchanged.Coupons.Single(binding => binding.Disposition == ExchangeCouponDisposition.Replaced);

            Assert.Equal(2, exchanged.Coupons.Count);
            Assert.Equal(servicesBefore + 1, order.OrderServices.Count);
            Assert.Equal(segmentsBefore + 1, order.Segments.Count);
            Assert.Equal(continuedId, continuedBinding.OrderServiceId);
            Assert.Null(continuedBinding.ReplacedOrderServiceId);
            Assert.Equal(segmentBefore, continuedBinding.OrderSegmentId);
            Assert.Equal(statusBefore, continued.Status);
            Assert.Equal(OrderServiceDocumentStatus.Issued, continued.DocumentStatus);
            Assert.Equal(SuccessorTicketId, continued.ElectronicTicketId);
            Assert.Equal(continuedBinding.SuccessorTicketCouponId, continued.TicketCouponId);
            Assert.NotEqual(OrderServiceCommercialStatus.Exchanged, continued.CommercialStatus);
            Assert.Equal(scenario.CouponServiceIds[1], replacedBinding.ReplacedOrderServiceId);
            Assert.Equal(OrderServiceDocumentStatus.Exchanged, order.OrderServices.Single(service => service.Id == scenario.CouponServiceIds[1]).DocumentStatus);
            Assert.Single(order.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(2, order.CommercialVersion);
        }

        [Fact]
        public void Every_changed_coupon_is_superseded_exactly_once()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1, 2]);
            var order = scenario.Order;
            var servicesBefore = order.OrderServices.Count;

            var exchanged = order.CommitExchange(order.PrepareExchange(scenario.Args(), _ids, _clock), _ids, _clock);

            Assert.Equal(servicesBefore + 2, order.OrderServices.Count);
            Assert.All(exchanged.Coupons, binding => Assert.Equal(ExchangeCouponDisposition.Replaced, binding.Disposition));
            Assert.Equal(scenario.CouponServiceIds.Values.Order(), exchanged.Coupons.Select(binding => binding.ReplacedOrderServiceId!.Value).Order());
            Assert.Equal(2, order.OrderServices.Count(service => service.DocumentStatus == OrderServiceDocumentStatus.Exchanged));
            Assert.Equal(2, order.OrderServices.Count(service => service.ElectronicTicketId == SuccessorTicketId));
            Assert.Equal(exchanged.Coupons.Select(binding => binding.SuccessorTicketCouponId).Order(), scenario.Allocations.Select(allocation => allocation.SuccessorTicketCouponId).Order());
        }

        // ---------------------------------------------------------------- correlation

        [Fact]
        public void Predecessor_pricing_evidence_correlates_without_exposing_local_pricing_line_ids()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var carried = scenario.Predecessor.CarriedPricingLineIds();

            Assert.Equal(scenario.Predecessor.PriceLinks.Count, scenario.Evidence.Count);
            Assert.All(scenario.Evidence, evidence => Assert.StartsWith("XPL-", evidence.CorrelationRef));
            Assert.All(scenario.Evidence, evidence => Assert.DoesNotContain(carried, lineId => evidence.CorrelationRef.Contains(lineId.ToString())));
            Assert.All(scenario.Evidence, evidence => Assert.False(string.IsNullOrWhiteSpace(evidence.SourceLineRef)));
            Assert.Equal(scenario.Evidence.Count, scenario.Evidence.Select(evidence => evidence.CorrelationRef).Distinct().Count());
            Assert.Equal(new[] { 1, 2 }, scenario.Evidence.Select(evidence => evidence.CouponNumber).Distinct().Order());

            var map = ExchangePricingCorrelation.Map(scenario.Predecessor.Id, carried);

            Assert.All(scenario.Evidence, evidence => Assert.Contains(map[evidence.CorrelationRef], carried));
            Assert.Equal(
                ExchangePricingCorrelation.CorrelationRef(scenario.Predecessor.Id, carried.First()),
                ExchangePricingCorrelation.CorrelationRef(scenario.Predecessor.Id, carried.First()));
            Assert.NotEqual(
                ExchangePricingCorrelation.CorrelationRef(scenario.Predecessor.Id, carried.First()),
                ExchangePricingCorrelation.CorrelationRef(scenario.Predecessor.Id + 1, carried.First()));
        }

        [Fact]
        public void A_transfer_naming_evidence_outside_the_predecessor_document_is_refused()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var other = scenario.Tickets.Single(ticket => ticket.Id != scenario.Predecessor.Id);
            var foreign = ExchangePricingCorrelation.CorrelationRef(other.Id, other.CarriedPricingLineIds().First());
            var shaped = scenario.Accepted with
            {
                PricingLines = scenario.Accepted.PricingLines
                    .Select((line, index) => index == 0 ? line with { PredecessorCorrelationRef = foreign } : line)
                    .ToList()
            };

            var refusal = Assert.Throws<BusinessException>(() => scenario.Order.PrepareExchange(scenario.Args(shaped), _ids, _clock));

            Assert.Equal(20277, refusal.Code);
        }

        // ---------------------------------------------------------------- policy

        [Theory]
        [InlineData("no-transfer", 20275)]
        [InlineData("no-group", 20275)]
        [InlineData("duplicate-ref", 20275)]
        [InlineData("ordering-derived", 20275)]
        [InlineData("manual", 20275)]
        [InlineData("unresolved-attribution", 20278)]
        [InlineData("foreign-currency-attribution", 20275)]
        [InlineData("duplicate-coupon", 20275)]
        [InlineData("continued-with-replacement", 20275)]
        [InlineData("replaced-without-replacement", 20275)]
        [InlineData("changed-set-mismatch", 20275)]
        public void Malformed_exchange_pricing_is_refused_by_policy(string shape, int code)
        {
            var accepted = Scenario(roundTrip: true, changedCouponNumbers: [1]).Accepted;
            var first = accepted.Coupons[0];
            var second = accepted.Coupons[1];
            var shaped = shape switch
            {
                "no-transfer" => accepted with { PricingLines = accepted.PricingLines.Select(line => line with { LineRole = PricingLineRole.Original }).ToList() },
                "no-group" => accepted with { PricingLines = accepted.PricingLines.Select(line => line with { TransferGroupId = " " }).ToList() },
                "duplicate-ref" => accepted with { PricingLines = accepted.PricingLines.Select(line => line with { SourceLineRef = "SAME" }).ToList() },
                "ordering-derived" => accepted with { PricingSource = PricingSource.OrderingDerived },
                "manual" => accepted with { PricingSource = PricingSource.Manual },
                "unresolved-attribution" => accepted with { Coupons = [first with { Successor = first.Successor with { PriceLinks = [new SuccessorDocumentPriceLink("MISSING", 1m, accepted.SaleCurrencyId)] } }, second] },
                "foreign-currency-attribution" => accepted with { Coupons = [first with { Successor = first.Successor with { PriceLinks = first.Successor.PriceLinks.Select(link => link with { CurrencyId = 99 }).ToList() } }, second] },
                "duplicate-coupon" => accepted with { Coupons = [first, first] },
                "continued-with-replacement" => accepted with { Coupons = [first, second with { Replacement = first.Replacement }] },
                "replaced-without-replacement" => accepted with { Coupons = [first with { Replacement = null }, second] },
                _ => accepted with { ChangedOrderServiceIds = [second.PredecessorOrderServiceId] }
            };

            var refusal = Assert.Throws<BusinessException>(() => ExchangePricingPolicy.EnsureWellFormed(shaped));

            Assert.Equal(code, refusal.Code);
        }

        [Theory]
        [InlineData("mixed", "Mixed")]
        [InlineData("penalty", "Penalty")]
        [InlineData("fee", "Fee")]
        [InlineData("unbalanced", "NonZeroCustomerBalance")]
        public void Unsupported_monetary_shapes_are_deferred_not_transformed(string shape, string reason)
        {
            var accepted = Scenario(roundTrip: false, changedCouponNumbers: [1]).Accepted;
            var currency = accepted.SaleCurrencyId;
            var shaped = shape switch
            {
                "mixed" => accepted with { MonetaryOutcome = ChangeMonetaryOutcome.Mixed },
                "penalty" => accepted with { PricingLines = [.. accepted.PricingLines, ExchangeSourceFactory.PenaltyLine(currency)] },
                "fee" => accepted with { PricingLines = [.. accepted.PricingLines, ExchangeSourceFactory.PenaltyLine(currency) with { ComponentType = PricingComponentType.Fee, SourceLineRef = "EXC:FEE" }] },
                _ => accepted with { PricingLines = accepted.PricingLines.Where(line => line.Direction == OrderPricingLineDirection.Debit).ToList() }
            };

            Assert.Equal(reason, ExchangePricingPolicy.DeferralReason(shaped));
            Assert.Null(ExchangePricingPolicy.DeferralReason(accepted));
        }

        [Fact]
        public void An_add_collect_priced_by_the_provider_is_supported_and_needs_funding()
        {
            var accepted = AddCollectAccepted(ExchangeSourceFactory.AddCollectAmount);

            Assert.Null(ExchangePricingPolicy.DeferralReason(accepted));
            Assert.True(ExchangePricingPolicy.RequiresFunding(accepted));
            Assert.False(ExchangePricingPolicy.RequiresFunding(Scenario(roundTrip: false, changedCouponNumbers: [1]).Accepted));
            Assert.Equal(
                ExchangeSourceFactory.AddCollectAmount,
                ExchangePricingPolicy.NetCustomerBalance(accepted.PricingLines));

            ExchangePricingPolicy.EnsureWellFormed(accepted);
        }

        [Theory]
        [InlineData("no-amount")]
        [InlineData("zero")]
        [InlineData("negative")]
        [InlineData("foreign-currency")]
        [InlineData("contradicts-lines")]
        [InlineData("even-with-amount")]
        public void A_malformed_add_collect_amount_is_refused_by_the_pricing_policy(string shape)
        {
            var priced = AddCollectAccepted(ExchangeSourceFactory.AddCollectAmount);
            var currency = priced.SaleCurrencyId;
            var shaped = shape switch
            {
                "no-amount" => priced with { AddCollect = null },
                "zero" => priced with { AddCollect = new AcceptedAddCollect(0m, currency) },
                "negative" => priced with { AddCollect = new AcceptedAddCollect(-1m, currency) },
                "foreign-currency" => priced with { AddCollect = new AcceptedAddCollect(priced.AddCollect!.Amount, currency + 7) },
                "contradicts-lines" => priced with { AddCollect = new AcceptedAddCollect(priced.AddCollect!.Amount + 1m, currency) },
                _ => priced with
                {
                    MonetaryOutcome = ChangeMonetaryOutcome.Even,
                    PricingLines = [.. priced.PricingLines.Where(line => line.ComponentType != PricingComponentType.Penalty)]
                }
            };

            var refusal = Assert.Throws<BusinessException>(() => ExchangePricingPolicy.EnsureWellFormed(shaped));

            Assert.Equal(20275, refusal.Code);
        }

        private AcceptedExchange AddCollectAccepted(decimal amount)
        {
            var accepted = Scenario(roundTrip: false, changedCouponNumbers: [1]).Accepted;

            return accepted with
            {
                MonetaryOutcome = ChangeMonetaryOutcome.AddCollect,
                AddCollect = new AcceptedAddCollect(amount, accepted.SaleCurrencyId),
                PricingLines = [.. accepted.PricingLines, ExchangeSourceFactory.PenaltyLine(accepted.SaleCurrencyId, amount)]
            };
        }

        // ---------------------------------------------------------------- document lineage

        [Fact]
        public void The_predecessor_records_lineage_for_every_coupon_and_the_successor_points_back()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var order = scenario.Order;
            var predecessor = scenario.Predecessor;
            var version = predecessor.DocumentVersion;
            var staged = order.PrepareExchange(scenario.Args(), _ids, _clock);

            var record = predecessor.MarkExchanged(
                new ExchangeProvenance(OperationId, scenario.Accepted.QuotedExchangeId, scenario.Accepted.TargetSelectionRef, scenario.Accepted.SourcePricingReference, "EXCH-1", 7, "test"),
                SuccessorTicketId,
                "T999",
                staged.Coupons
                    .Select(coupon => new ExchangedCouponLineage(coupon.PredecessorTicketCouponId, coupon.SuccessorTicketCouponId, CouponNumberOf(predecessor, coupon.PredecessorTicketCouponId), coupon.OrderServiceId))
                    .ToList(),
                _ids,
                _clock);

            var exchanged = order.CommitExchange(staged, _ids, _clock);
            var successor = ElectronicTicket.IssueSuccessor(Issuance(order, predecessor, scenario.Accepted, exchanged), _ids, _clock);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.All(predecessor.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));
            Assert.Equal(version + 1, predecessor.DocumentVersion);
            Assert.Equal(SuccessorTicketId, record.SuccessorElectronicTicketId);
            Assert.Equal("T999", record.SuccessorDocumentNumber);
            Assert.Equal(2, record.Coupons.Count);

            foreach (var binding in exchanged.Coupons)
            {
                var mapping = Assert.Single(record.Coupons, coupon => coupon.PredecessorTicketCouponId == binding.PredecessorTicketCouponId);
                var successorCoupon = Assert.Single(successor.Coupons, coupon => coupon.Id == binding.SuccessorTicketCouponId);
                var predecessorCoupon = predecessor.Coupons.Single(coupon => coupon.Id == binding.PredecessorTicketCouponId);

                Assert.Equal(binding.SuccessorTicketCouponId, mapping.SuccessorTicketCouponId);
                Assert.Equal(predecessorCoupon.CouponNumber, mapping.SuccessorCouponNumber);
                Assert.Equal(predecessorCoupon.CurrentOrderServiceId, mapping.PreviousOrderServiceId);
                Assert.Equal(binding.OrderServiceId, mapping.SuccessorOrderServiceId);
                Assert.Equal(binding.PredecessorTicketCouponId, successorCoupon.PredecessorTicketCouponId);
                Assert.Equal(binding.OrderServiceId, successorCoupon.OrderServiceId);
                Assert.Equal(binding.OrderSegmentId, successorCoupon.JourneySegmentId);
                Assert.Equal(TicketCouponFinancialStatus.Open, successorCoupon.FinancialStatus);
            }

            Assert.Equal(SuccessorTicketId, successor.Id);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);
            Assert.Equal(OperationId, successor.PredecessorExchangeOperationId);
            Assert.Equal(ElectronicTicketStatus.Issued, successor.StatusSummary);
            Assert.Equal(1, successor.DocumentVersion);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(scenario.Accepted.Coupons.Sum(coupon => coupon.Successor.PriceLinks.Count), successor.PriceLinks.Count);
            Assert.Equal(scenario.Accepted.Coupons.Sum(coupon => coupon.Successor.IssuanceValue), successor.IssuedTotal);
            Assert.All(successor.PriceLinks, link => Assert.Contains(link.PricingLineId, exchanged.PricingLineIdsBySourceRef.Values));
            Assert.All(successor.PriceLinks, link => Assert.Null(link.AllocationId));
            Assert.DoesNotContain(successor.PriceLinks, link => predecessor.CarriedPricingLineIds().Contains(link.PricingLineId));

            var again = Assert.Throws<BusinessException>(() => ExchangeCapabilityPolicy.EnsureEligible(
                predecessor,
                predecessor.Coupons.Select(coupon => new ExchangeCouponScope(coupon.Id, coupon.CurrentOrderServiceId)).ToList()));

            Assert.Equal(20271, again.Code);
        }

        [Fact]
        public void The_reissue_scope_is_every_remaining_open_coupon_and_used_coupons_stay_historical()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var predecessor = scenario.Predecessor;
            var coupons = predecessor.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();
            var wholeDocument = coupons.Select(coupon => new ExchangeCouponScope(coupon.Id, coupon.CurrentOrderServiceId)).ToList();

            TicketCouponStatus.Fly(coupons[0]);

            var openOnly = new[] { new ExchangeCouponScope(coupons[1].Id, coupons[1].CurrentOrderServiceId) };

            Assert.Equal(
                [coupons[1].Id],
                ExchangeCapabilityPolicy.ReissueScope(predecessor).Select(coupon => coupon.Id));

            ExchangeCapabilityPolicy.EnsureEligible(predecessor, openOnly);
            predecessor.EnsureDocumentCanBeExchanged();
            predecessor.EnsureCouponsCanBeExchanged(openOnly);

            var withHistory = Assert.Throws<BusinessException>(() => ExchangeCapabilityPolicy.EnsureEligible(predecessor, wholeDocument));
            var flown = Assert.Throws<BusinessException>(() => predecessor.EnsureCouponsCanBeExchanged(wholeDocument));

            Assert.Equal(20288, withHistory.Code);
            Assert.Equal(20292, flown.Code);
            Assert.DoesNotContain("capability", flown.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TicketCouponFinancialStatus.Used, coupons[0].FinancialStatus);
            Assert.Equal(TicketCouponFinancialStatus.Open, coupons[1].FinancialStatus);
        }

        [Fact]
        public void A_stored_fare_construction_is_exposed_to_a_quote_as_provider_neutral_context()
        {
            var source = MultiPassengerOrderFactory.AcceptedSource(_clock) with
            {
                FareConstructions = [FareConstructionFactory.TrueRoundTrip("T1"), FareConstructionFactory.TrueRoundTrip("T2")]
            };
            var order = Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                _ids,
                _clock);

            var contexts = order.FareConstructionContexts();
            var context = contexts[0];
            var group = Assert.Single(context.Groups);
            var unit = Assert.Single(group.Units);

            Assert.Equal(2, contexts.Count);
            Assert.Equal(FareConstructionFactory.SourceSystem, context.SourceSystem);
            Assert.Equal(AirFareConstructionType.RoundTrip, context.ConstructionType);
            Assert.Equal(PassengerTypeCode.ADT, group.PassengerType);
            Assert.Single(group.OrderTravellerIds);
            Assert.Equal(FarePricingUnitType.RoundTrip, unit.UnitType);
            Assert.Equal(FareCombinationMethod.FiledFare, unit.CombinationMethod);
            Assert.Equal([1, 2], unit.Components.Select(component => component.Sequence));
            Assert.All(unit.Components, component =>
            {
                Assert.Equal("YRTFC", component.FareBasis);
                Assert.NotNull(component.OriginAirportId);
                Assert.NotNull(component.DestinationAirportId);
                Assert.All(
                    component.OrderServiceIds,
                    serviceId => Assert.Contains(serviceId, order.OrderServices.Select(service => service.Id)));
                Assert.All(
                    component.OrderSegmentIds,
                    segmentId => Assert.Contains(segmentId, order.Segments.Select(segment => segment.Id)));
            });
        }

        [Fact]
        public void An_order_with_no_stored_fare_construction_exposes_no_context()
            => Assert.Empty(MultiPassengerOrderFactory.Create(_ids, _clock).FareConstructionContexts());

        [Fact]
        public void A_coupon_state_outside_open_and_used_is_refused_as_a_capability_limit()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var coupons = scenario.Predecessor.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            TicketCouponStatus.Set(coupons[0], TicketCouponFinancialStatus.Refunded);

            var refusal = Assert.Throws<BusinessException>(() => ExchangeCapabilityPolicy.ReissueScope(scenario.Predecessor));

            Assert.Equal(20270, refusal.Code);
            Assert.Contains("capability", refusal.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void An_exchange_scope_must_cover_every_coupon_of_the_document_exactly_once()
        {
            var scenario = Scenario(roundTrip: true, changedCouponNumbers: [1]);
            var coupons = scenario.Predecessor.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            var partial = Assert.Throws<BusinessException>(() => ExchangeCapabilityPolicy.EnsureEligible(
                scenario.Predecessor,
                [new ExchangeCouponScope(coupons[0].Id, coupons[0].CurrentOrderServiceId)]));
            var duplicated = Assert.Throws<BusinessException>(() => ExchangeCapabilityPolicy.EnsureEligible(
                scenario.Predecessor,
                [new ExchangeCouponScope(coupons[0].Id, coupons[0].CurrentOrderServiceId), new ExchangeCouponScope(coupons[0].Id, coupons[0].CurrentOrderServiceId)]));

            Assert.Equal(20288, partial.Code);
            Assert.Equal(20288, duplicated.Code);
        }

        // ---------------------------------------------------------------- support

        private sealed record ExchangeCase(
            Order Order,
            ElectronicTicket Predecessor,
            IReadOnlyList<ElectronicTicket> Tickets,
            IReadOnlyList<long> ChangedOrderServiceIds,
            IReadOnlyDictionary<int, long> CouponServiceIds,
            IReadOnlyList<PredecessorPricingEvidence> Evidence,
            AcceptedExchange Accepted,
            IReadOnlyList<ExchangeCouponAllocation> Allocations)
        {
            public AcceptedExchangeArgs Args(AcceptedExchange? accepted = null)
                => new(
                    accepted ?? Accepted,
                    Predecessor.TravelerId,
                    SuccessorTicketId,
                    Allocations,
                    ExchangePricingCorrelation.Map(Predecessor.Id, Predecessor.CarriedPricingLineIds()),
                    OperationId,
                    7,
                    "test");
        }

        private ExchangeCase Scenario(bool roundTrip, int[] changedCouponNumbers)
        {
            var order = roundTrip
                ? MultiPassengerOrderFactory.Create(_ids, _clock)
                : MultiPassengerOrderFactory.CreateOneWay(_ids, _clock);
            var tickets = Ticketed(order);
            var predecessor = tickets.First();
            var coupons = predecessor.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();
            var couponServiceIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.CurrentOrderServiceId);
            var changed = changedCouponNumbers.Select(number => couponServiceIds[number]).Order().ToList();
            var evidence = order.PredecessorPricingEvidence(predecessor.Id, predecessor.DocumentNumber, predecessor.CarriedPricingLinks());

            var request = new ExchangeQuoteRequest(
                order.Id,
                order.CommercialVersion,
                predecessor.Id,
                predecessor.DocumentNumber,
                changed,
                coupons
                    .Select(coupon => new ExchangeScopeCoupon(
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.CurrentOrderServiceId,
                        changed.Contains(coupon.CurrentOrderServiceId),
                        order.SoldSegmentSnapshot(coupon.CurrentOrderServiceId)!,
                        coupon.IssuedSegment.AsTicketedSegment()))
                    .ToList(),
                [],
                evidence,
                order.FareConstructionContexts(),
                order.CurrencyId);

            var accepted = ExchangeSourceFactory.Compose(request, ExchangeSourceFactory.ReplacementsFor(order, changed));

            var allocations = coupons
                .Select((coupon, index) => new ExchangeCouponAllocation(
                    coupon.Id,
                    FirstSuccessorCouponId + index,
                    changed.Contains(coupon.CurrentOrderServiceId) ? FirstReplacementServiceId + index : null,
                    changed.Contains(coupon.CurrentOrderServiceId) ? FirstReplacementSegmentId + index : null))
                .ToList();

            return new ExchangeCase(order, predecessor, tickets, changed, couponServiceIds, evidence, accepted, allocations);
        }

        private static int CouponNumberOf(ElectronicTicket ticket, long couponId)
            => ticket.Coupons.Single(coupon => coupon.Id == couponId).CouponNumber;

        private IReadOnlyList<ElectronicTicket> Ticketed(Order order)
        {
            var serviceIds = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(serviceIds, "PNR-1", null, _ids, _clock);

            var tickets = new List<ElectronicTicket>();
            var documents = new List<IssuedServiceDocument>();

            foreach (var group in order.OrderServices.Where(service => service.IsAirTransport).GroupBy(service => service.SoleBeneficiaryId).OrderBy(group => group.Key))
            {
                var ticketId = _ids.NewId();
                var coupons = group
                    .OrderBy(service => order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value).Sequence)
                    .Select(service =>
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
                    })
                    .ToList();

                var ticket = ElectronicTicket.Issue(ticketId, order.Id, group.Key, 4_000, $"T{ticketId}", 1, null, DocumentAuthority.Local, null, order.CurrencyId, coupons, _ids, _clock);

                tickets.Add(ticket);
                documents.AddRange(ticket.Coupons.Select(coupon => new IssuedServiceDocument(coupon.OrderServiceId, ticket.Id, coupon.Id)));
            }

            order.RecordIssuedDocuments(documents);
            order.CompleteTicketing(_clock);

            return tickets;
        }

        private static SuccessorTicketIssuance Issuance(Order order, ElectronicTicket predecessor, AcceptedExchange accepted, ExchangedOrder exchanged)
            => new(
                SuccessorTicketId,
                predecessor.Id,
                OperationId,
                order.Id,
                predecessor.TravelerId,
                "T999",
                1,
                null,
                DocumentAuthority.Local,
                null,
                order.CurrencyId,
                exchanged.Coupons
                    .Select(binding =>
                    {
                        var coupon = accepted.Coupons.Single(candidate => candidate.PredecessorTicketCouponId == binding.PredecessorTicketCouponId);
                        var snapshot = binding.Disposition == ExchangeCouponDisposition.Replaced
                            ? new IssuedSegmentSnapshot(coupon.Replacement!.Segment.MarketingAirlineId, coupon.Replacement.Segment.FlightNumber, coupon.Replacement.Segment.OriginAirportId, coupon.Replacement.Segment.DestinationAirportId, coupon.Replacement.Segment.DepartureAt, coupon.Replacement.Segment.ArrivalAt, coupon.Replacement.Segment.BookingClass)
                            : Snapshot(order.Segments.Single(segment => segment.Id == binding.OrderSegmentId));

                        return new SuccessorCouponIssuance(
                            binding.SuccessorTicketCouponId,
                            CouponNumberOf(predecessor, binding.PredecessorTicketCouponId),
                            binding.PredecessorTicketCouponId,
                            binding.OrderServiceId,
                            binding.OrderSegmentId,
                            snapshot,
                            coupon.Successor.FareBasis,
                            coupon.Successor.IssuanceValue,
                            coupon.Successor.PriceLinks.Select(link => new TicketCouponPriceLink(exchanged.PricingLineIdsBySourceRef[link.SourceLineRef], null, link.AttributedValue)).ToList());
                    })
                    .ToList());

        private static IssuedSegmentSnapshot Snapshot(Domain.OrderAggregate.Entities.OrderSegment segment)
            => new(segment.MarketingAirlineId, segment.Number, segment.OriginAirportId, segment.DestinationAirportId, segment.DepartureDateTime, segment.ArrivalDateTime, segment.BookingClass);
    }
}
