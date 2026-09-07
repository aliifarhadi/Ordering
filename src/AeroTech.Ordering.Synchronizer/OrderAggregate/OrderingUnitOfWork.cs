using System.Data;
using System.Data.Common;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Query._Shared.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AeroTech.Ordering.Synchronizer.OrderAggregate
{
    public sealed class OrderingUnitOfWork : IUnitOfWork
    {
        private readonly OrderingDbContext _commandDbContext;
        private readonly OrderQueryDbContext _queryDbContext;

        public OrderingUnitOfWork(OrderingDbContext commandDbContext, OrderQueryDbContext queryDbContext)
        {
            _commandDbContext = commandDbContext;
            _queryDbContext = queryDbContext;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_commandDbContext.Database.CurrentTransaction is { } ambient)
                return await SaveBothAsync(ambient.GetDbTransaction(), cancellationToken);

            ShareConnection();

            var connection = _commandDbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var transaction = await _commandDbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var affected = await SaveBothAsync(transaction.GetDbTransaction(), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return affected;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<int> SaveBothAsync(DbTransaction transaction, CancellationToken cancellationToken)
        {
            ShareConnection();
            await _queryDbContext.Database.UseTransactionAsync(transaction, cancellationToken);

            try
            {
                var affected = await _commandDbContext.SaveChangesAsync(cancellationToken);
                await _queryDbContext.SaveChangesAsync(cancellationToken);

                return affected;
            }
            finally
            {
                await DetachQueryTransactionAsync(cancellationToken);
            }
        }

        private async Task DetachQueryTransactionAsync(CancellationToken cancellationToken)
        {
            if (_queryDbContext.Database.CurrentTransaction is not null)
                await _queryDbContext.Database.UseTransactionAsync(null, cancellationToken);
        }

        private void ShareConnection()
        {
            var commandConnection = _commandDbContext.Database.GetDbConnection();

            if (!ReferenceEquals(_queryDbContext.Database.GetDbConnection(), commandConnection))
                _queryDbContext.Database.SetDbConnection(commandConnection, contextOwnsConnection: false);
        }
    }
}
