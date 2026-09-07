using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Consumers.Jobs
{
    public sealed class MessageRetentionPoller : BackgroundService
    {
        private const string PruneProcessedOutbox =
            "DELETE TOP (@batch) FROM [dbo].[OutboxMessages] WHERE [ProcessedOn] IS NOT NULL AND [ProcessedOn] < @cutoff";

        private const string PruneInbox =
            "DELETE TOP (@batch) FROM [dbo].[InboxMessages] WHERE [ReceivedOn] < @cutoff";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MessageRetentionOptions _options;
        private readonly ILogger<MessageRetentionPoller> _logger;

        public MessageRetentionPoller(
            IServiceScopeFactory scopeFactory,
            IOptions<MessageRetentionOptions> options,
            ILogger<MessageRetentionPoller> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.PollIntervalMinutes));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PruneAsync(stoppingToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Message retention pruning loop failed.");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task PruneAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().GetDateTime();

            var outboxRemoved = await DeleteBatchedAsync(
                dbContext,
                PruneProcessedOutbox,
                now.AddDays(-Math.Max(1, _options.OutboxRetentionDays)),
                cancellationToken);

            var inboxRemoved = await DeleteBatchedAsync(
                dbContext,
                PruneInbox,
                now.AddDays(-Math.Max(1, _options.InboxRetentionDays)),
                cancellationToken);

            if (outboxRemoved > 0 || inboxRemoved > 0)
                _logger.LogInformation(
                    "Message retention pruned {OutboxRemoved} processed outbox row(s) and {InboxRemoved} inbox row(s).",
                    outboxRemoved,
                    inboxRemoved);
        }

        private async Task<int> DeleteBatchedAsync(
            OrderingDbContext dbContext,
            string sql,
            DateTimeOffset cutoff,
            CancellationToken cancellationToken)
        {
            var batchSize = Math.Max(1, _options.DeleteBatchSize);
            var total = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var removed = await dbContext.Database.ExecuteSqlRawAsync(
                    sql,
                    new object[]
                    {
                        new SqlParameter("@batch", batchSize),
                        new SqlParameter("@cutoff", cutoff)
                    },
                    cancellationToken);

                total += removed;

                if (removed < batchSize)
                    break;
            }

            return total;
        }
    }
}
