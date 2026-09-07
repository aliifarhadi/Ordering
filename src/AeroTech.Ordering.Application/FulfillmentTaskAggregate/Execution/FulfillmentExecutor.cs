using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.ProviderInteractionAggregate;
using AeroTech.Ordering.Domain.ProviderInteractionAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public sealed class FulfillmentExecutor : IFulfillmentExecutor
    {
        private readonly IProviderInteractionRepository _providerInteractionRepository;
        private readonly IFulfillmentProviderResolver _resolver;
        private readonly IIdGenerator _idGenerator;

        public FulfillmentExecutor(
            IProviderInteractionRepository providerInteractionRepository,
            IFulfillmentProviderResolver resolver,
            IIdGenerator idGenerator)
        {
            _providerInteractionRepository = providerInteractionRepository;
            _resolver = resolver;
            _idGenerator = idGenerator;
        }

        public async Task<FulfillmentResult> ExecuteAttemptAsync(
            FulfillmentTask task,
            Order order,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var adapter = _resolver.Resolve(task.ProviderType, task.TaskType);

            var attemptId = _idGenerator.NewId();
            var interactionId = _idGenerator.NewId();
            var correlationId = Guid.NewGuid().ToString();
            var isRetriable = task.AttemptCount + 1 < task.MaxAttempts;

            task.StartAttempt(attemptId, interactionId, isRetriable, now);

            FulfillmentResult result;
            try
            {
                result = await adapter.ExecuteAsync(new FulfillmentExecutionContext(task, order), cancellationToken);
            }
            catch (Exception exception)
            {
                result = FulfillmentResult.Failed(
                    exception.Message,
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    null,
                    null,
                    null);
            }

            var interaction = ProviderInteraction.Create(
                interactionId,
                task.Id,
                attemptId,
                task.ProviderType,
                task.SupplierCode,
                adapter.InteractionType,
                result.ProviderIdempotencyKey ?? task.IdempotencyKey,
                correlationId,
                result.RawRequest ?? string.Empty);

            if (result.Success)
            {
                interaction.MarkSucceeded(result.RawResponse);
                task.MarkSucceeded(_idGenerator.NewId(), now, result.ExpiresAt);

                foreach (var target in result.Targets)
                    task.MarkTargetConfirmed(target.OrderServiceId, result.Reference, target.ServiceReference);
            }
            else
            {
                interaction.MarkFailed(result.RawResponse);
            }

            await _providerInteractionRepository.AddAsync(interaction, cancellationToken);
            return result;
        }
    }
}
