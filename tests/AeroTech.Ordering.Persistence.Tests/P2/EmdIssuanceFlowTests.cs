using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class EmdIssuanceFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public EmdIssuanceFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task An_already_ticketed_order_issues_only_the_outstanding_document()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            var ticketsBefore = harness.Documents.Requests.Count;

            await AddEmdBaggageAsync(harness, order);

            var issued = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);
            Assert.Equal(ServicingOperationStatus.Completed, issued.OperationStatus);
            Assert.Single(issued.MiscellaneousDocuments);
            Assert.Equal(ticketsBefore, harness.Documents.Requests.Count);
        }

        [Fact]
        public async Task A_pending_ticket_is_issued_before_the_associated_document()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await AddEmdBaggageAsync(harness, await ReloadAsync(order.Id));

            var issued = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);
            Assert.Equal(2, issued.Tickets.Count);
            Assert.Single(issued.MiscellaneousDocuments);

            var document = Assert.Single(await DocumentsAsync(order.Id));
            var coupon = Assert.Single(document.Coupons);

            Assert.Equal(ElectronicMiscDocumentType.Associated, document.Type);
            Assert.NotNull(coupon.AssociatedTicketCouponId);

            var ticketCouponIds = await TicketCouponIdsAsync(order.Id);

            Assert.Contains(coupon.AssociatedTicketCouponId!.Value, ticketCouponIds);
        }

        [Fact]
        public async Task A_standalone_document_issues_without_any_ticket_dependency()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddAsync(harness, order, ProductAdditionFactory.EmdLounge(order));

            var issued = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var document = Assert.Single(await DocumentsAsync(order.Id));

            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, document.Type);
            Assert.Null(Assert.Single(document.Coupons).AssociatedTicketCouponId);
            Assert.Equal(ProductAdditionFactory.LoungeReasonForIssuanceCode, document.ReasonForIssuanceCode);
        }

        [Fact]
        public async Task The_association_resolves_through_the_explicit_air_service_relationship()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var air = ProductAdditionFactory.OutboundAirService(order);

            await AddEmdBaggageAsync(harness, order);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var coupon = Assert.Single(Assert.Single(await DocumentsAsync(order.Id)).Coupons);

            await using var context = _fixture.NewCommandContext();

            var ticketCoupon = await context.ElectronicTickets
                .SelectMany(ticket => ticket.Coupons)
                .SingleAsync(candidate => candidate.Id == coupon.AssociatedTicketCouponId);

            Assert.Equal(air.Id, ticketCoupon.CurrentOrderServiceId);
        }

        [Fact]
        public async Task An_unticketable_association_target_fails_closed()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var reserved = await ReloadAsync(order.Id);
            var air = ProductAdditionFactory.OutboundAirService(reserved);

            await AddEmdBaggageAsync(harness, reserved);
            await WithdrawAirServiceAsync(harness, order.Id, air.Id);

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(20181, exception.Code);
            Assert.Empty(await DocumentsAsync(order.Id));
        }

        [Fact]
        public async Task A_service_without_an_accepted_profile_cannot_be_documented()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddAsync(harness, order, ProductAdditionFactory.EmdBaggage(order, withIssuanceProfile: false));

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(20178, exception.Code);
            Assert.Empty(await DocumentsAsync(order.Id));
        }

        [Fact]
        public async Task Two_reason_for_issuance_codes_cannot_share_one_document()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddAsync(
                harness,
                order,
                ProductAdditionFactory.EmdBaggageBundle(order, secondReasonForIssuanceCode: "I"));

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(20179, exception.Code);
        }

        [Fact]
        public async Task One_grouped_document_preserves_distinct_accepted_sub_codes()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddAsync(harness, order, ProductAdditionFactory.EmdBaggageBundle(order));

            var issued = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);

            var document = Assert.Single(await DocumentsAsync(order.Id));

            Assert.Equal(2, document.Coupons.Count);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceCode, document.ReasonForIssuanceCode);
            Assert.Equal(
                ["0DF", "0DG"],
                document.Coupons.OrderBy(coupon => coupon.CouponNumber).Select(coupon => coupon.ReasonForIssuanceSubCode));
        }

        [Fact]
        public async Task Two_independent_obligations_produce_two_documents()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddAsync(harness, order, ProductAdditionFactory.EmdBaggage(order));

            var reloaded = await ReloadAsync(order.Id);

            await AddAsync(
                harness,
                reloaded,
                ProductAdditionFactory.EmdLounge(reloaded, quotedOfferId: "QOFFER-LNG", productRef: "ADD-LOUNGE"),
                "QOFFER-LNG");

            var issued = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var documents = await DocumentsAsync(order.Id);

            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);
            Assert.Equal(2, documents.Count);
            Assert.Equal(2, documents.Select(document => document.DocumentNumber).Distinct().Count());
            Assert.Equal(2, issued.MiscellaneousDocuments.Count);
        }

        [Fact]
        public async Task Attributed_value_comes_from_accepted_pricing_evidence()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddAsync(harness, order, ProductAdditionFactory.EmdBaggage(order, amount: 400_000m));

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var document = Assert.Single(await DocumentsAsync(order.Id));
            var coupon = Assert.Single(document.Coupons);
            var link = Assert.Single(document.PriceLinks);

            Assert.Equal(400_000m, coupon.IssuanceValue);
            Assert.Equal(400_000m, document.IssuedTotal);
            Assert.Equal(400_000m, link.AttributedValue);
            Assert.Equal(coupon.Id, link.EmdCouponId);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Contains(reloaded.PricingLines, line => line.Id == link.PricingLineId);
        }

        [Fact]
        public async Task An_item_priced_service_with_no_defensible_split_is_not_divided()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            var accepted = ProductAdditionFactory.EmdBaggage(order);

            var itemPriced = accepted with
            {
                PricingLines =
                [
                    ProductAdditionFactory.Line(
                        PricingComponentType.ProductCharge,
                        500_000m,
                        PricingBasisType.OrderItem)
                ],
                Product = accepted.Product with
                {
                    Services =
                    [
                        accepted.Product.Services[0] with { PriceTreatment = ServicePriceTreatment.Included }
                    ]
                }
            };

            await AddAsync(harness, order, itemPriced);

            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(20176, exception.Code);
            Assert.Empty(await DocumentsAsync(order.Id));
        }

        [Fact]
        public async Task Issuance_creates_no_new_commercial_truth()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);

            var before = await ReloadAsync(order.Id);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var after = await ReloadAsync(order.Id);

            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Equal(before.Changes.Count, after.Changes.Count);
            Assert.Equal(before.Items.Count, after.Items.Count);
            Assert.Equal(before.OrderServices.Count, after.OrderServices.Count);
            Assert.Equal(before.FareConstructions.Count, after.FareConstructions.Count);
        }

        [Fact]
        public async Task A_confirmed_document_marks_only_the_service_it_covers()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            var added = await AddEmdBaggageAsync(harness, order);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var reloaded = await ReloadAsync(order.Id);
            var documented = reloaded.OrderServices.Single(service => service.Id == added.ServiceIds.Single());

            Assert.Equal(OrderServiceDocumentStatus.Issued, documented.DocumentStatus);
            Assert.NotNull(documented.ElectronicMiscDocumentId);
            Assert.NotNull(documented.EmdCouponId);
            Assert.Null(documented.ElectronicTicketId);

            Assert.All(reloaded.AirTransportServices, service =>
            {
                Assert.NotNull(service.ElectronicTicketId);
                Assert.Null(service.ElectronicMiscDocumentId);
            });
        }

        [Fact]
        public async Task The_existing_electronic_ticket_is_unchanged_by_document_issuance()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            var before = await TicketSnapshotAsync(order.Id);

            await AddEmdBaggageAsync(harness, order);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(before, await TicketSnapshotAsync(order.Id));
        }

        [Fact]
        public async Task An_issued_document_does_not_ticket_an_unticketed_order()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await AddAsync(harness, order, ProductAdditionFactory.EmdLounge(order));

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var reloaded = await ReloadAsync(order.Id);

            Assert.NotEqual(OrderStatus.Ticketed, reloaded.Status);
            Assert.False(reloaded.IsElectronicTicketingComplete());
        }

        [Fact]
        public async Task An_unknown_provider_outcome_keeps_the_document_recoverable()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);

            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Unknown;

            var key = NewKey();
            var suspended = await harness.Issue.IssueAsync(order.Id, key, null);

            Assert.Equal(ProviderOperationOutcome.Unknown, suspended.Outcome);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, suspended.OperationStatus);
            Assert.Empty(await DocumentsAsync(order.Id));

            var reserved = await ReservedNumbersAsync(OrderSliceHarness.EmdDocumentType, suspended.OperationId);

            Assert.Single(reserved);

            harness.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Issue.IssueAsync(order.Id, key, null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, recovered.Outcome);

            var document = Assert.Single(await DocumentsAsync(order.Id));

            Assert.Equal(reserved[0], document.DocumentNumber);
            Assert.Single(harness.MiscDocuments.Recoveries);
            Assert.Equal(reserved[0], harness.MiscDocuments.Recoveries[0].DocumentNumber);
        }

        [Fact]
        public async Task A_ticket_confirmed_with_an_unknown_document_leaves_the_ticket_intact()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await AddEmdBaggageAsync(harness, await ReloadAsync(order.Id));

            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Unknown;

            var key = NewKey();
            var suspended = await harness.Issue.IssueAsync(order.Id, key, null);

            Assert.Equal(ProviderOperationOutcome.Unknown, suspended.Outcome);
            Assert.Equal(2, suspended.Tickets.Count);

            var reloaded = await ReloadAsync(order.Id);

            Assert.All(reloaded.AirTransportServices, service =>
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus));
            Assert.True(reloaded.IsElectronicTicketingComplete());

            harness.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var ticketRequestsBefore = harness.Documents.Requests.Count;
            var recovered = await harness.Issue.IssueAsync(order.Id, key, null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, recovered.Outcome);
            Assert.Equal(ticketRequestsBefore, harness.Documents.Requests.Count);
            Assert.Single(await DocumentsAsync(order.Id));
        }

        [Fact]
        public async Task The_document_number_is_allocated_before_the_provider_is_called()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var request = Assert.Single(harness.MiscDocuments.Requests);
            var document = Assert.Single(await DocumentsAsync(order.Id));

            Assert.Equal(document.DocumentNumber, request.DocumentNumber);
            Assert.StartsWith("M", document.DocumentNumber, StringComparison.Ordinal);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceCode, request.ReasonForIssuanceCode);
            Assert.Equal(ElectronicMiscDocumentType.Associated, request.EmdType);
        }

        [Fact]
        public async Task A_retry_reuses_the_same_number_and_provider_operation_key()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);

            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Pending;

            var key = NewKey();

            await harness.Issue.IssueAsync(order.Id, key, null);

            var issueRequest = Assert.Single(harness.MiscDocuments.Requests);

            harness.MiscDocuments.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await harness.Issue.IssueAsync(order.Id, key, null);

            var recovery = Assert.Single(harness.MiscDocuments.Recoveries);

            Assert.Equal(issueRequest.DocumentNumber, recovery.DocumentNumber);
            Assert.Equal(issueRequest.OperationKey, recovery.OperationKey);
            Assert.Single(harness.MiscDocuments.Requests);
            Assert.Single(await DocumentsAsync(order.Id));
        }

        [Fact]
        public async Task A_definite_rejection_before_any_irreversible_document_retires_the_number()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);

            harness.MiscDocuments.Outcome = ProviderOperationOutcome.Rejected;

            var rejected = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Rejected, rejected.Outcome);
            Assert.Equal(ServicingOperationStatus.Rejected, rejected.OperationStatus);
            Assert.Empty(await DocumentsAsync(order.Id));
            Assert.Empty(await ReservedNumbersAsync(OrderSliceHarness.EmdDocumentType, rejected.OperationId));
        }

        [Fact]
        public async Task A_replay_after_a_confirmed_document_creates_no_duplicate()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);

            var key = NewKey();

            await harness.Issue.IssueAsync(order.Id, key, null);
            var replay = await harness.Issue.IssueAsync(order.Id, key, null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, replay.Outcome);
            Assert.Single(await DocumentsAsync(order.Id));
            Assert.Single(harness.MiscDocuments.Requests);
        }

        [Fact]
        public async Task Document_issuance_adds_no_order_item_service_or_fare_construction()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            await AddEmdBaggageAsync(harness, order);

            var before = await ReloadAsync(order.Id);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var after = await ReloadAsync(order.Id);

            Assert.Equal(before.Items.Select(item => item.Id).Order(), after.Items.Select(item => item.Id).Order());
            Assert.Equal(before.OrderServices.Select(service => service.Id).Order(), after.OrderServices.Select(service => service.Id).Order());
            Assert.Equal(before.FareConstructions.Count, after.FareConstructions.Count);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            return await ReloadAsync(order.Id);
        }

        private static Task<OrderChangeOutcome> AddEmdBaggageAsync(OrderSliceHarness harness, Order order)
            => AddAsync(harness, order, ProductAdditionFactory.EmdBaggage(order));

        private static async Task<OrderChangeOutcome> AddAsync(
            OrderSliceHarness harness,
            Order order,
            Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedAddServiceChange accepted,
            string quotedOfferId = ProductAdditionFactory.QuotedOfferId)
        {
            harness.Quotes.Quote(accepted);

            return await harness.OrderChange.AddServiceAsync(
                order.Id,
                [new SelectedQuotedOffer(quotedOfferId, [accepted.SelectedOfferItemId])],
                NewKey(),
                order.CommercialVersion);
        }

        private async Task<IReadOnlyList<ElectronicMiscDocument>> DocumentsAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return await new ElectronicMiscDocumentRepository(context).ListByOrderAsync(orderId);
        }

        private async Task<IReadOnlyList<long>> TicketCouponIdsAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return await context.ElectronicTickets
                .Where(ticket => ticket.CurrentServicingOrderId == orderId)
                .SelectMany(ticket => ticket.Coupons)
                .Select(coupon => coupon.Id)
                .ToListAsync();
        }

        private async Task<IReadOnlyList<string>> ReservedNumbersAsync(string documentType, long operationId)
        {
            await using var context = _fixture.NewCommandContext();

            return await context.DocumentStocks
                .Where(stock => stock.DocumentType == documentType)
                .SelectMany(stock => stock.Allocations)
                .Where(allocation => allocation.State == StockNumberState.Reserved
                                     && allocation.OperationId == operationId)
                .Select(allocation => allocation.DocumentNumber)
                .ToListAsync();
        }

        private async Task<IReadOnlyList<(long Id, string Number, decimal Total, int Coupons)>> TicketSnapshotAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var tickets = await context.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .Where(ticket => ticket.CurrentServicingOrderId == orderId)
                .ToListAsync();

            return tickets
                .OrderBy(ticket => ticket.Id)
                .Select(ticket => (ticket.Id, ticket.DocumentNumber, ticket.IssuedTotal, ticket.Coupons.Count))
                .ToList();
        }

        private static async Task WithdrawAirServiceAsync(OrderSliceHarness harness, long orderId, long airServiceId)
        {
            var order = await harness.Orders.GetAsync(orderId);

            order!.WithdrawBeforeTicketing(
                [airServiceId],
                VoidReason.CustomerRequest,
                7,
                harness.Ids,
                new OrderingDatabaseFixture.FixedClock());

            await harness.UnitOfWork.SaveChangesAsync();
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var order = await new OrderRepository(context).GetAsync(orderId);

            Assert.NotNull(order);

            return order!;
        }
    }
}
