using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderRemark;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Backoffice;
using AeroTech.Ordering.Application.OrderAggregate.Commands.SplitOrder;
using AeroTech.Ordering.Application.OrderAggregate.Commands.UpdateLastTicketingDate;
using AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrdersPaginated;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Backoffice")]
    [Route($"Backoffice/v{{version:apiVersion}}/Orders")]
    public sealed class BackofficeController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IIdentityService _identity;

        public BackofficeController(IMediator mediator, IIdentityService identity)
        {
            _mediator = mediator;
            _identity = identity;
        }

        [HttpPost("FlightOffers")]
        public async Task<IActionResult> CreateFromOffer([FromBody] BackofficeCreateOrderFromOfferRequest request, CancellationToken cancellationToken)
        {
            var command = new BackofficeCreateOrderFromOfferCommand(
                request.CustomerId,
                _identity.RequiredCurrentUserId,
                request.AirlineOfficeId,
                request.OfferId,
                request.CommissionRate,
                request.Travellers,
                request.Contact,
                request.SeatSelections);

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("Paginated")]
        public async Task<IActionResult> Paginated([FromQuery] GetOrdersPaginatedQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{id:long}/LastTicketingDate")]
        public async Task<IActionResult> UpdateLastTicketingDate(long id, [FromBody] UpdateLastTicketingDateRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UpdateLastTicketingDateCommand(id, request.LastTicketingDate), cancellationToken);
            return NoContent();
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
