using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.RestApi.V1.OrderAggregate;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OtaCreationFailClosedTests : IDisposable
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly RecordingCreateOrderHandler _handler = new();
        private readonly ServiceProvider _provider;

        public OtaCreationFailClosedTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;

            var services = new ServiceCollection();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<OtaCreationFailClosedTests>());
            services.AddSingleton<IRequestHandler<OtaCreateOrderFromOfferCommand, CreateOrderFromOfferResult>>(_handler);

            _provider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task The_ota_api_creation_fails_closed_without_customer_context()
        {
            await using var harness = NewHarness(customerId: null);

            var before = await OrderCountAsync();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewOtaController(harness).CreateFromOffer(Request(), default));

            Assert.Equal(2890, error.Code);
            Assert.Equal(403, error.HttpStatus);

            Assert.Equal(0, _handler.Invocations);
            Assert.Equal(before, await OrderCountAsync());
            AssertNoProviderWork(harness);
        }

        [Fact]
        public async Task The_ota_panel_creation_fails_closed_without_customer_context()
        {
            await using var harness = NewHarness(customerId: null);

            var before = await OrderCountAsync();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewOtaPanelController(harness).CreateFromOffer(Request(), default));

            Assert.Equal(2890, error.Code);
            Assert.Equal(403, error.HttpStatus);

            Assert.Equal(0, _handler.Invocations);
            Assert.Equal(before, await OrderCountAsync());
            AssertNoProviderWork(harness);
        }

        [Fact]
        public async Task An_unauthenticated_caller_cannot_create_an_ota_order()
        {
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            caller.CustomerId = 42;
            caller.IsAuthenticated = false;

            await using var harness = new OrderSliceHarness(_fixture, caller);

            var before = await OrderCountAsync();

            var api = await Assert.ThrowsAsync<BusinessException>(
                () => NewOtaController(harness).CreateFromOffer(Request(), default));

            var panel = await Assert.ThrowsAsync<BusinessException>(
                () => NewOtaPanelController(harness).CreateFromOffer(Request(), default));

            Assert.Equal(2890, api.Code);
            Assert.Equal(2890, panel.Code);
            Assert.Equal(0, _handler.Invocations);
            Assert.Equal(before, await OrderCountAsync());
        }

        [Fact]
        public async Task An_authenticated_customer_reaches_the_creation_workflow()
        {
            await using var harness = NewHarness(customerId: 42);

            await NewOtaController(harness).CreateFromOffer(Request(), default);
            await NewOtaPanelController(harness).CreateFromOffer(Request(), default);

            Assert.Equal(2, _handler.Invocations);
            Assert.All(_handler.Commands, command => Assert.Equal(42, command.CustomerId));
        }

        public void Dispose() => _provider.Dispose();

        private static void AssertNoProviderWork(OrderSliceHarness harness)
        {
            Assert.Equal(0, harness.Quotes.CallCount);
            Assert.Empty(harness.Reservation.ObservedOperationKeys);
            Assert.Empty(harness.Funding.ObservedOperationKeys);
            Assert.Empty(harness.Documents.Requests);
            Assert.Empty(harness.MiscDocuments.Requests);
            Assert.Empty(harness.Events.Dispatched);
        }

        private OrderSliceHarness NewHarness(long? customerId)
        {
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            caller.CustomerId = customerId;

            return new OrderSliceHarness(_fixture, caller);
        }

        private OtaController NewOtaController(OrderSliceHarness harness)
            => new(
                _provider.GetRequiredService<IMediator>(),
                new OrderingDatabaseFixture.NullIdentityService(),
                harness.OrderChange,
                harness.AccessGuard)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

        private OtaPanelController NewOtaPanelController(OrderSliceHarness harness)
            => new(
                _provider.GetRequiredService<IMediator>(),
                new OrderingDatabaseFixture.NullIdentityService(),
                harness.AccessGuard)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

        private async Task<int> OrderCountAsync()
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Orders.CountAsync();
        }

        private static OtaCreateOrderFromOfferRequest Request()
            => new(
                "OFFER-P2H-1",
                new OtaOrderContact(["a@b.c"], []),
                [
                    new OtaOrderTraveller(
                        1,
                        null,
                        new OtaTravellerName("ALI", "FARHADI", false),
                        new DateOnly(1990, 1, 1),
                        Gender.Male,
                        PassengerTypeCode.ADT,
                        1,
                        1,
                        [])
                ]);

        private sealed class RecordingCreateOrderHandler
            : IRequestHandler<OtaCreateOrderFromOfferCommand, CreateOrderFromOfferResult>
        {
            public List<OtaCreateOrderFromOfferCommand> Commands { get; } = new();

            public int Invocations => Commands.Count;

            public Task<CreateOrderFromOfferResult> Handle(
                OtaCreateOrderFromOfferCommand request,
                CancellationToken cancellationToken)
            {
                Commands.Add(request);

                return Task.FromResult(new CreateOrderFromOfferResult(0, OrderStatus.Created, null, null, null));
            }
        }
    }
}
