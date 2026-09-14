using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingEvidenceConcurrencyTests
    {
        private const ServicingEvidenceStage Stage = ServicingEvidenceStage.DocumentVoid;
        private const string ConfirmedReference = "VOID-CONFIRMED";
        private const string ConfirmedDetail = "issuer confirmed";
        private const int RaceRepetitions = 20;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly long _operationId;

        public ServicingEvidenceConcurrencyTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _operationId = NewOperationId();
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Unknown)]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Rejected)]
        public async Task C1_C2_C3_A_stale_writer_cannot_downgrade_a_confirmed_row(
            ProviderOperationOutcome loser)
        {
            await using var seedContext = _fixture.NewCommandContext();
            await using var winnerContext = _fixture.NewCommandContext();
            await using var loserContext = _fixture.NewCommandContext();

            var seed = Store(seedContext);
            var winner = Store(winnerContext);
            var stale = Store(loserContext);

            await seed.RecordAsync(_operationId, Stage, ProviderOperationOutcome.Pending, null, null);

            await ObserveAsync(winnerContext);
            await ObserveAsync(loserContext);

            await winner.RecordAsync(_operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);
            var recording = await stale.RecordAsync(_operationId, Stage, loser, "STALE", "stale attempt");

            Assert.False(recording.Applied);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recording.Durable.Outcome);
            Assert.Equal(loser == ProviderOperationOutcome.Rejected, ServicingEvidencePolicy.Contradicts(recording));

            await AssertConfirmedAsync();
        }

        [Fact]
        public async Task C4_A_confirmed_writer_upgrades_a_weaker_row_written_first()
        {
            await using var firstContext = _fixture.NewCommandContext();
            await using var secondContext = _fixture.NewCommandContext();

            await Store(firstContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Unknown, "WEAK", "weak attempt");

            var upgrade = await Store(secondContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            Assert.True(upgrade.Applied);
            Assert.False(ServicingEvidencePolicy.Contradicts(upgrade));

            await AssertConfirmedAsync();
        }

        [Fact]
        public async Task C5_An_exact_confirmed_replay_is_a_no_op()
        {
            await using var firstContext = _fixture.NewCommandContext();

            await Store(firstContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            var before = await RowAsync();

            await using var replayContext = _fixture.NewCommandContext();
            var later = new TestClock();

            later.Advance(TimeSpan.FromHours(3));

            var replay = await new ServicingExternalEvidenceStore(replayContext, later).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            Assert.False(replay.Applied);
            Assert.False(ServicingEvidencePolicy.Contradicts(replay));

            var after = await RowAsync();

            Assert.Equal(before.Outcome, after.Outcome);
            Assert.Equal(before.ProviderReference, after.ProviderReference);
            Assert.Equal(before.Detail, after.Detail);
            Assert.Equal(before.RecordedAt, after.RecordedAt);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        }

        [Fact]
        public async Task C6_An_insert_race_won_by_the_confirmed_writer_keeps_confirmed()
        {
            await using var confirmedContext = _fixture.NewCommandContext();
            await using var weakContext = _fixture.NewCommandContext();

            await Store(confirmedContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            var stale = await Store(weakContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Unknown, "STALE", "stale attempt");

            Assert.False(stale.Applied);
            Assert.False(ServicingEvidencePolicy.Contradicts(stale));

            await AssertConfirmedAsync();
        }

        [Fact]
        public async Task C7_An_insert_race_won_by_the_weaker_writer_still_ends_confirmed()
        {
            await using var weakContext = _fixture.NewCommandContext();
            await using var confirmedContext = _fixture.NewCommandContext();

            await Store(weakContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Unknown, "WEAK", "weak attempt");

            var confirmation = await Store(confirmedContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            Assert.True(confirmation.Applied);

            await AssertConfirmedAsync();
        }

        [Fact]
        public async Task C8_Two_confirmed_writers_with_the_same_identity_leave_one_row()
        {
            await using var firstContext = _fixture.NewCommandContext();
            await using var secondContext = _fixture.NewCommandContext();

            await Store(firstContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            var second = await Store(secondContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            Assert.False(second.Applied);
            Assert.False(ServicingEvidencePolicy.Contradicts(second));

            await AssertConfirmedAsync();
        }

        [Fact]
        public async Task C9_A_contradictory_confirmed_identity_is_flagged_and_never_overwrites_the_durable_one()
        {
            await using var firstContext = _fixture.NewCommandContext();
            await using var secondContext = _fixture.NewCommandContext();

            await Store(firstContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            var before = await RowAsync();

            var contradiction = await Store(secondContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, "VOID-OTHER", "a different issuer answer");

            Assert.False(contradiction.Applied);
            Assert.True(ServicingEvidencePolicy.Contradicts(contradiction));
            Assert.Equal(ConfirmedReference, contradiction.Durable.ProviderReference);

            var after = await RowAsync();

            Assert.Equal(ProviderOperationOutcome.Confirmed, after.Outcome);
            Assert.Equal(ConfirmedReference, after.ProviderReference);
            Assert.Equal(ConfirmedDetail, after.Detail);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        }

        [Fact]
        public async Task C4b_An_upgrade_that_omits_a_field_keeps_the_durable_value()
        {
            await using var firstContext = _fixture.NewCommandContext();
            await using var secondContext = _fixture.NewCommandContext();

            await Store(firstContext).RecordAsync(
                _operationId,
                Stage,
                ProviderOperationOutcome.Pending,
                "EARLY-REFERENCE",
                "early detail",
                AccountableDocumentKind.ElectronicTicket,
                "1234567890123");

            await Store(secondContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Unknown, null, null);

            var row = await RowAsync();

            Assert.Equal(ProviderOperationOutcome.Unknown, row.Outcome);
            Assert.Equal("EARLY-REFERENCE", row.ProviderReference);
            Assert.Equal("early detail", row.Detail);
            Assert.Equal(AccountableDocumentKind.ElectronicTicket, row.DocumentKind);
            Assert.Equal("1234567890123", row.DocumentNumber);
        }

        [Fact]
        public async Task C9b_A_parallel_burst_of_unresolved_writers_and_one_confirmation_leaves_exactly_one_confirmed_row()
        {
            var outcomes = new[]
            {
                ProviderOperationOutcome.Unknown,
                ProviderOperationOutcome.Pending,
                ProviderOperationOutcome.Confirmed,
                ProviderOperationOutcome.Pending,
                ProviderOperationOutcome.Unknown
            };

            await Task.WhenAll(outcomes.Select(outcome => RecordInOwnContextAsync(_operationId, outcome)));

            await AssertConfirmedAsync();
        }

        [Fact]
        public async Task C10a_A_pending_and_an_unknown_racing_on_the_first_insert_always_leave_unknown()
        {
            for (var repetition = 0; repetition < RaceRepetitions; repetition++)
            {
                var operationId = NewOperationId();

                await Task.WhenAll(
                    RecordInOwnContextAsync(operationId, ProviderOperationOutcome.Pending),
                    RecordInOwnContextAsync(operationId, ProviderOperationOutcome.Unknown));

                Assert.Equal(ProviderOperationOutcome.Unknown, (await SingleEvidenceAsync(operationId)).Outcome);
            }
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending, ProviderOperationOutcome.Unknown, true)]
        [InlineData(ProviderOperationOutcome.Unknown, ProviderOperationOutcome.Pending, false)]
        public async Task C10c_Sequential_unresolved_answers_settle_on_unknown(
            ProviderOperationOutcome first,
            ProviderOperationOutcome second,
            bool secondApplies)
        {
            await RecordInOwnContextAsync(_operationId, first);

            var recording = await RecordInOwnContextAsync(_operationId, second);

            Assert.Equal(secondApplies, recording.Applied);
            Assert.False(ServicingEvidencePolicy.Contradicts(recording));
            Assert.Equal(ProviderOperationOutcome.Unknown, (await SingleEvidenceAsync(_operationId)).Outcome);
        }

        [Fact]
        public async Task C10b_A_rejected_row_is_never_upgraded_by_a_later_confirmed_answer()
        {
            await using var rejectingContext = _fixture.NewCommandContext();
            await using var confirmingContext = _fixture.NewCommandContext();

            await Store(rejectingContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Rejected, "REJECTED-FIRST", "provider said no");

            var before = await RowAsync();

            var confirmation = await Store(confirmingContext).RecordAsync(
                _operationId, Stage, ProviderOperationOutcome.Confirmed, ConfirmedReference, ConfirmedDetail);

            Assert.False(confirmation.Applied);
            Assert.True(ServicingEvidencePolicy.Contradicts(confirmation));

            var after = await RowAsync();

            Assert.Equal(ProviderOperationOutcome.Rejected, after.Outcome);
            Assert.Equal("REJECTED-FIRST", after.ProviderReference);
            Assert.Equal("provider said no", after.Detail);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        }

        [Fact]
        public async Task C10d_A_confirmed_and_a_rejected_racing_keep_one_terminal_row_and_flag_the_other_writer()
        {
            for (var repetition = 0; repetition < RaceRepetitions; repetition++)
            {
                var operationId = NewOperationId();

                var recordings = await Task.WhenAll(
                    RecordInOwnContextAsync(operationId, ProviderOperationOutcome.Confirmed),
                    RecordInOwnContextAsync(operationId, ProviderOperationOutcome.Rejected));

                var kept = Assert.Single(recordings, recording => !ServicingEvidencePolicy.Contradicts(recording));

                Assert.True(kept.Applied);
                Assert.Equal(kept.Attempted.Outcome, (await SingleEvidenceAsync(operationId)).Outcome);
            }
        }

        private async Task<ServicingEvidenceRecording> RecordInOwnContextAsync(
            long operationId,
            ProviderOperationOutcome outcome)
        {
            await using var context = _fixture.NewCommandContext();

            return await Store(context).RecordAsync(
                operationId,
                Stage,
                outcome,
                outcome == ProviderOperationOutcome.Confirmed ? ConfirmedReference : "WEAK",
                outcome == ProviderOperationOutcome.Confirmed ? ConfirmedDetail : "weak attempt");
        }

        private async Task ObserveAsync(Persistence.OrderingDbContext context)
        {
            var observed = await context.Set<ServicingExternalEvidenceRow>()
                .SingleAsync(row => row.OperationId == _operationId && row.Stage == Stage);

            Assert.Equal(ProviderOperationOutcome.Pending, observed.Outcome);
        }

        private static ServicingExternalEvidenceStore Store(Persistence.OrderingDbContext context)
            => new(context, new TestClock());

        private static long NewOperationId() => Random.Shared.NextInt64(700_000_000_000, 799_999_999_999);

        private async Task<ServicingExternalEvidence> AssertConfirmedAsync()
        {
            var row = await SingleEvidenceAsync(_operationId);

            Assert.Equal(Stage, row.Stage);
            Assert.Equal(ProviderOperationOutcome.Confirmed, row.Outcome);
            Assert.True(row.IsConfirmed);

            return row;
        }

        private async Task<ServicingExternalEvidence> SingleEvidenceAsync(long operationId)
        {
            await using var context = _fixture.NewCommandContext();

            return Assert.Single(await Store(context).ListAsync(operationId));
        }

        private async Task<ServicingExternalEvidenceRow> RowAsync()
        {
            await using var context = _fixture.NewCommandContext();

            return await context.Set<ServicingExternalEvidenceRow>()
                .AsNoTracking()
                .SingleAsync(row => row.OperationId == _operationId && row.Stage == Stage);
        }
    }
}
