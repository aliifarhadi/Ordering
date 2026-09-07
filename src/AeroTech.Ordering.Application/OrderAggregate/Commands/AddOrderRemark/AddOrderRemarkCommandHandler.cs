using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderRemark
{
    public sealed class AddOrderRemarkCommandHandler : IRequestHandler<AddOrderRemarkCommand, long>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IIdentityService _identityService;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;

        public AddOrderRemarkCommandHandler(
            IOrderRepository orderRepository,
            IIdentityService identityService,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork)
        {
            _orderRepository = orderRepository;
            _identityService = identityService;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
        }

        public async Task<long> Handle(AddOrderRemarkCommand command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetAsync(command.OrderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(command.OrderId);

            var args = new AddOrderRemarkArgs(
                command.Type,
                command.Visibility,
                command.Scope,
                command.Text,
                _identityService.RequiredCurrentUserId,
                command.TravellerId,
                command.SegmentId,
                command.OrderItemId,
                command.OrderServiceId,
                command.DocumentId,
                command.CategoryCode,
                command.IsPrintedOnItinerary,
                command.IsPrintedOnInvoice);

            var remarkId = order.AddRemark(args, _idGenerator, _clock);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return remarkId;
        }
    }
}
