using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingFeeDocumentFreezeGuardTests
    {
        public enum TaxPerturbation
        {
            MissingLink = 1,
            WrongPricingLine = 2,
            WrongAttributedValue = 3,
            ExtraLink = 4
        }

        private const string PenaltyRef = "EXC:PENALTY";
        private const string TaxRef = "EXC:PENALTY-TAX";
        private const string DocumentRef = "FEE-DOC-1";
        private const string SourceRef = "FEE-SOURCE-1";
        private const string ReasonForIssuanceCode = "C";
        private const string ReasonForIssuanceSubCode = "98A";
        private const string ProviderReference = "EMD-CONFIRMED-BEFORE-CRASH";
        private const decimal PenaltyAmount = ExchangeSourceFactory.AddCollectAmount;
        private const decimal TaxAmount = 45_000m;

        private readonly OrderingDatabaseFixture _fixture;

        public ServicingFeeDocumentFreezeGuardTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- FG1. durable Confirmed is never re-asked of the provider

        [Fact]
        public async Task G6FG1_a_confirmed_checkpoint_surviving_a_crash_never_calls_the_provider_again()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var held = await SuspendedFeeDocumentAsync(setup, harness);
            var allocated = await AllocatedNumberAsync(harness, held.OperationId);

            await ConfirmFeeDocumentIssuanceAsync(_fixture, held.OperationId, DocumentRef, ProviderReference);

            await using var resumed = new OrderSliceHarness(_fixture, caller);
            Register(resumed, held.Scenario);

            // both provider answers are Rejected; durable Confirmed must make either call unreachable
            resumed.MiscDocuments.Outcome = ProviderOperationOutcome.Rejected;
            resumed.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await resumed.Exchange.ExchangeAsync(held.Execution);

            var accepted = (await resumed.ExchangePlans.FindAsync(held.OperationId))!.FeeDocuments.Single();
            var document = Assert.Single(await AncillariesAsync(_fixture, held.Scenario.OrderId));

            Assert.Empty(resumed.MiscDocuments.Requests);
            Assert.Empty(resumed.MiscDocuments.Recoveries);

            Assert.Equal(ProviderOperationOutcome.Confirmed, accepted.IssuanceOutcome);
            Assert.Equal(ProviderReference, accepted.IssuanceProviderReference);
            Assert.Equal(allocated, accepted.AllocatedDocumentNumber);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(allocated, document.DocumentNumber);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, document.Type);
            Assert.Equal(2, document.PriceLinks.Count);
            Assert.Equal(document.Id, accepted.ElectronicMiscDocumentId);
            Assert.NotNull(accepted.SettledAt);
        }

        // ---------------------------------------------- FG2. an exactly matching document is adopted once

        [Fact]
        public async Task G6FG2_an_exactly_matching_existing_document_is_adopted_without_a_second_issue()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var held = await SuspendedFeeDocumentAsync(setup, harness);
            var allocated = await AllocatedNumberAsync(harness, held.OperationId);
            var byHand = await IssueByHandAsync(harness, held, allocated, null);

            await ConfirmFeeDocumentIssuanceAsync(_fixture, held.OperationId, DocumentRef, ProviderReference);

            await using var resumed = new OrderSliceHarness(_fixture, caller);
            Register(resumed, held.Scenario);
            resumed.MiscDocuments.Outcome = ProviderOperationOutcome.Rejected;
            resumed.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await resumed.Exchange.ExchangeAsync(held.Execution);

            var accepted = (await resumed.ExchangePlans.FindAsync(held.OperationId))!.FeeDocuments.Single();
            var document = Assert.Single(await AncillariesAsync(_fixture, held.Scenario.OrderId));

            Assert.Empty(resumed.MiscDocuments.Requests);
            Assert.Empty(resumed.MiscDocuments.Recoveries);

            Assert.Equal(byHand.Id, document.Id);
            Assert.Equal(byHand.DocumentVersion, document.DocumentVersion);
            Assert.Equal(byHand.Id, accepted.ElectronicMiscDocumentId);
            Assert.NotNull(accepted.SettledAt);
            Assert.Equal(ProviderOperationOutcome.Confirmed, accepted.IssuanceOutcome);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
        }

        // ---------------------------------------------- FG3. a price-link contradiction reconciles

        [Theory]
        [InlineData(TaxPerturbation.MissingLink)]
        [InlineData(TaxPerturbation.WrongPricingLine)]
        [InlineData(TaxPerturbation.WrongAttributedValue)]
        [InlineData(TaxPerturbation.ExtraLink)]
        public async Task G6FG3_a_contradicted_price_link_set_reconciles_and_keeps_confirmed_truth(
            TaxPerturbation perturbation)
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var held = await SuspendedFeeDocumentAsync(setup, harness);
            var allocated = await AllocatedNumberAsync(harness, held.OperationId);
            var byHand = await IssueByHandAsync(harness, held, allocated, perturbation);

            await ConfirmFeeDocumentIssuanceAsync(_fixture, held.OperationId, DocumentRef, ProviderReference);

            await using var resumed = new OrderSliceHarness(_fixture, caller);
            Register(resumed, held.Scenario);
            resumed.MiscDocuments.Outcome = ProviderOperationOutcome.Confirmed;
            resumed.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var outcome = await resumed.Exchange.ExchangeAsync(held.Execution);

            var accepted = (await resumed.ExchangePlans.FindAsync(held.OperationId))!.FeeDocuments.Single();
            var document = Assert.Single(await AncillariesAsync(_fixture, held.Scenario.OrderId));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            Assert.Equal(ProviderOperationOutcome.Confirmed, accepted.IssuanceOutcome);
            Assert.Equal(ProviderReference, accepted.IssuanceProviderReference);
            Assert.Null(accepted.SettledAt);
            Assert.Null(accepted.ElectronicMiscDocumentId);

            Assert.Empty(resumed.MiscDocuments.Requests);
            Assert.Empty(resumed.MiscDocuments.Recoveries);

            // the existing document is neither overwritten nor replaced
            Assert.Equal(byHand.Id, document.Id);
            Assert.Equal(byHand.DocumentVersion, document.DocumentVersion);
            Assert.Equal(byHand.PriceLinks.Count, document.PriceLinks.Count);

            await AssertTicketTruthHeldAsync(held.Scenario, outcome);
        }

        // ---------------------------------------------- FG4. an unresolvable secondary line blocks dispatch

        [Fact]
        public async Task G6FG4_a_missing_committed_secondary_line_blocks_any_provider_issuance()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [1], shapeAccepted: WithPenaltyAndTaxDocument);
            var key = NewKey();

            // hold the operation after ticket truth is durable but before the G6 stage is reached
            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Pending;

            var held = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Empty(harness.MiscDocuments.Requests);

            var committed = await ReloadAsync(_fixture, scenario.OrderId);

            await DetachPricingLineSourceRefAsync(
                _fixture, CommittedLine(committed, held.OperationId, TaxRef).Id);

            await using var resumed = new OrderSliceHarness(_fixture, caller);
            Register(resumed, scenario);
            resumed.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var accepted = (await resumed.ExchangePlans.FindAsync(outcome.OperationId))!.FeeDocuments.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            Assert.Empty(resumed.MiscDocuments.Requests);
            Assert.Empty(resumed.MiscDocuments.Recoveries);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            // the accepted instruction survives untouched for an operator to resolve
            Assert.Null(accepted.IssuanceOutcome);
            Assert.Null(accepted.AllocatedDocumentNumber);
            Assert.Null(accepted.SettledAt);
            Assert.Equal(PenaltyAmount + TaxAmount, accepted.TotalAmount);
            Assert.Equal(2, accepted.Coupons.Single().Attributions.Count);

            await AssertTicketTruthHeldAsync(scenario, outcome);
        }

        // ---------------------------------------------- FG5. a supplied traveller must belong to this order

        [Fact]
        public async Task G6FG5_a_foreign_traveller_refuses_before_the_ticket_exchange()
        {
            await using var foreign = NewHarness();
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var other = await IssuedAsync(_fixture, foreign);
            var foreignTraveller = (await ReloadAsync(_fixture, other.OrderId)).Travellers.First().Id;

            var scenario = await AddCollectAsync(
                _fixture,
                setup,
                harness,
                [1],
                shapeAccepted: accepted => WithTraveller(accepted, foreignTraveller));

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey())));

            Assert.Equal(20327, refusal.Code);
            Assert.Equal(409, refusal.HttpStatus);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        [Fact]
        public async Task G6FG5b_a_traveller_of_this_order_is_accepted_and_round_trips()
        {
            var traveller = 0L;

            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture,
                setup,
                harness,
                [1],
                shapeAccepted: accepted => WithTraveller(accepted, traveller),
                createOrder: async candidate =>
                {
                    var created = await candidate.CreateOrderAsync();

                    traveller = created.Travellers.First().Id;

                    return created;
                });

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var document = Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));

            Assert.NotEqual(0L, traveller);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(traveller, document.TravelerId);
        }

        [Fact]
        public async Task G6FG5c_a_null_traveller_stays_null_and_is_accepted()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [1], shapeAccepted: WithPenaltyDocument);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Null(Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId)).TravelerId);
        }

        // ---------------------------------------------- shared arrangement

        private async Task<HeldFeeDocument> SuspendedFeeDocumentAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness)
        {
            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [1], shapeAccepted: WithPenaltyAndTaxDocument);
            var key = NewKey();

            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Pending;

            var held = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Single(harness.MiscDocuments.Requests);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));

            return new HeldFeeDocument(scenario, key, held.OperationId);
        }

        private static async Task<string> AllocatedNumberAsync(OrderSliceHarness harness, long operationId)
        {
            var plan = await harness.ExchangePlans.FindAsync(operationId);
            var allocated = plan!.FeeDocuments.Single().AllocatedDocumentNumber;

            Assert.NotNull(allocated);

            return allocated!;
        }

        private async Task<ElectronicMiscDocument> IssueByHandAsync(
            OrderSliceHarness harness,
            HeldFeeDocument held,
            string documentNumber,
            TaxPerturbation? perturbation)
        {
            var order = await ReloadAsync(_fixture, held.Scenario.OrderId);
            var penalty = CommittedLine(order, held.OperationId, PenaltyRef);
            var tax = CommittedLine(order, held.OperationId, TaxRef);
            var unrelated = AnotherCommittedLine(order, held.OperationId, penalty.Id, tax.Id);

            var links = new List<EmdCouponPriceLink> { new(penalty.Id, null, PenaltyAmount) };

            switch (perturbation)
            {
                case null:
                    links.Add(new EmdCouponPriceLink(tax.Id, null, TaxAmount));
                    break;

                case TaxPerturbation.MissingLink:
                    break;

                case TaxPerturbation.WrongPricingLine:
                    links.Add(new EmdCouponPriceLink(unrelated.Id, null, TaxAmount));
                    break;

                case TaxPerturbation.WrongAttributedValue:
                    links.Add(new EmdCouponPriceLink(tax.Id, null, TaxAmount - 1m));
                    break;

                case TaxPerturbation.ExtraLink:
                    links.Add(new EmdCouponPriceLink(tax.Id, null, TaxAmount));
                    links.Add(new EmdCouponPriceLink(unrelated.Id, null, 1m));
                    break;
            }

            var document = ElectronicMiscDocument.Issue(
                harness.Ids.NewId(),
                held.Scenario.OrderId,
                null,
                held.OperationId,
                documentNumber,
                ElectronicMiscDocumentType.Standalone,
                ReasonForIssuanceCode,
                OrderSliceHarness.HomeAirlineId,
                order.AirlineOfficeId,
                DocumentAuthority.Local,
                order.CurrencyId,
                [
                    new EmdCouponIssuance(
                        EmdCouponPurpose.Fee,
                        ReasonForIssuanceSubCode,
                        PenaltyAmount + TaxAmount,
                        links,
                        PricingLineId: penalty.Id)
                ],
                harness.Ids,
                harness.Clock);

            document.RecordProviderConfirmation(ProviderReference);

            await harness.MiscDocumentRepository.AddAsync(document);
            await harness.UnitOfWork.SaveChangesAsync();

            return document;
        }

        private async Task AssertTicketTruthHeldAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
        {
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        private async Task AssertNoIrreversibleWorkAsync(OrderSliceHarness harness, ExchangeScenario scenario)
        {
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.MiscDocuments.Requests);
            Assert.Empty(harness.MiscDocuments.Recoveries);
            Assert.Empty(predecessor.Exchanges);
            Assert.NotEqual(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Empty(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Empty(
                (await TicketsAsync(_fixture, scenario.OrderId))
                    .Where(candidate => candidate.PredecessorElectronicTicketId is not null));
        }

        // ---------------------------------------------- source shaping

        private static AcceptedExchange WithPenaltyDocument(AcceptedExchange accepted)
            => accepted with
            {
                FeeDocuments =
                [
                    Document(
                        accepted.SaleCurrencyId,
                        new AcceptedServicingFeeDocumentCoupon(
                            ReasonForIssuanceSubCode,
                            PenaltyRef,
                            PenaltyAmount,
                            [new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount)]))
                ]
            };

        private static AcceptedExchange WithPenaltyAndTaxDocument(AcceptedExchange accepted)
        {
            var taxed = accepted with
            {
                AddCollect = new AcceptedAddCollect(
                    accepted.AddCollect!.Amount + TaxAmount, accepted.SaleCurrencyId),
                PricingLines =
                [
                    .. accepted.PricingLines,
                    new AcceptedExchangePricingLine(
                        PricingComponentType.Tax,
                        PricingEffect.CustomerBalance,
                        OrderPricingLineDirection.Debit,
                        PricingLineRole.Original,
                        TaxAmount,
                        accepted.SaleCurrencyId,
                        TaxAmount,
                        accepted.SaleCurrencyId,
                        PricingBasisType.Order,
                        RefundabilityRule.NonRefundable,
                        TaxRef,
                        Code: "YQ")
                ]
            };

            return taxed with
            {
                FeeDocuments =
                [
                    Document(
                        taxed.SaleCurrencyId,
                        new AcceptedServicingFeeDocumentCoupon(
                            ReasonForIssuanceSubCode,
                            PenaltyRef,
                            PenaltyAmount + TaxAmount,
                            [
                                new AcceptedServicingFeeAttribution(PenaltyRef, PenaltyAmount),
                                new AcceptedServicingFeeAttribution(TaxRef, TaxAmount)
                            ]))
                ]
            };
        }

        private static AcceptedServicingFeeDocument Document(
            int currencyId,
            AcceptedServicingFeeDocumentCoupon coupon)
            => new(
                DocumentRef,
                SourceRef,
                OrderSliceHarness.HomeAirlineId,
                null,
                ReasonForIssuanceCode,
                currencyId,
                [coupon]);

        private static AcceptedExchange WithTraveller(AcceptedExchange accepted, long travellerId)
        {
            var documented = WithPenaltyDocument(accepted);

            return documented with
            {
                FeeDocuments = documented.FeeDocuments!
                    .Select(document => document with { TravelerId = travellerId })
                    .ToList()
            };
        }

        private static OrderPricingLine CommittedLine(Order order, long operationId, string sourceLineRef)
            => CommittedLines(order, operationId)
                .Single(line => line.SourceLineRef == sourceLineRef);

        private static OrderPricingLine AnotherCommittedLine(
            Order order,
            long operationId,
            params long[] excluded)
            => CommittedLines(order, operationId)
                .First(line => !excluded.Contains(line.Id));

        private static IReadOnlyList<OrderPricingLine> CommittedLines(Order order, long operationId)
        {
            var change = order.Changes.Single(candidate => candidate.OperationId == operationId);
            var changeSet = order.PriceConsequencesOf(change.Id).Single();

            return order.PricingLines
                .Where(line => line.PriceChangeSetId == changeSet.Id)
                .OrderBy(line => line.Id)
                .ToList();
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7440, $"feeguard-{Guid.NewGuid():N}");

        private sealed record HeldFeeDocument(ExchangeScenario Scenario, string Key, long OperationId)
        {
            public ExchangeExecution Execution => Scenario.FundedExecution(Key);
        }
    }
}
