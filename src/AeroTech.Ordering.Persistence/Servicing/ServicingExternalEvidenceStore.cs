using System.Data;
using System.Data.Common;
using System.Transactions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingExternalEvidenceStore : IServicingExternalEvidenceStore
    {
        private const int UpsertAttempts = 3;

        private const string UpgradeSql =
            @"UPDATE [Order].[ServicingExternalEvidences]
                 SET [Outcome] = @outcome,
                     [ProviderReference] = COALESCE(@reference, [ProviderReference]),
                     [Detail] = COALESCE(@detail, [Detail]),
                     [DocumentKind] = COALESCE(@kind, [DocumentKind]),
                     [DocumentNumber] = COALESCE(@number, [DocumentNumber]),
                     [UpdatedAt] = @now
               WHERE [OperationId] = @operationId
                 AND [Stage] = @stage
                 AND [Outcome] <> @confirmed";

        private const string InsertSql =
            @"INSERT INTO [Order].[ServicingExternalEvidences]
                  ([OperationId], [Stage], [Outcome], [ProviderReference], [Detail],
                   [DocumentKind], [DocumentNumber], [RecordedAt], [UpdatedAt])
              SELECT @operationId, @stage, @outcome, @reference, @detail, @kind, @number, @now, @now
               WHERE NOT EXISTS (
                     SELECT 1 FROM [Order].[ServicingExternalEvidences]
                      WHERE [OperationId] = @operationId AND [Stage] = @stage)";

        private const string ConfirmedSql =
            @"SELECT COUNT(1) FROM [Order].[ServicingExternalEvidences]
               WHERE [OperationId] = @operationId AND [Stage] = @stage AND [Outcome] = @confirmed";

        private const string ExistsSql =
            @"SELECT COUNT(1) FROM [Order].[ServicingExternalEvidences]
               WHERE [OperationId] = @operationId AND [Stage] = @stage";

        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        public ServicingExternalEvidenceStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task RecordAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind = null,
            string? documentNumber = null,
            CancellationToken cancellationToken = default)
        {
            var parameters = new EvidenceParameters(
                operationId, stage, outcome, providerReference, detail, documentKind, documentNumber,
                _clock.GetDateTime());

            using var suppressed = new TransactionScope(
                TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);

            await using var connection = IndependentConnection(operationId, stage);
            await connection.OpenAsync(cancellationToken);

            for (var attempt = 0; attempt < UpsertAttempts; attempt++)
            {
                if (await ExecuteAsync(connection, UpgradeSql, parameters, cancellationToken) > 0)
                    return;

                if (await CountAsync(connection, ConfirmedSql, parameters, cancellationToken) > 0)
                    return;

                if (await TryInsertAsync(connection, parameters, cancellationToken))
                    return;
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

        private static async Task<bool> TryInsertAsync(
            DbConnection connection,
            EvidenceParameters parameters,
            CancellationToken cancellationToken)
        {
            try
            {
                return await ExecuteAsync(connection, InsertSql, parameters, cancellationToken) > 0;
            }
            catch (DbException)
            {
                if (await CountAsync(connection, ExistsSql, parameters, cancellationToken) > 0)
                    return false;

                throw;
            }
        }

        private static async Task<int> ExecuteAsync(
            DbConnection connection,
            string sql,
            EvidenceParameters parameters,
            CancellationToken cancellationToken)
        {
            await using var command = parameters.Bind(connection, sql);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<int> CountAsync(
            DbConnection connection,
            string sql,
            EvidenceParameters parameters,
            CancellationToken cancellationToken)
        {
            await using var command = parameters.Bind(connection, sql);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        private sealed record EvidenceParameters(
            long OperationId,
            ServicingEvidenceStage Stage,
            ProviderOperationOutcome Outcome,
            string? ProviderReference,
            string? Detail,
            AccountableDocumentKind? DocumentKind,
            string? DocumentNumber,
            DateTimeOffset Now)
        {
            public DbCommand Bind(DbConnection connection, string sql)
            {
                var command = connection.CreateCommand();
                command.CommandText = sql;

                Add(command, "@operationId", DbType.Int64, OperationId);
                Add(command, "@stage", DbType.Int32, (int)Stage);
                Add(command, "@outcome", DbType.Int32, (int)Outcome);
                Add(command, "@reference", DbType.String, ProviderReference);
                Add(command, "@detail", DbType.String, Detail);
                Add(command, "@kind", DbType.Int32, (int?)DocumentKind);
                Add(command, "@number", DbType.String, DocumentNumber);
                Add(command, "@now", DbType.DateTimeOffset, Now);
                Add(command, "@confirmed", DbType.Int32, (int)ProviderOperationOutcome.Confirmed);

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
}
