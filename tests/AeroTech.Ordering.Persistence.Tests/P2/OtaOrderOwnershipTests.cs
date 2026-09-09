using System.Reflection;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
using AeroTech.Ordering.Query.OrderAggregate.View;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.RestApi;
using AeroTech.Ordering.RestApi._Shared;
using AeroTech.Ordering.RestApi.V1.OrderAggregate;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contract = AeroTech.Messages.Ordering.IntegrationEvents.V1;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OtaOrderOwnershipTests : IDisposable
    {
        private const long OwnerCustomerId = 42;
        private const long ForeignCustomerId = 8_642;
        private const long UnknownOrderId = 99_999_999_999;

        private static readonly string PricingChangedType = typeof(Contract.OrderPricingChanged).FullName!;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly OrderQueryDbContext _queryContext;
        private readonly ServiceProvider _provider;

        public OtaOrderOwnershipTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _queryContext = fixture.NewQueryContext();

            var services = new ServiceCollection();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetOrderDetailsQuery).Assembly));
            services.AddSingleton(_queryContext);

            _provider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task The_owning_customer_reads_the_typed_order_view()
        {
            await using var harness = NewHarness(OwnerCustomerId);
            var order = await CreateOrderForAsync(harness, OwnerCustomerId);

            var result = Assert.IsType<OkObjectResult>(await NewController(harness).Get(order.Id, default));
            var view = Assert.IsType<OrderView>(result.Value);

            Assert.Equal(order.Id, view.OrderId);
            Assert.Equal(OwnerCustomerId, view.CustomerId);
        }

        [Fact]
        public async Task An_ota_read_touches_no_upstream_provider()
        {
            await using var harness = NewHarness(OwnerCustomerId);
            var order = await CreateOrderForAsync(harness, OwnerCustomerId);

            await NewController(harness).Get(order.Id, default);

            Assert.Equal(0, harness.Quotes.CallCount);
            Assert.Empty(harness.Reservation.ObservedOperationKeys);
            Assert.Empty(harness.Funding.ObservedOperationKeys);
            Assert.Empty(harness.Documents.Requests);
            Assert.Empty(harness.MiscDocuments.Requests);
        }

        [Fact]
        public async Task Reading_another_customers_order_is_not_found()
        {
            var foreign = await CreateForeignOrderAsync();

            await using var harness = NewHarness(OwnerCustomerId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(harness).Get(foreign.OrderId, default));

            Assert.Equal(2500, error.Code);
            Assert.Equal(404, error.HttpStatus);
        }

        [Fact]
        public async Task A_cross_customer_read_error_discloses_nothing_about_the_other_order()
        {
            var foreign = await CreateForeignOrderAsync();

            await using var harness = NewHarness(OwnerCustomerId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(harness).Get(foreign.OrderId, default));

            Assert.DoesNotContain(ForeignCustomerId.ToString(), error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("FARHADI", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreign.CustomerTotal.ToString("0"), error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(MultiPassengerOrderFactory.SourceOfferId, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_unknown_order_and_another_customers_order_are_indistinguishable()
        {
            var foreign = await CreateForeignOrderAsync();

            await using var harness = NewHarness(OwnerCustomerId);
            var controller = NewController(harness);

            var crossCustomer = await Assert.ThrowsAsync<BusinessException>(() => controller.Get(foreign.OrderId, default));
            var unknown = await Assert.ThrowsAsync<BusinessException>(() => controller.Get(UnknownOrderId, default));

            Assert.Equal(unknown.Code, crossCustomer.Code);
            Assert.Equal(unknown.HttpStatus, crossCustomer.HttpStatus);
            Assert.Equal(
                unknown.Message.Replace(UnknownOrderId.ToString(), "{id}", StringComparison.Ordinal),
                crossCustomer.Message.Replace(foreign.OrderId.ToString(), "{id}", StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_read_without_customer_identity_fails_closed()
        {
            await using var owner = NewHarness(OwnerCustomerId);
            var order = await CreateOrderForAsync(owner, OwnerCustomerId);

            await using var harness = NewHarness(customerId: null);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(harness).Get(order.Id, default));

            Assert.Equal(2890, error.Code);
            Assert.Equal(403, error.HttpStatus);
        }

        [Fact]
        public async Task An_unauthenticated_caller_fails_closed_even_with_a_customer_value()
        {
            await using var owner = NewHarness(OwnerCustomerId);
            var order = await CreateOrderForAsync(owner, OwnerCustomerId);

            var caller = Caller(OwnerCustomerId);
            caller.IsAuthenticated = false;

            await using var harness = new OrderSliceHarness(_fixture, caller);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(harness).Get(order.Id, default));

            Assert.Equal(2890, error.Code);
        }

        [Fact]
        public async Task A_cross_customer_change_never_enters_the_commercial_workflow()
        {
            var foreign = await CreateForeignOrderAsync();

            await using var harness = NewHarness(OwnerCustomerId);
            harness.Quotes.Quote(ProductAdditionFactory.Seat(foreign.Order));

            var before = await SnapshotAsync(foreign.OrderId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(harness, NewKey()).Change(foreign.OrderId, ChangeRequest(foreign.Order), default));

            Assert.Equal(2500, error.Code);
            Assert.Equal(404, error.HttpStatus);

            Assert.Equal(0, harness.Quotes.CallCount);
            Assert.Empty(harness.Events.Dispatched);

            var after = await SnapshotAsync(foreign.OrderId);

            Assert.Equal(before, after);
        }

        [Fact]
        public async Task A_cross_customer_scope_cancellation_never_reaches_the_pricing_authority()
        {
            var foreign = await CreateForeignOrderAsync();

            await using var harness = NewHarness(OwnerCustomerId);

            var before = await SnapshotAsync(foreign.OrderId);

            var request = new OrderChangeRequest(
                1,
                CancelOrderItem: new CancelOrderItem(foreign.Order.Items.First().Id, "QCXL-1"));

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(harness, NewKey()).Change(foreign.OrderId, request, default));

            Assert.Equal(2500, error.Code);
            Assert.Equal(404, error.HttpStatus);
            Assert.Equal(0, harness.CancellationQuotes.CallCount);
            Assert.Empty(harness.Reservation.ObservedOperationKeys);
            Assert.Empty(harness.Events.Dispatched);
            Assert.Equal(before, await SnapshotAsync(foreign.OrderId));
        }

        [Fact]
        public async Task The_owning_customer_can_still_change_the_order()
        {
            await using var harness = NewHarness(OwnerCustomerId);
            var order = await CreateOrderForAsync(harness, OwnerCustomerId);

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var result = Assert.IsType<OkObjectResult>(
                await NewController(harness, NewKey()).Change(order.Id, ChangeRequest(order), default));

            var response = Assert.IsType<OrderChangeResponse>(result.Value);

            Assert.Equal(2, response.CommercialVersion);
            Assert.NotNull(response.Order);
            Assert.Equal(order.Id, response.Order!.OrderId);
            Assert.Equal(1, harness.Quotes.CallCount);
        }

        [Fact]
        public async Task A_non_owner_cannot_replay_the_owners_idempotency_key()
        {
            await using var owner = NewHarness(ForeignCustomerId);
            var order = await CreateOrderForAsync(owner, ForeignCustomerId);
            var accepted = ProductAdditionFactory.Seat(order);
            var key = NewKey();

            owner.Quotes.Quote(accepted);

            await NewController(owner, key).Change(order.Id, ChangeRequest(order), default);

            var before = await SnapshotAsync(order.Id);

            await using var intruder = NewHarness(OwnerCustomerId);
            intruder.Quotes.Quote(accepted);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewController(intruder, key).Change(order.Id, ChangeRequest(order), default));

            Assert.Equal(2500, error.Code);
            Assert.Equal(404, error.HttpStatus);
            Assert.Equal(0, intruder.Quotes.CallCount);
            Assert.DoesNotContain(ForeignCustomerId.ToString(), error.Message, StringComparison.Ordinal);

            Assert.Equal(before, await SnapshotAsync(order.Id));
        }

        [Fact]
        public async Task A_non_owner_cannot_read_the_order_it_tried_to_change()
        {
            await using var owner = NewHarness(ForeignCustomerId);
            var order = await CreateOrderForAsync(owner, ForeignCustomerId);

            await using var intruder = NewHarness(OwnerCustomerId);
            var controller = NewController(intruder, NewKey());

            await Assert.ThrowsAsync<BusinessException>(
                () => controller.Change(order.Id, ChangeRequest(order), default));

            await Assert.ThrowsAsync<BusinessException>(() => controller.Get(order.Id, default));
        }

        [Fact]
        public async Task The_backoffice_change_is_not_customer_restricted()
        {
            var caller = TestCallerContexts.AirlineUser(11, $"subject-{Guid.NewGuid():N}");

            Assert.Null(caller.CustomerId);

            await using var harness = new OrderSliceHarness(_fixture, caller);
            var order = await CreateOrderForAsync(harness, ForeignCustomerId);

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(
                order.Id,
                OrderChangeRequestMapper.ToSelections(ChangeRequest(order)),
                NewKey(),
                order.CommercialVersion);

            Assert.Equal(2, outcome.CommercialVersion);
            Assert.False(outcome.IsReplay);
        }

        [Fact]
        public void Every_customer_facing_controller_takes_the_access_guard_and_backoffice_does_not()
        {
            var customerFacing = new[] { typeof(OtaController), typeof(OtaPanelController) };

            Assert.All(customerFacing, controller => Assert.Contains(
                controller.GetConstructors().Single().GetParameters(),
                parameter => parameter.ParameterType == typeof(IOrderCustomerAccessGuard)));

            var backoffice = typeof(RestApiAssembly).Assembly.GetTypes()
                .Where(type => type.Name.StartsWith("Backoffice", StringComparison.Ordinal))
                .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(backoffice);
            Assert.All(backoffice, controller => Assert.DoesNotContain(
                controller.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
                parameter => parameter.ParameterType == typeof(IOrderCustomerAccessGuard)));
        }

        [Fact]
        public void No_customer_facing_controller_reads_a_defaulted_customer_identity()
        {
            var identityUses = new[] { typeof(OtaController), typeof(OtaPanelController) }
                .SelectMany(controller => controller.GetConstructors().Single().GetParameters())
                .Count(parameter => parameter.ParameterType == typeof(IOrderCustomerAccessGuard));

            Assert.Equal(2, identityUses);

            Assert.Equal(
                typeof(long),
                typeof(IOrderCustomerAccessGuard).GetMethod(nameof(IOrderCustomerAccessGuard.RequireCustomerId))!.ReturnType);
        }

        [Fact]
        public void The_public_ota_contract_carries_no_caller_supplied_customer_identity()
        {
            var names = new[]
                {
                    typeof(OrderChangeRequest),
                    typeof(AcceptSelectedQuotedOffer),
                    typeof(OtaCreateOrderFromOfferRequest)
                }
                .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain(names, name => name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("Owner", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void The_guard_resolves_ownership_from_ordering_local_truth()
        {
            var dependencies = typeof(OrderCustomerAccessGuard).GetConstructors().Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType.Name)
                .ToList();

            Assert.Equal(new[] { "IOrderRepository", "ICallerContext" }, dependencies);

            var guardMethod = typeof(IOrderCustomerAccessGuard).GetMethod(nameof(IOrderCustomerAccessGuard.EnsureOwnedAsync))!;

            Assert.DoesNotContain(
                guardMethod.GetParameters(),
                parameter => parameter.Name!.Contains("customer", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void The_ota_read_contract_is_still_the_typed_order_view()
            => Assert.Equal(
                typeof(OrderView),
                typeof(GetOrderDetailsQuery).GetInterfaces()
                    .Single(contract => contract.IsGenericType && contract.Name.StartsWith("IRequest", StringComparison.Ordinal))
                    .GetGenericArguments()[0]);

        public void Dispose()
        {
            _provider.Dispose();
            _queryContext.Dispose();
        }

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static TestCallerContexts Caller(long? customerId)
        {
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            caller.CustomerId = customerId;
            return caller;
        }

        private OrderSliceHarness NewHarness(long? customerId) => new(_fixture, Caller(customerId));

        private OtaController NewController(OrderSliceHarness harness, string? idempotencyKey = null)
        {
            var httpContext = new DefaultHttpContext();

            if (idempotencyKey is not null)
                httpContext.Request.Headers[IdempotencyKey.HeaderName] = idempotencyKey;

            return new OtaController(
                _provider.GetRequiredService<IMediator>(),
                new OrderingDatabaseFixture.NullIdentityService(),
                harness.OrderChange,
                harness.ScopeCancel,
                harness.AccessGuard)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        private static Task<Order> CreateOrderForAsync(OrderSliceHarness harness, long customerId)
            => harness.CreateOrderAsync(MultiPassengerOrderFactory.CreateForCustomer(customerId, harness.Ids, harness.Clock));

        private static OrderChangeRequest ChangeRequest(Order order)
            => new(
                order.CommercialVersion,
                [new AcceptSelectedQuotedOffer(ProductAdditionFactory.QuotedOfferId, [ProductAdditionFactory.SelectedOfferItemId])]);

        private async Task<ForeignOrder> CreateForeignOrderAsync()
        {
            await using var harness = NewHarness(ForeignCustomerId);
            var order = await CreateOrderForAsync(harness, ForeignCustomerId);

            return new ForeignOrder(order, order.Id, order.CustomerTotal);
        }

        private async Task<OrderAccessSnapshot> SnapshotAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();
            await using var query = _fixture.NewQueryContext();

            var order = (await new OrderRepository(command).GetAsync(orderId))!;
            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == orderId);

            return new OrderAccessSnapshot(
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                await command.CommandReceipts.CountAsync(row => row.OrderId == orderId),
                await command.ServicingOperations.CountAsync(row => row.OrderId == orderId),
                await command.OperationOrderClaims.CountAsync(row => row.OrderId == orderId),
                order.Changes.Count,
                order.Items.Count,
                order.OrderServices.Count,
                order.PriceChangeSets.Count,
                order.PricingLines.Count,
                await command.OutboxMessages.CountAsync(row => row.MessageType.StartsWith(PricingChangedType)),
                details.ProjectionRevision,
                details.SnapshotJson);
        }

        private sealed record ForeignOrder(Order Order, long OrderId, decimal CustomerTotal);

        private sealed record OrderAccessSnapshot(
            int CommercialVersion,
            long FinancialSequence,
            long ObligationVersion,
            decimal CustomerTotal,
            int CommandReceipts,
            int ServicingOperations,
            int OperationClaims,
            int Changes,
            int Items,
            int Services,
            int PriceChangeSets,
            int PricingLines,
            int PricingOutboxMessages,
            long ProjectionRevision,
            string SnapshotJson);
    }
}
