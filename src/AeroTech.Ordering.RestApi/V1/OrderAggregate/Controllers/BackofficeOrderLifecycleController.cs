using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
using AeroTech.Ordering.RestApi._Shared;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
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
        private readonly IOrderChangeService _orderChangeService;
        private readonly IOrderScopeCancellationService _scopeCancellationService;
        private readonly IRefundService _refundService;

        public BackofficeOrderLifecycleController(
            IMediator mediator,
            IReserveOrderService reserveOrderService,
            IIssueOrderService issueOrderService,
            IWithdrawOrderService withdrawOrderService,
            IOrderChangeService orderChangeService,
            IOrderScopeCancellationService scopeCancellationService,
            IRefundService refundService)
        {
            _mediator = mediator;
            _reserveOrderService = reserveOrderService;
            _issueOrderService = issueOrderService;
            _withdrawOrderService = withdrawOrderService;
            _orderChangeService = orderChangeService;
            _scopeCancellationService = scopeCancellationService;
            _refundService = refundService;
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

        [HttpPost("{orderId:long}/Change")]
        public async Task<IActionResult> Change(
            [FromRoute] long orderId,
            [FromBody] OrderChangeRequest request,
            CancellationToken cancellationToken)
        {
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

        [HttpGet("{orderId:long}/Documents/{documentId:long}/RefundQuote")]
        public async Task<IActionResult> RefundQuote(
            [FromRoute] long orderId,
            [FromRoute] long documentId,
            [FromQuery] long[]? ticketCouponIds,
            CancellationToken cancellationToken)
            => Ok(await _refundService.QuoteAsync(orderId, documentId, ticketCouponIds, cancellationToken));

        [HttpPost("{orderId:long}/Documents/{documentId:long}/Refund")]
        public async Task<IActionResult> Refund(
            [FromRoute] long orderId,
            [FromRoute] long documentId,
            [FromBody] RefundDocumentRequest request,
            CancellationToken cancellationToken)
            => Ok(await _refundService.RefundAsync(
                RefundDocumentRequestMapper.ToExecution(
                    orderId,
                    documentId,
                    request,
                    IdempotencyKey.Require(Request)),
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

    public sealed record RefundDocumentRequest(
        IReadOnlyList<long> TicketCouponIds,
        int? ExpectedCommercialVersion,
        string? QuotedRefundId = null,
        ManualRefundRequest? Manual = null);

    public sealed record ManualRefundRequest(
        string AuthorityReference,
        string Reason,
        decimal ApprovedRefundAmount,
        string ApprovedDisposition,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
        string? DispositionReference = null,
        string? SourcePricingReference = null,
        string? SourceRefundType = null,
        string? SourceEvidence = null);


}
