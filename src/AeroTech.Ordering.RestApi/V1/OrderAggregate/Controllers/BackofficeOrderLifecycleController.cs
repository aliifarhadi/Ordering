using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
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
        private const string IdempotencyKeyHeader = "Idempotency-Key";

        private readonly IMediator _mediator;
        private readonly IReserveOrderService _reserveOrderService;
        private readonly IIssueOrderService _issueOrderService;
        private readonly IWithdrawOrderService _withdrawOrderService;

        public BackofficeOrderLifecycleController(
            IMediator mediator,
            IReserveOrderService reserveOrderService,
            IIssueOrderService issueOrderService,
            IWithdrawOrderService withdrawOrderService)
        {
            _mediator = mediator;
            _reserveOrderService = reserveOrderService;
            _issueOrderService = issueOrderService;
            _withdrawOrderService = withdrawOrderService;
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
                RequireIdempotencyKey(),
                request.ExpectedCommercialVersion,
                cancellationToken));

        [HttpPost("{orderId:long}/Issue")]
        public async Task<IActionResult> Issue(
            [FromRoute] long orderId,
            [FromBody] IssueOrderRequest request,
            CancellationToken cancellationToken)
            => Ok(await _issueOrderService.IssueAsync(
                orderId,
                RequireIdempotencyKey(),
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
                RequireIdempotencyKey(),
                request.ExpectedCommercialVersion,
                cancellationToken));

        private string RequireIdempotencyKey()
        {
            var key = Request.Headers[IdempotencyKeyHeader].ToString();

            if (string.IsNullOrWhiteSpace(key))
                throw new AeroTech.Framework.Core.Domain.Exceptions.BusinessException(
                    2731,
                    $"The '{IdempotencyKeyHeader}' header is required for this operation.")
                {
                    HttpStatus = 400
                };

            return key;
        }
    }

    public sealed record ReserveOrderRequest(int? ExpectedCommercialVersion);

    public sealed record IssueOrderRequest(int? ExpectedCommercialVersion);

    public sealed record WithdrawOrderRequest(VoidReason Reason, int? ExpectedCommercialVersion);
}
