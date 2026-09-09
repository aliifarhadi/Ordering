using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund;
using AeroTech.Ordering.Application.OrderAggregate.Services.Creation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Persistence.DocumentStockAggregate;
using AeroTech.Ordering.Persistence.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.FulfillmentReservationAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Outbox;
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
        public const string EmdDocumentType = "EMD";

        private static int _stockSequence;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly OrderingDbContext _command;
        private readonly OrderQueryDbContext _query;
        private readonly ReferenceDbContext _reference;

        public OrderSliceHarness(OrderingDatabaseFixture fixture, ICallerContext caller)
        {
            _fixture = fixture;

            Events = new OutboxDomainEventDispatcher(() => new OutboxWriter(
                _command!,
                new OrderingDatabaseFixture.FixedClock(),
                new OrderingDatabaseFixture.NullIdentityService(),
                Options.Create(new IntegrationEventOptions { TenantId = 1, SourceSystem = "Ordering" })));

            _command = fixture.NewCommandContext(Events);
            _query = fixture.NewQueryContext();
            _reference = fixture.NewReferenceContext();

            Ids = SequentialIdGenerator.Unique();
            Clock = new TestClock();
            Reservation = new DeterministicReservationAdapter();
            Funding = new DeterministicFundingCoverageAdapter();
            Documents = new DeterministicDocumentIssuanceAdapter();
            MiscDocuments = new DeterministicEmdIssuanceAdapter();
            Quotes = new DeterministicOrderChangeQuoteAdapter();

            var homeOperator = new ReferenceDataHomeOperatorProvider(_reference);
            var frameworkClock = new OrderingDatabaseFixture.FixedClock();
            var unitOfWork = new OrderingUnitOfWork(_command, _query);
            var projector = new OrderProjector(_command, _query, frameworkClock);

            Orders = new OrderRepository(_command);
            var reservations = new FulfillmentReservationRepository(_command);
            var tickets = new ElectronicTicketRepository(_command);
            var miscDocuments = new ElectronicMiscDocumentRepository(_command);
            var stocks = new DocumentStockRepository(_command);

            var options = Options.Create(new OrderOperationOptions
            {
                RecoveryLeaseSeconds = 900,
                TicketDocumentType = TicketDocumentType,
                EmdDocumentType = EmdDocumentType
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
            var ticketIssuer = new ElectronicTicketIssuer(tickets, Documents, coordinator, unitOfWork, Ids, frameworkClock);
            var miscDocumentIssuer = new ElectronicMiscDocumentIssuer(miscDocuments, MiscDocuments, coordinator, unitOfWork, Ids, frameworkClock);

            MiscDocumentRepository = miscDocuments;

            Issue = new IssueOrderService(Orders, reservations, tickets, miscDocuments, stocks, Funding, ticketIssuer, miscDocumentIssuer, coordinator, operationStore, receipts, homeOperator, unitOfWork, frameworkClock, projector, options);
            Create = new CreateOrderService(Orders, receipts, coordinator, unitOfWork, Ids, frameworkClock, projector, homeOperator);
            Withdraw = new WithdrawOrderService(Orders, reservations, tickets, Reservation, Funding, coordinator, new StubIdentity(), unitOfWork, Ids, frameworkClock, projector);
            OrderChange = new OrderChangeService(Orders, Quotes, coordinator, caller, unitOfWork, Ids, frameworkClock, projector);
            AccessGuard = new OrderCustomerAccessGuard(Orders, caller);
            var releaseCoordinator = new ReservationReleaseCoordinator(reservations, Reservation, coordinator, frameworkClock);

            CancellationQuotes = new DeterministicOrderCancellationQuoteAdapter();
            DocumentVoids = new DeterministicDocumentVoidAdapter();
            VoidDocument = new Application.OrderAggregate.Services.DocumentVoid.DocumentVoidService(
                Orders, tickets, miscDocuments, DocumentVoids, coordinator, operationStore, receipts, unitOfWork, frameworkClock, projector);
            Cancel = new OrderCancelService(Orders, tickets, miscDocuments, releaseCoordinator, coordinator, operationStore, receipts, unitOfWork, Ids, frameworkClock, projector);
            ScopeCancel = new OrderScopeCancellationService(Orders, tickets, miscDocuments, CancellationQuotes, releaseCoordinator, coordinator, operationStore, receipts, caller, unitOfWork, Ids, frameworkClock, projector);

            RefundQuotes = new DeterministicRefundQuoteAdapter();
            DocumentRefunds = new DeterministicDocumentRefundAdapter();
            RefundValues = new DeterministicRefundValueAdapter();
            var refundValueCoordinator = new RefundValueMovementCoordinator(RefundValues, coordinator, frameworkClock);
            ManualRefundAuthorizations = new DeterministicManualRefundAuthorizationAdapter();
            var manualRefundAuthorizer = new ManualRefundAuthorizer(ManualRefundAuthorizations, caller);
            ChangeQuotes = new DeterministicChangeQuoteAdapter();
            ReservationChanges = new DeterministicReservationChangeAdapter();
            DocumentChangeEligibilities = new DeterministicDocumentChangeEligibilityAdapter();
            DocumentRevalidations = new DeterministicDocumentRevalidationAdapter();
            ChangePlans = new AcceptedChangePlanStore(_command, frameworkClock);
            VoluntaryChange = new VoluntaryChangeService(
                Orders, tickets, ChangeQuotes, ReservationChanges, DocumentChangeEligibilities,
                DocumentRevalidations, ChangePlans, coordinator, operationStore, receipts, caller,
                unitOfWork, Ids, frameworkClock, projector);
            DocumentRefundCorrections = new DeterministicDocumentRefundCorrectionAdapter();
            RefundValueCorrections = new DeterministicRefundValueCorrectionAdapter();
            CancelRefundAuthorizations = new DeterministicCancelRefundAuthorizationAdapter();
            CancelRefund = new CancelRefundService(
                Orders,
                tickets,
                DocumentRefundCorrections,
                new RefundValueCorrectionCoordinator(RefundValueCorrections, coordinator, frameworkClock),
                new CancelRefundAuthorizer(CancelRefundAuthorizations, caller),
                coordinator,
                operationStore,
                receipts,
                caller,
                unitOfWork,
                Ids,
                frameworkClock,
                projector);
            Refund = new RefundService(Orders, tickets, RefundQuotes, DocumentRefunds, refundValueCoordinator, manualRefundAuthorizer, coordinator, operationStore, receipts, caller, unitOfWork, Ids, frameworkClock, projector);
        }

        public SequentialIdGenerator Ids { get; }

        public TestClock Clock { get; }

        public DeterministicReservationAdapter Reservation { get; }

        public DeterministicFundingCoverageAdapter Funding { get; }

        public DeterministicDocumentIssuanceAdapter Documents { get; }

        public DeterministicEmdIssuanceAdapter MiscDocuments { get; }

        public OutboxDomainEventDispatcher Events { get; }

        public ElectronicMiscDocumentRepository MiscDocumentRepository { get; } = default!;

        public DeterministicOrderChangeQuoteAdapter Quotes { get; }

        public OrderRepository Orders { get; }

        public FulfillmentReservationRepository Reservations { get; }

        public ElectronicTicketRepository Tickets { get; }

        public DocumentStockRepository Stocks { get; }

        public OrderingUnitOfWork UnitOfWork { get; }

        public OrderProjector Projector { get; }

        public IReserveOrderService Reserve { get; }

        public IIssueOrderService Issue { get; }

        public IWithdrawOrderService Withdraw { get; }

        public IOrderChangeService OrderChange { get; }

        public IOrderCustomerAccessGuard AccessGuard { get; }

        public IOrderCancelService Cancel { get; }

        public IOrderScopeCancellationService ScopeCancel { get; }

        public DeterministicOrderCancellationQuoteAdapter CancellationQuotes { get; }

        public DeterministicDocumentVoidAdapter DocumentVoids { get; }

        public Application.OrderAggregate.Services.DocumentVoid.IDocumentVoidService VoidDocument { get; }

        public DeterministicRefundQuoteAdapter RefundQuotes { get; }

        public DeterministicDocumentRefundAdapter DocumentRefunds { get; }

        public DeterministicRefundValueAdapter RefundValues { get; }

        public DeterministicManualRefundAuthorizationAdapter ManualRefundAuthorizations { get; }

        public DeterministicDocumentRefundCorrectionAdapter DocumentRefundCorrections { get; }

        public DeterministicChangeQuoteAdapter ChangeQuotes { get; }

        public DeterministicReservationChangeAdapter ReservationChanges { get; }

        public DeterministicDocumentChangeEligibilityAdapter DocumentChangeEligibilities { get; }

        public DeterministicDocumentRevalidationAdapter DocumentRevalidations { get; }

        public AcceptedChangePlanStore ChangePlans { get; }

        public IVoluntaryChangeService VoluntaryChange { get; }

        public DeterministicRefundValueCorrectionAdapter RefundValueCorrections { get; }

        public DeterministicCancelRefundAuthorizationAdapter CancelRefundAuthorizations { get; }

        public ICancelRefundService CancelRefund { get; }

        public IRefundService Refund { get; }

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

            await SeedStockAsync(command, airlineId, TicketDocumentType, "T", rangeFrom, rangeTo);
            await SeedStockAsync(command, airlineId, EmdDocumentType, "M", rangeFrom, rangeTo);

            await command.SaveChangesAsync();
        }

        private async Task SeedStockAsync(
            OrderingDbContext command,
            long airlineId,
            string documentType,
            string prefix,
            long rangeFrom,
            long rangeTo)
        {
            var alreadyStocked = await command.Set<DocumentStock>()
                .AsNoTracking()
                .AnyAsync(stock => stock.OwnerAirlineId == airlineId
                                   && stock.DocumentType == documentType
                                   && stock.Status == DocumentStockStatus.Active);

            if (alreadyStocked)
                return;

            command.Set<DocumentStock>().Add(DocumentStock.Define(
                Ids.NewId(),
                airlineId,
                null,
                documentType,
                $"{prefix}{Interlocked.Increment(ref _stockSequence):D4}",
                10,
                DocumentStock.NoCheckDigitProfile,
                rangeFrom,
                rangeTo));
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
