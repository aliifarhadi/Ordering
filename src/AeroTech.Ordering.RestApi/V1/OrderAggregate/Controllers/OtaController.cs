using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;
using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
using AeroTech.Ordering.RestApi._Shared;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("OTA Api")]
    [Route($"Api/v{{version:apiVersion}}/Bookings")]
    public sealed class OtaController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IIdentityService _identity;
        private readonly IOrderChangeService _orderChangeService;
        private readonly IOrderScopeCancellationService _scopeCancellationService;
        private readonly IOrderCustomerAccessGuard _accessGuard;

        public OtaController(
            IMediator mediator,
            IIdentityService identity,
            IOrderChangeService orderChangeService,
            IOrderScopeCancellationService scopeCancellationService,
            IOrderCustomerAccessGuard accessGuard)
        {
            _mediator = mediator;
            _identity = identity;
            _orderChangeService = orderChangeService;
            _scopeCancellationService = scopeCancellationService;
            _accessGuard = accessGuard;
        }

        [HttpPost("FlightOffers")]
        public async Task<IActionResult> CreateFromOffer([FromBody] OtaCreateOrderFromOfferRequest request, CancellationToken cancellationToken)
        {
            var command = new OtaCreateOrderFromOfferCommand(
                _accessGuard.RequireCustomerId(),
                _identity.RequiredCurrentUserId,
                request.OfferId,
                request.Contact,
                request.Travellers);

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{orderId:long}")]
        public async Task<IActionResult> Get([FromRoute] long orderId, CancellationToken cancellationToken)
        {
            await _accessGuard.EnsureOwnedAsync(orderId, cancellationToken);

            var order = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return order is null ? NotFound() : Ok(order);
        }

        [HttpPost("{orderId:long}/Change")]
        public async Task<IActionResult> Change(
            [FromRoute] long orderId,
            [FromBody] OrderChangeRequest request,
            CancellationToken cancellationToken)
        {
            await _accessGuard.EnsureOwnedAsync(orderId, cancellationToken);

            var idempotencyKey = IdempotencyKey.Require(Request);

            var (operationId, commercialVersion) = OrderChangeRequestMapper.ResolveVariant(request) switch
            {
                OrderChangeVariant.CancelOrderItem => await CancelOrderItemAsync(
                    orderId, request, idempotencyKey, cancellationToken),
                OrderChangeVariant.RemoveOrderServices => await RemoveOrderServicesAsync(
                    orderId, request, idempotencyKey, cancellationToken),
                _ => await AddServiceAsync(orderId, request, idempotencyKey, cancellationToken)
            };

            var order = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return Ok(new OrderChangeResponse(operationId, commercialVersion, order));
        }

        private async Task<(long OperationId, int CommercialVersion)> AddServiceAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _orderChangeService.AddServiceAsync(
                orderId,
                OrderChangeRequestMapper.ToSelections(request),
                idempotencyKey,
                request.ExpectedCommercialVersion,
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }

        private async Task<(long OperationId, int CommercialVersion)> CancelOrderItemAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _scopeCancellationService.CancelItemAsync(
                orderId,
                request.CancelOrderItem!.OrderItemId,
                request.CancelOrderItem.QuotedCancellationId,
                idempotencyKey,
                request.ExpectedCommercialVersion,
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }

        private async Task<(long OperationId, int CommercialVersion)> RemoveOrderServicesAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _scopeCancellationService.RemoveServicesAsync(
                orderId,
                request.RemoveOrderServices!.OrderServiceIds,
                request.RemoveOrderServices.QuotedCancellationId,
                idempotencyKey,
                request.ExpectedCommercialVersion,
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }
    }
}
