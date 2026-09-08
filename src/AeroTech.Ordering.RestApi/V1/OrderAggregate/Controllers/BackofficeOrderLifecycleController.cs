using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.ProductAddition;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
using AeroTech.Ordering.RestApi._Shared;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Backoffice")]
    [Route($"Backoffice/v{{version:apiVersion}}/Orders")]
    public sealed class BackofficeOrderLifecycleController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IReserveOrderService _reserveOrderService;
        private readonly IIssueOrderService _issueOrderService;
        private readonly IWithdrawOrderService _withdrawOrderService;
        private readonly IAddProductService _addProductService;

        public BackofficeOrderLifecycleController(
            IMediator mediator,
            IReserveOrderService reserveOrderService,
            IIssueOrderService issueOrderService,
            IWithdrawOrderService withdrawOrderService,
            IAddProductService addProductService)
        {
            _mediator = mediator;
            _reserveOrderService = reserveOrderService;
            _issueOrderService = issueOrderService;
            _withdrawOrderService = withdrawOrderService;
            _addProductService = addProductService;
        }

        [HttpGet("{orderId:long}/Details")]
        public async Task<IActionResult> GetDetails([FromRoute] long orderId, CancellationToken cancellationToken)
        {
            var details = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return details is null ? NotFound() : Ok(details);
        }

        [HttpPost("{orderId:long}/Reserve")]
        public async Task<IActionResult> Reserve(
            [FromRoute] long orderId,
            [FromBody] ReserveOrderRequest request,
            CancellationToken cancellationToken)
            => Ok(await _reserveOrderService.ReserveAsync(
                orderId,
                IdempotencyKey.Require(Request),
                request.ExpectedCommercialVersion,
                cancellationToken));

        [HttpPost("{orderId:long}/Issue")]
        public async Task<IActionResult> Issue(
            [FromRoute] long orderId,
            [FromBody] IssueOrderRequest request,
            CancellationToken cancellationToken)
            => Ok(await _issueOrderService.IssueAsync(
                orderId,
                IdempotencyKey.Require(Request),
                request.ExpectedCommercialVersion,
                cancellationToken));

        [HttpPost("{orderId:long}/AddProduct")]
        public async Task<IActionResult> AddProduct(
            [FromRoute] long orderId,
            [FromBody] AddProductRequest request,
            CancellationToken cancellationToken)
            => Ok(await _addProductService.AddProductAsync(
                orderId,
                request.SourceReference,
                IdempotencyKey.Require(Request),
                request.ExpectedCommercialVersion,
                cancellationToken));

        [HttpPost("{orderId:long}/Withdraw")]
        public async Task<IActionResult> Withdraw(
            [FromRoute] long orderId,
            [FromBody] WithdrawOrderRequest request,
            CancellationToken cancellationToken)
            => Ok(await _withdrawOrderService.WithdrawAsync(
                orderId,
                request.Reason,
                IdempotencyKey.Require(Request),
                request.ExpectedCommercialVersion,
                cancellationToken));

    }

    public sealed record ReserveOrderRequest(int? ExpectedCommercialVersion);

    public sealed record IssueOrderRequest(int? ExpectedCommercialVersion);

    public sealed record WithdrawOrderRequest(VoidReason Reason, int? ExpectedCommercialVersion);

    public sealed record AddProductRequest(string SourceReference, int? ExpectedCommercialVersion);
}
