using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void ApplyDocumentVoid(IReadOnlyCollection<long> orderServiceIds, IClock clock)
        {
            foreach (var service in _orderServices.Where(service => orderServiceIds.Contains(service.Id)))
                service.MarkDocumentVoided();

            RecomputeCommercialSummary();

            _ = clock;
        }
    }
}
