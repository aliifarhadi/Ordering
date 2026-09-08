using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderPriceChangeSet : Entity<long>
    {
        private OrderPriceChangeSet()
        {
        }

        public OrderPriceChangeSet(CreateOrderPriceChangeSetArgs args)
        {
            Id = args.Id;
            OrderId = args.OrderId;
            ChangeId = args.ChangeId;
            FinancialSequence = args.FinancialSequence;
            ExpectedCommercialVersion = args.ExpectedCommercialVersion;
            Reason = args.Reason;
            Source = args.Source;
            SourceOfferId = args.SourceOfferId;
            SourcePricingRef = args.SourcePricingRef;
            CreatedAt = args.CreatedAt;
        }

        public long OrderId { get; private set; }

        public long ChangeId { get; private set; }

        public long FinancialSequence { get; private set; }

        public int ExpectedCommercialVersion { get; private set; }

        public PriceChangeReason Reason { get; private set; }

        public PricingSource Source { get; private set; }

        public string? SourceOfferId { get; private set; }

        public string? SourcePricingRef { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset? CommittedAt { get; private set; }

        public bool IsCommitted => CommittedAt.HasValue;

        internal void Commit(DateTimeOffset committedAt)
        {
            if (IsCommitted)
                throw ExceptionFactory.PriceChangeSetAlreadyCommitted();

            CommittedAt = committedAt;
        }
    }
}
