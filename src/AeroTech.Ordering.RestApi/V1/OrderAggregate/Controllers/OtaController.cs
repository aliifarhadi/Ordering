using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;
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

        public OtaController(IMediator mediator, IIdentityService identity, IOrderChangeService orderChangeService)
        {
            _mediator = mediator;
            _identity = identity;
            _orderChangeService = orderChangeService;
        }

        [HttpPost("FlightOffers")]
        public async Task<IActionResult> CreateFromOffer([FromBody] OtaCreateOrderFromOfferRequest request, CancellationToken cancellationToken)
        {
            var command = new OtaCreateOrderFromOfferCommand(
                _identity.CurrentCustomerId ?? 0,
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
            var order = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return order is null ? NotFound() : Ok(order);
        }

        [HttpPost("{orderId:long}/Change")]
        public async Task<IActionResult> Change(
            [FromRoute] long orderId,
            [FromBody] OrderChangeRequest request,
            CancellationToken cancellationToken)
        {
            var outcome = await _orderChangeService.AddServiceAsync(
                orderId,
                OrderChangeRequestMapper.ToSelections(request),
                IdempotencyKey.Require(Request),
                request.ExpectedCommercialVersion,
                cancellationToken);

            var order = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return Ok(new OrderChangeResponse(outcome.OperationId, outcome.CommercialVersion, order));
        }
    }
}
