using AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderRemark;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer;
using AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.PayOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.SplitOrder;
using AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderById;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Internal")]
    [Route($"Internal/v{{version:apiVersion}}/Orders")]
    public sealed class InternalController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InternalController(IMediator mediator) => _mediator = mediator;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderCommand command, CancellationToken cancellationToken)
        {
            var orderId = await _mediator.Send(command, cancellationToken);
            return Ok(new { orderId });
        }

        [HttpPost("FlightOffers")]
        public async Task<IActionResult> CreateFromOffer([FromBody] CreateOrderFromOfferCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        {
            var order = await _mediator.Send(new GetOrderByIdQuery(id), cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }

        [HttpPost("{id:long}/Reservations")]
        public async Task<IActionResult> Reserve(long id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ReserveOrderCommand(id), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{id:long}/Payments")]
        public async Task<IActionResult> Pay(long id, [FromBody] PayOrderRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PayOrderCommand(id, request.FormOfPayment, request.WalletReference), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{id:long}/Issuance")]
        public async Task<IActionResult> Issue(long id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new IssueOrderCommand(id), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{id:long}/Cancel")]
        public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CancelOrderCommand(id), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{id:long}/Split")]
        public async Task<IActionResult> Split(long id, [FromBody] SplitOrderRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new SplitOrderCommand(id, request.TravellerIds), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{id:long}/Documents/{documentId:long}/Void")]
        public async Task<IActionResult> VoidDocument(long id, long documentId, [FromBody] VoidTrafficDocumentRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new VoidTrafficDocumentCommand(id, documentId, request.Reason, request.ReasonDetail),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("{id:long}/Remarks")]
        public async Task<IActionResult> AddRemark(long id, [FromBody] AddOrderRemarkRequest request, CancellationToken cancellationToken)
        {
            var remarkId = await _mediator.Send(
                new AddOrderRemarkCommand(
                    id,
                    request.Type,
                    request.Visibility,
                    request.Scope,
                    request.Text,
                    request.TravellerId,
                    request.SegmentId,
                    request.OrderItemId,
                    request.OrderServiceId,
                    request.DocumentId,
                    request.CategoryCode,
                    request.IsPrintedOnItinerary,
                    request.IsPrintedOnInvoice),
                cancellationToken);

            return Ok(new { remarkId });
        }
    }
}
