using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public interface IFulfillmentProviderAdapter
    {
        OrderProviderType ProviderType { get; }

        OrderFulfillmentTaskType TaskType { get; }

        ProviderInteractionType InteractionType { get; }

        FulfillmentMode Mode => FulfillmentMode.Sync;

        Task<FulfillmentResult> ExecuteAsync(FulfillmentExecutionContext context, CancellationToken cancellationToken = default);
    }
}
