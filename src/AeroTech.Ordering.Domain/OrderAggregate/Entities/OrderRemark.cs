using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.OrderAggregate.Constants;
using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderRemark : Entity<long>
    {
        private OrderRemark()
        {
        }

        private OrderRemark(long id, long orderId, AddOrderRemarkArgs args, DateTimeOffset now)
        {
            Id = id;
            OrderId = orderId;
            Type = args.Type;
            Visibility = args.Visibility;
            Scope = args.Scope;
            TravellerId = args.TravellerId;
            SegmentId = args.SegmentId;
            OrderItemId = args.OrderItemId;
            OrderServiceId = args.OrderServiceId;
            DocumentId = args.DocumentId;
            Text = args.Text.Trim();
            CategoryCode = args.CategoryCode;
            IsPrintedOnItinerary = args.IsPrintedOnItinerary;
            IsPrintedOnInvoice = args.IsPrintedOnInvoice;
            Status = OrderRemarkStatus.Active;
            CreatedBy = args.CreatedBy;
            CreatedAt = now;
        }

        public long OrderId { get; private set; }

        public OrderRemarkType Type { get; private set; }

        public OrderRemarkVisibility Visibility { get; private set; }

        public OrderRemarkScope Scope { get; private set; }

        public long? TravellerId { get; private set; }

        public long? SegmentId { get; private set; }

        public long? OrderItemId { get; private set; }

        public long? OrderServiceId { get; private set; }

        public long? DocumentId { get; private set; }

        public string Text { get; private set; } = default!;

        public string? CategoryCode { get; private set; }

        public bool IsPrintedOnItinerary { get; private set; }

        public bool IsPrintedOnInvoice { get; private set; }

        public OrderRemarkStatus Status { get; private set; }

        public long CreatedBy { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public long? ModifiedBy { get; private set; }

        public DateTimeOffset? ModifiedAt { get; private set; }

        public long? DeletedBy { get; private set; }

        public DateTimeOffset? DeletedAt { get; private set; }

        public static OrderRemark Create(long id, long orderId, AddOrderRemarkArgs args, DateTimeOffset now)
        {
            Validate(args);
            return new OrderRemark(id, orderId, args, now);
        }

        public void Modify(string newText, long modifiedBy, DateTimeOffset now)
        {
            EnsureActive();
            EnsureTextIsValid(newText);

            Text = newText.Trim();
            ModifiedBy = modifiedBy;
            ModifiedAt = now;
        }

        public void Delete(long deletedBy, DateTimeOffset now)
        {
            if (Status == OrderRemarkStatus.Deleted)
                return;

            Status = OrderRemarkStatus.Deleted;
            DeletedBy = deletedBy;
            DeletedAt = now;
        }

        private void EnsureActive()
        {
            if (Status != OrderRemarkStatus.Active)
                throw ExceptionFactory.OnlyActiveRemarkCanBeModified();
        }

        private static void Validate(AddOrderRemarkArgs args)
        {
            EnsureTextIsValid(args.Text);

            if (args.Scope == OrderRemarkScope.Traveller && args.TravellerId is null)
                throw ExceptionFactory.TravellerScopedRemarkRequiresTraveller();

            if (args.Scope == OrderRemarkScope.Segment && args.SegmentId is null)
                throw ExceptionFactory.SegmentScopedRemarkRequiresSegment();

            if (args.Scope == OrderRemarkScope.OrderItem && args.OrderItemId is null)
                throw ExceptionFactory.OrderItemScopedRemarkRequiresOrderItem();

            if (args.Scope == OrderRemarkScope.OrderService && args.OrderServiceId is null)
                throw ExceptionFactory.OrderServiceScopedRemarkRequiresOrderService();

            if (args.Scope == OrderRemarkScope.Document && args.DocumentId is null)
                throw ExceptionFactory.DocumentScopedRemarkRequiresDocument();
        }

        private static void EnsureTextIsValid(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw ExceptionFactory.RemarkTextIsRequired();

            if (text.Length > OrderRemarkRules.MaxTextLength)
                throw ExceptionFactory.RemarkTextTooLong(OrderRemarkRules.MaxTextLength);
        }
    }
}
