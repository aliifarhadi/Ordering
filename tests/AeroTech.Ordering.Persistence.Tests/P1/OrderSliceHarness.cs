using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Creation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Persistence.DocumentStockAggregate;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.FulfillmentReservationAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Providers.Testing;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;
using AeroTech.Ordering.ServiceHost.OperatorContext;
using AeroTech.Ordering.Synchronizer.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Persistence.Tests.P1
{
    public sealed class OrderSliceHarness : IAsyncDisposable
    {
        public const long HomeAirlineId = 7401;
        public const string TicketDocumentType = "ETKT";

        private static int _stockSequence;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly OrderingDbContext _command;
        private readonly OrderQueryDbContext _query;
        private readonly ReferenceDbContext _reference;

        public OrderSliceHarness(OrderingDatabaseFixture fixture, ICallerContext caller)
        {
            _fixture = fixture;
            _command = fixture.NewCommandContext();
            _query = fixture.NewQueryContext();
            _reference = fixture.NewReferenceContext();

            Ids = SequentialIdGenerator.Unique();
            Clock = new TestClock();
            Reservation = new DeterministicReservationAdapter();
            Funding = new DeterministicFundingCoverageAdapter();
            Documents = new DeterministicDocumentIssuanceAdapter();

            var homeOperator = new ReferenceDataHomeOperatorProvider(_reference);
            var frameworkClock = new OrderingDatabaseFixture.FixedClock();
            var unitOfWork = new OrderingUnitOfWork(_command, _query);
            var projector = new OrderProjector(_command, _query, frameworkClock);

            Orders = new OrderRepository(_command);
            var reservations = new FulfillmentReservationRepository(_command);
            var tickets = new ElectronicTicketRepository(_command);
            var stocks = new DocumentStockRepository(_command);

            var options = Options.Create(new OrderOperationOptions
            {
                RecoveryLeaseSeconds = 900,
                TicketDocumentType = TicketDocumentType
            });

            var receipts = new CommandReceiptStore(_command, homeOperator, caller, Ids, frameworkClock);

            var operationStore = new ServicingOperationStore(_command, homeOperator, frameworkClock);

            var coordinator = new OrderOperationCoordinator(
                receipts,
                new OperationClaimStore(_command, Ids, frameworkClock),
                operationStore,
                frameworkClock,
                options);

            Receipts = receipts;
            OperationStore = operationStore;

            UnitOfWork = unitOfWork;
            Projector = projector;
            Tickets = tickets;
            Reservations = reservations;
            Stocks = stocks;

            Reserve = new ReserveOrderService(Orders, reservations, Reservation, coordinator, unitOfWork, Ids, frameworkClock, projector);
            Issue = new IssueOrderService(Orders, reservations, tickets, stocks, Funding, Documents, coordinator, operationStore, receipts, homeOperator, unitOfWork, Ids, frameworkClock, projector, options);
            Create = new CreateOrderService(Orders, receipts, coordinator, unitOfWork, Ids, frameworkClock, projector, homeOperator);
            Withdraw = new WithdrawOrderService(Orders, reservations, tickets, Reservation, Funding, coordinator, new StubIdentity(), unitOfWork, Ids, frameworkClock, projector);
        }

        public SequentialIdGenerator Ids { get; }

        public TestClock Clock { get; }

        public DeterministicReservationAdapter Reservation { get; }

        public DeterministicFundingCoverageAdapter Funding { get; }

        public DeterministicDocumentIssuanceAdapter Documents { get; }

        public OrderRepository Orders { get; }

        public FulfillmentReservationRepository Reservations { get; }

        public ElectronicTicketRepository Tickets { get; }

        public DocumentStockRepository Stocks { get; }

        public OrderingUnitOfWork UnitOfWork { get; }

        public OrderProjector Projector { get; }

        public IReserveOrderService Reserve { get; }

        public IIssueOrderService Issue { get; }

        public IWithdrawOrderService Withdraw { get; }

        public ICreateOrderService Create { get; }

        public CommandReceiptStore Receipts { get; } = default!;

        public ServicingOperationStore OperationStore { get; } = default!;

        public OrderingDbContext Command => _command;

        public OrderQueryDbContext Query => _query;

        public async Task SeedPlatformAsync(long rangeFrom = 1, long rangeTo = 999_999, long? homeAirlineId = null)
        {
            var airlineId = homeAirlineId ?? HomeAirlineId;
            await using var reference = _fixture.NewReferenceContext();

            var operatorSettings = await reference.OperatorSettings
                .SingleOrDefaultAsync(row => row.ScopeKey == OperatorScopeKey.HomeOperator);

            if (operatorSettings is null)
            {
                reference.OperatorSettings.Add(new OperatorSettingsReadModel
                {
                    Id = 1,
                    ScopeKey = OperatorScopeKey.HomeOperator,
                    HomeAirlineId = airlineId,
                    LastUpdateTime = DateTimeOffset.UtcNow
                });
            }
            else
            {
                operatorSettings.HomeAirlineId = airlineId;
            }

            await reference.SaveChangesAsync();

            await using var command = _fixture.NewCommandContext();

            var alreadyStocked = await command.Set<DocumentStock>()
                .AsNoTracking()
                .AnyAsync(stock => stock.OwnerAirlineId == airlineId
                                   && stock.DocumentType == TicketDocumentType
                                   && stock.Status == DocumentStockStatus.Active);

            if (alreadyStocked)
                return;

            var stockId = Ids.NewId();

            command.Set<DocumentStock>().Add(DocumentStock.Define(
                stockId,
                airlineId,
                null,
                TicketDocumentType,
                $"T{Interlocked.Increment(ref _stockSequence):D4}",
                10,
                DocumentStock.NoCheckDigitProfile,
                rangeFrom,
                rangeTo));

            await command.SaveChangesAsync();
        }

        public Task<Order> CreateOrderAsync() => CreateOrderAsync(MultiPassengerOrderFactory.Create(Ids, Clock));

        public async Task<Order> CreateOrderAsync(Order order)
        {

            await Orders.AddAsync(order);
            await UnitOfWork.SaveChangesAsync();

            await Projector.ProjectAsync(order.Id);
            await UnitOfWork.SaveChangesAsync();

            return order;
        }

        public async ValueTask DisposeAsync()
        {
            await _command.DisposeAsync();
            await _query.DisposeAsync();
            await _reference.DisposeAsync();
        }

        private sealed class StubIdentity : IIdentityService
        {
            public long? CurrentUserId => 7;

            public long? CurrentCustomerId => 42;

            public long RequiredCurrentUserId => 7;

            public Guid RequiredDeviceId => Guid.Empty;

            public bool IsAuthenticated => true;

            public List<System.Security.Claims.Claim>? Claims => null;

            public void CheckAccess(string scopeType, object scopeId)
            {
            }
        }
    }
}
