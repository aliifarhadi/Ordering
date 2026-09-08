using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("OTA Panel")]
    [Route($"OtaPanel/v{{version:apiVersion}}/Bookings")]
    public sealed class OtaPanelController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IIdentityService _identity;
        private readonly IOrderCustomerAccessGuard _accessGuard;

        public OtaPanelController(IMediator mediator, IIdentityService identity, IOrderCustomerAccessGuard accessGuard)
        {
            _mediator = mediator;
            _identity = identity;
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
    }
}
