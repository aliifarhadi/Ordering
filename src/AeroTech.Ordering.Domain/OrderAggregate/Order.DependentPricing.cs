using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public OrderPriceChangeSet CommitDependentPriceChange(
            AcceptedDependentPriceChangeArgs consequence,
            IIdGenerator idGenerator,
            IClock clock)
            => CommitDependentPriceChange(consequence, idGenerator, clock.GetDateTime());

        public OrderPriceChangeSet CommitDependentPriceChange(
            AcceptedDependentPriceChangeArgs consequence,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(consequence);

            var host = RequireDependentPriceChangeHost(consequence.OperationId, consequence.HostChangeType);

            var staged = StagePriceChange(
                host,
                new AcceptedPriceChangeArgs(
                    host.ChangeType,
                    consequence.Reason,
                    consequence.Source,
                    consequence.Lines,
                    consequence.SourceOfferId,
                    consequence.SourcePricingRef),
                idGenerator,
                now);

            var changeSet = AttachPriceConsequence(staged, now);

            IncrementCommercialVersion();
            RaisePricingChanged(host, changeSet, idGenerator, now);

            return changeSet;
        }

        public IReadOnlyList<OrderPriceChangeSet> PriceConsequencesOf(long orderChangeId)
            => _priceChangeSets
                .Where(set => set.ChangeId == orderChangeId)
                .OrderBy(set => set.FinancialSequence)
                .ToList();

        public OrderPriceChangeSet? OriginatingPriceConsequenceOf(long orderChangeId)
            => PriceConsequencesOf(orderChangeId).FirstOrDefault();

        private OrderChange RequireDependentPriceChangeHost(long operationId, OrderChangeType hostChangeType)
        {
            var hosts = _changes
                .Where(change => change.OrderId == Id && change.OperationId == operationId)
                .ToList();

            if (hosts.Count != 1 || hosts[0].ChangeType != hostChangeType)
                throw ExceptionFactory.DependentPriceChangeHostNotResolvable(operationId, hostChangeType, Id);

            return hosts[0];
        }
    }
}
