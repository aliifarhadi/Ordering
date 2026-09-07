using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.ReferenceData.ReadModels;
using AeroTech.Ordering.ServiceHost.OperatorContext;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Operations
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class HomeOperatorIdentityTests
    {
        private const long HomeAirlineId = 7401;

        private readonly OrderingDatabaseFixture _fixture;

        public HomeOperatorIdentityTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_owner_airline_comes_from_the_trusted_core_operator_projection()
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            await using var reference = _fixture.NewReferenceContext();

            var resolved = await new ReferenceDataHomeOperatorProvider(reference).GetOwnerAirlineIdAsync();

            Assert.Equal(HomeAirlineId, resolved);
        }

        [Fact]
        public async Task A_missing_operator_projection_fails_closed()
        {
            await ClearHomeOperatorAsync();

            await using var reference = _fixture.NewReferenceContext();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => new ReferenceDataHomeOperatorProvider(reference).GetOwnerAirlineIdAsync());

            Assert.Equal(2710, error.Code);
            Assert.Contains(OperatorScopeKey.HomeOperator, error.Message);

            await SeedHomeOperatorAsync(HomeAirlineId);
        }

        [Fact]
        public async Task The_operator_row_is_selected_by_its_scope_key_not_by_position()
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            await using (var reference = _fixture.NewReferenceContext())
            {
                if (!await reference.OperatorSettings.AnyAsync(row => row.ScopeKey == "OTHER_SCOPE"))
                {
                    reference.OperatorSettings.Add(new OperatorSettingsReadModel
                    {
                        Id = 999_999,
                        ScopeKey = "OTHER_SCOPE",
                        HomeAirlineId = 999,
                        LastUpdateTime = DateTimeOffset.UtcNow
                    });

                    await reference.SaveChangesAsync();
                }
            }

            await using var verification = _fixture.NewReferenceContext();

            Assert.Equal(HomeAirlineId, await new ReferenceDataHomeOperatorProvider(verification).GetOwnerAirlineIdAsync());
        }

        [Theory]
        [MemberData(nameof(CallerContexts))]
        public async Task Every_caller_context_stamps_the_same_deployment_owner_airline(ICallerContext caller)
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            var receipt = await AcquireAsync(caller, Guid.NewGuid().ToString("N"));

            Assert.Equal(HomeAirlineId, receipt.OwnerAirlineId);
        }

        [Fact]
        public async Task A_replay_returns_the_original_receipt()
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            var caller = TestCallerContexts.AgencyUser(11, "subject-a");
            var key = Guid.NewGuid().ToString("N");

            var first = await AcquireAsync(caller, key);
            var second = await AcquireAsync(caller, key);

            Assert.False(first.IsReplay);
            Assert.True(second.IsReplay);
            Assert.Equal(first.ReceiptId, second.ReceiptId);
        }

        [Fact]
        public async Task The_same_key_with_a_different_payload_is_a_conflict()
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            var caller = TestCallerContexts.AgencyUser(11, "subject-a");
            var key = Guid.NewGuid().ToString("N");

            await AcquireAsync(caller, key, requestHash: "hash-a");

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => AcquireAsync(caller, key, requestHash: "hash-b"));

            Assert.Equal(2703, error.Code);
        }

        [Fact]
        public async Task Two_callers_sharing_a_key_receive_separate_receipts_under_one_owner_airline()
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            var key = Guid.NewGuid().ToString("N");

            var agency = await AcquireAsync(TestCallerContexts.AgencyUser(11, "subject-a"), key);
            var airline = await AcquireAsync(TestCallerContexts.AirlineUser(11, "subject-a"), key);

            Assert.NotEqual(agency.ReceiptId, airline.ReceiptId);
            Assert.NotEqual(agency.CallerScope, airline.CallerScope);
            Assert.Equal(HomeAirlineId, agency.OwnerAirlineId);
            Assert.Equal(HomeAirlineId, airline.OwnerAirlineId);
        }

        [Fact]
        public async Task A_servicing_operation_is_stamped_with_the_trusted_owner_airline()
        {
            await SeedHomeOperatorAsync(HomeAirlineId);

            await using var command = _fixture.NewCommandContext();
            await using var reference = _fixture.NewReferenceContext();

            var store = new ServicingOperationStore(
                command,
                new ReferenceDataHomeOperatorProvider(reference),
                new OrderingDatabaseFixture.FixedClock());

            var operation = await store.PrepareAsync(
                NewId(), NewId(), ServicingOperationKind.Cancel, "hash", claimGeneration: 1);

            Assert.Equal(HomeAirlineId, operation.OwnerAirlineId);
            Assert.Equal(ServicingOperationStatus.Prepared, operation.Status);
        }

        public static TheoryData<ICallerContext> CallerContexts() =>
        [
            TestCallerContexts.AirlineUser(31, "subject-airline"),
            TestCallerContexts.AgencyUser(41, "subject-agency"),
            TestCallerContexts.Individual(51, "subject-individual"),
            TestCallerContexts.AgencyApiClient(61, "client-api")
        ];

        private async Task<Domain._Shared.Operations.Contracts.CommandReceiptResult> AcquireAsync(
            ICallerContext caller,
            string idempotencyKey,
            string requestHash = "hash")
        {
            await using var command = _fixture.NewCommandContext();
            await using var reference = _fixture.NewReferenceContext();

            var store = new CommandReceiptStore(
                command,
                new ReferenceDataHomeOperatorProvider(reference),
                caller,
                new TestIdGenerator(),
                new OrderingDatabaseFixture.FixedClock());

            return await store.AcquireAsync("CreateOrder", idempotencyKey, requestHash);
        }

        private async Task SeedHomeOperatorAsync(long homeAirlineId)
        {
            await using var reference = _fixture.NewReferenceContext();

            var existing = await reference.OperatorSettings
                .SingleOrDefaultAsync(row => row.ScopeKey == OperatorScopeKey.HomeOperator);

            if (existing is null)
            {
                reference.OperatorSettings.Add(new OperatorSettingsReadModel
                {
                    Id = 1,
                    ScopeKey = OperatorScopeKey.HomeOperator,
                    HomeAirlineId = homeAirlineId,
                    LastUpdateTime = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.HomeAirlineId = homeAirlineId;
            }

            await reference.SaveChangesAsync();
        }

        private async Task ClearHomeOperatorAsync()
        {
            await using var reference = _fixture.NewReferenceContext();

            await reference.OperatorSettings
                .Where(row => row.ScopeKey == OperatorScopeKey.HomeOperator)
                .ExecuteDeleteAsync();
        }

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000);

        private sealed class TestIdGenerator : IIdGenerator
        {
            private long _next = DateTime.UtcNow.Ticks;

            public long NewId() => Interlocked.Increment(ref _next);
        }
    }
}
