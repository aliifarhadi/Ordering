using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Expiry;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Consumers.Jobs
{
    public sealed class OrderExpiryPoller : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ExpiryOptions _options;
        private readonly ILogger<OrderExpiryPoller> _logger;

        public OrderExpiryPoller(
            IServiceScopeFactory scopeFactory,
            IOptions<ExpiryOptions> options,
            ILogger<OrderExpiryPoller> logger)
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
                    _logger.LogError(exception, "Order expiry polling loop failed.");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<long> dueOrderIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                dueOrderIds = await repository.GetExpiredOrderIdsAsync(clock.GetDateTime(), _options.PollBatchSize, cancellationToken);
            }

            foreach (var orderId in dueOrderIds)
            {
                try
                {
                    using var orderScope = _scopeFactory.CreateScope();
                    var distributedLock = orderScope.ServiceProvider.GetRequiredService<IDistributedLock>();

                    await using var lockHandle = await distributedLock.AcquireAsync(
                        $"order-expiry:{orderId}",
                        TimeSpan.FromSeconds(_options.LockExpirySeconds),
                        cancellationToken);

                    if (lockHandle is null)
                        continue;

                    var expiryService = orderScope.ServiceProvider.GetRequiredService<IOrderExpiryService>();
                    await expiryService.ExpireAsync(orderId, cancellationToken);
                }
                catch (BusinessException exception)
                {
                    _logger.LogInformation(exception, "Order {OrderId} was no longer expirable and was skipped.", orderId);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Expiring order {OrderId} failed.", orderId);
                }
            }
        }
    }
}
