using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public interface IFulfillmentProviderResolver
    {
        IFulfillmentProviderAdapter Resolve(OrderProviderType providerType, OrderFulfillmentTaskType taskType);
    }

    public sealed class FulfillmentProviderResolver : IFulfillmentProviderResolver
    {
        private readonly IReadOnlyDictionary<(OrderProviderType, OrderFulfillmentTaskType), IFulfillmentProviderAdapter> _adapters;

        public FulfillmentProviderResolver(IEnumerable<IFulfillmentProviderAdapter> adapters)
            => _adapters = adapters.ToDictionary(adapter => (adapter.ProviderType, adapter.TaskType));

        public IFulfillmentProviderAdapter Resolve(OrderProviderType providerType, OrderFulfillmentTaskType taskType)
            => _adapters.TryGetValue((providerType, taskType), out var adapter)
                ? adapter
                : throw ExceptionFactory.NoFulfillmentAdapterRegistered(providerType, taskType);
    }
}
