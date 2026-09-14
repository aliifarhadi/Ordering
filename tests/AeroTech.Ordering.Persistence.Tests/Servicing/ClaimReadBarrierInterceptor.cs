using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AeroTech.Ordering.Persistence.Tests.Servicing
{
    internal sealed class ClaimReadBarrierInterceptor : DbCommandInterceptor
    {
        private readonly Func<Task> _whileBarred;
        private int _barriers;

        public ClaimReadBarrierInterceptor(Func<Task> whileBarred) => _whileBarred = whileBarred;

        public int Barriers => _barriers;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("OperationOrderClaims", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref _barriers, 1, 0) == 0)
                await _whileBarred();

            return result;
        }
    }
}
