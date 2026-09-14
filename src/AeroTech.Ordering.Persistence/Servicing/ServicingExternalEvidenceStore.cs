using System.Data;
using System.Data.Common;
using System.Transactions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingExternalEvidenceStore : IServicingExternalEvidenceStore
    {
        private const int UpsertAttempts = 3;

        private const string SupersedeSql =
            @"UPDATE [Order].[ServicingExternalEvidences]
                 SET [Outcome] = @outcome,
                     [ProviderReference] = COALESCE(@reference, [ProviderReference]),
                     [Detail] = COALESCE(@detail, [Detail]),
                     [DocumentKind] = COALESCE(@kind, [DocumentKind]),
                     [DocumentNumber] = COALESCE(@number, [DocumentNumber]),
                     [UpdatedAt] = @now
               WHERE [OperationId] = @operationId
                 AND [Stage] = @stage
                 AND [Outcome] IN ({0})";

        private const string InsertSql =
            @"INSERT INTO [Order].[ServicingExternalEvidences]
                  ([OperationId], [Stage], [Outcome], [ProviderReference], [Detail],
                   [DocumentKind], [DocumentNumber], [RecordedAt], [UpdatedAt])
              SELECT @operationId, @stage, @outcome, @reference, @detail, @kind, @number, @now, @now
               WHERE NOT EXISTS (
                     SELECT 1 FROM [Order].[ServicingExternalEvidences]
                      WHERE [OperationId] = @operationId AND [Stage] = @stage)";

        private const string ReadSql =
            @"SELECT [Outcome], [ProviderReference], [Detail], [DocumentKind], [DocumentNumber], [RecordedAt]
                FROM [Order].[ServicingExternalEvidences]
               WHERE [OperationId] = @operationId AND [Stage] = @stage";

        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        public ServicingExternalEvidenceStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task<ServicingEvidenceRecording> RecordAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind = null,
            string? documentNumber = null,
            CancellationToken cancellationToken = default)
        {
            var attempted = new ServicingExternalEvidence(
                operationId, stage, outcome, providerReference, detail, documentKind, documentNumber,
                _clock.GetDateTime());

            var superseded = ServicingEvidencePolicy.OutcomesSupersededBy(outcome);

            using var suppressed = new TransactionScope(
                TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);

            await using var connection = IndependentConnection(operationId, stage);
            await connection.OpenAsync(cancellationToken);

            for (var attempt = 0; attempt < UpsertAttempts; attempt++)
            {
                if (superseded.Count > 0
                    && await SupersedeAsync(connection, attempted, superseded, cancellationToken) > 0)
                    return new ServicingEvidenceRecording(
                        attempted, await DurableAsync(connection, attempted, cancellationToken), true);

                var durable = await ReadAsync(connection, attempted, cancellationToken);

                if (durable is not null && !superseded.Contains(durable.Outcome))
                    return new ServicingEvidenceRecording(attempted, durable, false);

                if (durable is null && await TryInsertAsync(connection, attempted, cancellationToken))
                    return new ServicingEvidenceRecording(attempted, attempted, true);
            }

            throw ExceptionFactory.ServicingEvidenceNotRecorded(operationId, stage);
        }

        public async Task<IReadOnlyList<ServicingExternalEvidence>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default)
            => await _dbContext.Set<ServicingExternalEvidenceRow>()
                .AsNoTracking()
                .Where(row => row.OperationId == operationId)
                .OrderBy(row => row.Stage)
                .Select(row => new ServicingExternalEvidence(
                    row.OperationId,
                    row.Stage,
                    row.Outcome,
                    row.ProviderReference,
                    row.Detail,
                    row.DocumentKind,
                    row.DocumentNumber,
                    row.RecordedAt))
                .ToListAsync(cancellationToken);

        private DbConnection IndependentConnection(long operationId, ServicingEvidenceStage stage)
        {
            var factory = DbProviderFactories.GetFactory(_dbContext.Database.GetDbConnection())
                          ?? throw ExceptionFactory.ServicingEvidenceNotRecorded(operationId, stage);

            var connection = factory.CreateConnection()
                             ?? throw ExceptionFactory.ServicingEvidenceNotRecorded(operationId, stage);

            connection.ConnectionString = _dbContext.Database.GetConnectionString()
                                          ?? throw ExceptionFactory.ServicingEvidenceNotRecorded(operationId, stage);

            return connection;
        }

        private static async Task<int> SupersedeAsync(
            DbConnection connection,
            ServicingExternalEvidence attempted,
            IReadOnlyList<ProviderOperationOutcome> superseded,
            CancellationToken cancellationToken)
        {
            var placeholders = string.Join(", ", superseded.Select((_, index) => $"@superseded{index}"));

            await using var command = Bind(connection, string.Format(SupersedeSql, placeholders), attempted);

            for (var index = 0; index < superseded.Count; index++)
                Add(command, $"@superseded{index}", DbType.Int32, (int)superseded[index]);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<bool> TryInsertAsync(
            DbConnection connection,
            ServicingExternalEvidence attempted,
            CancellationToken cancellationToken)
        {
            try
            {
                await using var command = Bind(connection, InsertSql, attempted);

                return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
            }
            catch (DbException)
            {
                if (await ReadAsync(connection, attempted, cancellationToken) is not null)
                    return false;

                throw;
            }
        }

        private static async Task<ServicingExternalEvidence> DurableAsync(
            DbConnection connection,
            ServicingExternalEvidence attempted,
            CancellationToken cancellationToken)
            => await ReadAsync(connection, attempted, cancellationToken)
               ?? throw ExceptionFactory.ServicingEvidenceNotRecorded(attempted.OperationId, attempted.Stage);

        private static async Task<ServicingExternalEvidence?> ReadAsync(
            DbConnection connection,
            ServicingExternalEvidence attempted,
            CancellationToken cancellationToken)
        {
            await using var command = Bind(connection, ReadSql, attempted);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new ServicingExternalEvidence(
                attempted.OperationId,
                attempted.Stage,
                (ProviderOperationOutcome)reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : (AccountableDocumentKind)reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5));
        }

        private static DbCommand Bind(DbConnection connection, string sql, ServicingExternalEvidence evidence)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;

            Add(command, "@operationId", DbType.Int64, evidence.OperationId);
            Add(command, "@stage", DbType.Int32, (int)evidence.Stage);
            Add(command, "@outcome", DbType.Int32, (int)evidence.Outcome);
            Add(command, "@reference", DbType.String, evidence.ProviderReference);
            Add(command, "@detail", DbType.String, evidence.Detail);
            Add(command, "@kind", DbType.Int32, (int?)evidence.DocumentKind);
            Add(command, "@number", DbType.String, evidence.DocumentNumber);
            Add(command, "@now", DbType.DateTimeOffset, evidence.RecordedAt);

            return command;
        }

        private static void Add(DbCommand command, string name, DbType type, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
