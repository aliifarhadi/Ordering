using AeroTech.Framework.Core.ServiceContracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AeroTech.Ordering.Consumers.Inbox
{
    public sealed class InboxConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
    {
        private readonly IInboxStore _inboxStore;
        private readonly ILogger<InboxConsumeFilter<T>> _logger;

        public InboxConsumeFilter(IInboxStore inboxStore, ILogger<InboxConsumeFilter<T>> logger)
        {
            _inboxStore = inboxStore;
            _logger = logger;
        }

        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            if (context.MessageId is not { } messageId)
            {
                await next.Send(context);
                return;
            }

            var consumer = context.ReceiveContext.InputAddress?.AbsolutePath ?? typeof(T).Name;
            var messageType = typeof(T).FullName ?? typeof(T).Name;

            if (await _inboxStore.HasProcessedAsync(messageId, consumer, context.CancellationToken))
            {
                _logger.LogInformation(
                    "Skipped duplicate message {MessageId} of type {MessageType} on {Consumer}.",
                    messageId,
                    messageType,
                    consumer);

                return;
            }

            await next.Send(context);

            await _inboxStore.MarkProcessedAsync(messageId, consumer, messageType, context.CancellationToken);
        }

        public void Probe(ProbeContext context) => context.CreateFilterScope("inbox");
    }
}
