using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        private readonly List<OrderRemark> _remarks = new();

        public IReadOnlyCollection<OrderRemark> Remarks => _remarks.AsReadOnly();

        public long AddRemark(AddOrderRemarkArgs args, IIdGenerator idGenerator, IClock clock)
        {
            EnsureRemarksCanBeModified();
            EnsureRemarkScopeReferencesBelongToOrder(args);

            var remark = OrderRemark.Create(idGenerator.NewId(), Id, args, clock.GetDateTime());
            _remarks.Add(remark);
            IncrementVersion();

            Causes(new OrderRemarkAdded(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                remark.Id,
                remark.Type,
                remark.Scope));

            return remark.Id;
        }

        public void ModifyRemark(long remarkId, string newText, long modifiedBy, IIdGenerator idGenerator, IClock clock)
        {
            EnsureRemarksCanBeModified();

            var remark = GetRemark(remarkId);
            remark.Modify(newText, modifiedBy, clock.GetDateTime());
            IncrementVersion();

            Causes(new OrderRemarkModified(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                remark.Id));
        }

        public void DeleteRemark(long remarkId, long deletedBy, IIdGenerator idGenerator, IClock clock)
        {
            EnsureRemarksCanBeModified();

            var remark = GetRemark(remarkId);
            remark.Delete(deletedBy, clock.GetDateTime());
            IncrementVersion();

            Causes(new OrderRemarkDeleted(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                remark.Id));
        }

        private OrderRemark GetRemark(long remarkId)
            => _remarks.SingleOrDefault(remark => remark.Id == remarkId)
               ?? throw ExceptionFactory.RemarkNotFound();

        private void EnsureRemarksCanBeModified()
        {
            if (Status is OrderStatus.Expired or OrderStatus.Cancelled or OrderStatus.Refunded)
                throw ExceptionFactory.RemarksCannotBeChangedOnClosedOrder();
        }

        private void EnsureRemarkScopeReferencesBelongToOrder(AddOrderRemarkArgs args)
        {
            if (args.TravellerId is not null && _travellers.All(traveller => traveller.Id != args.TravellerId))
                throw ExceptionFactory.RemarkTravellerDoesNotBelongToOrder();

            if (args.SegmentId is not null && _segments.All(segment => segment.Id != args.SegmentId))
                throw ExceptionFactory.RemarkSegmentDoesNotBelongToOrder();

            if (args.OrderItemId is not null && _items.All(item => item.Id != args.OrderItemId))
                throw ExceptionFactory.RemarkOrderItemDoesNotBelongToOrder();

            if (args.OrderServiceId is not null && _orderServices.All(service => service.Id != args.OrderServiceId))
                throw ExceptionFactory.RemarkOrderServiceDoesNotBelongToOrder();
        }
    }
}
