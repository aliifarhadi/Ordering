using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Consumers.Jobs
{
    public sealed class FulfillmentTaskPoller : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FulfillmentOptions _options;
        private readonly ILogger<FulfillmentTaskPoller> _logger;

        public FulfillmentTaskPoller(
            IServiceScopeFactory scopeFactory,
            IOptions<FulfillmentOptions> options,
            ILogger<FulfillmentTaskPoller> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollAsync(stoppingToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Fulfillment task polling loop failed.");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<long> dueTaskIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IFulfillmentTaskRepository>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                dueTaskIds = await repository.GetDueTaskIdsAsync(clock.GetDateTime(), _options.PollBatchSize, cancellationToken);
            }

            foreach (var taskId in dueTaskIds)
            {
                try
                {
                    using var taskScope = _scopeFactory.CreateScope();
                    var runner = taskScope.ServiceProvider.GetRequiredService<IFulfillmentTaskRunner>();
                    await runner.RunAsync(taskId, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Running fulfillment task {TaskId} failed.", taskId);
                }
            }
        }
    }
}
