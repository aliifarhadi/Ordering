using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;
using AeroTech.Ordering.Application.OrderAggregate.Services.ProductAddition;
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
        private readonly IAddProductService _addProductService;

        public OtaController(IMediator mediator, IIdentityService identity, IAddProductService addProductService)
        {
            _mediator = mediator;
            _identity = identity;
            _addProductService = addProductService;
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

        [HttpPost("{orderId:long}/AddProduct")]
        public async Task<IActionResult> AddProduct(
            [FromRoute] long orderId,
            [FromBody] OtaAddProductRequest request,
            CancellationToken cancellationToken)
            => Ok(await _addProductService.AddProductAsync(
                orderId,
                request.SourceReference,
                IdempotencyKey.Require(Request),
                request.ExpectedCommercialVersion,
                cancellationToken));
    }

    public sealed record OtaAddProductRequest(string SourceReference, int? ExpectedCommercialVersion);
}
