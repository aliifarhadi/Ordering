using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptExchange;
using AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptQuotedChange;
using AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderService;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrderItem;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CancelRefund;
using AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder.Backoffice;
using AeroTech.Ordering.Application.OrderAggregate.Commands.RemoveOrderServices;
using AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder.Backoffice;
using AeroTech.Ordering.Application.OrderAggregate.Commands.WithdrawOrder;
using AeroTech.Ordering.Query.OrderAggregate.Queries.QuoteChange;
using AeroTech.Ordering.Query.OrderAggregate.Queries.QuoteExchange;
using AeroTech.Ordering.Query.OrderAggregate.Queries.QuoteRefund;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
using AeroTech.Ordering.RestApi._Shared;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Responses;
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

        public BackofficeOrderLifecycleController(IMediator mediator) => _mediator = mediator;

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
            => Ok(await _mediator.Send(
                new BackofficeReserveOrderCommand(orderId, IdempotencyKey.Require(Request), request.ExpectedCommercialVersion),
                cancellationToken));

        [HttpPost("{orderId:long}/Issue")]
        public async Task<IActionResult> Issue(
            [FromRoute] long orderId,
            [FromBody] IssueOrderRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new BackofficeIssueOrderCommand(orderId, IdempotencyKey.Require(Request), request.ExpectedCommercialVersion),
                cancellationToken));

        [HttpPost("{orderId:long}/Change")]
        public async Task<IActionResult> Change(
            [FromRoute] long orderId,
            [FromBody] OrderChangeRequest request,
            CancellationToken cancellationToken)
        {
            var idempotencyKey = IdempotencyKey.Require(Request);
            var variant = OrderChangeRequestMapper.ResolveVariant(request);

            if (variant == OrderChangeVariant.AcceptExchange)
                return Ok(await AcceptExchangeAsync(orderId, request, idempotencyKey, cancellationToken));

            var (operationId, commercialVersion) = variant switch
            {
                OrderChangeVariant.CancelOrderItem => await CancelOrderItemAsync(orderId, request, idempotencyKey, cancellationToken),
                OrderChangeVariant.RemoveOrderServices => await RemoveOrderServicesAsync(orderId, request, idempotencyKey, cancellationToken),
                OrderChangeVariant.AcceptQuotedChange => await AcceptQuotedChangeAsync(orderId, request, idempotencyKey, cancellationToken),
                _ => await AddServiceAsync(orderId, request, idempotencyKey, cancellationToken)
            };

            var order = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return Ok(new OrderChangeResponse(operationId, commercialVersion, order));
        }

        [HttpPost("{orderId:long}/Change/Quote")]
        public async Task<IActionResult> ChangeQuote(
            [FromRoute] long orderId,
            [FromBody] ChangeQuoteRequestBody request,
            CancellationToken cancellationToken)
            => ChangeQuoteRequestMapper.ResolveVariant(request) switch
            {
                ChangeQuoteVariant.Exchange => Ok(await _mediator.Send(
                    new QuoteExchangeQuery(orderId, request.QuoteExchange!.ChangedOrderServiceIds), cancellationToken)),
                _ => Ok(await _mediator.Send(
                    new QuoteChangeQuery(orderId, request.OrderServiceId!.Value), cancellationToken))
            };

        [HttpGet("{orderId:long}/Documents/{documentId:long}/RefundQuote")]
        public async Task<IActionResult> RefundQuote(
            [FromRoute] long orderId,
            [FromRoute] long documentId,
            [FromQuery] long[]? ticketCouponIds,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new QuoteRefundQuery(orderId, documentId, ticketCouponIds), cancellationToken));

        [HttpPost("{orderId:long}/Documents/{documentId:long}/Refund")]
        public async Task<IActionResult> Refund(
            [FromRoute] long orderId,
            [FromRoute] long documentId,
            [FromBody] RefundDocumentRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                RefundDocumentRequestMapper.ToCommand(orderId, documentId, request, IdempotencyKey.Require(Request)),
                cancellationToken));

        [HttpPost("{orderId:long}/Documents/{documentId:long}/Refunds/{refundRecordId:long}/Cancel")]
        public async Task<IActionResult> CancelRefund(
            [FromRoute] long orderId,
            [FromRoute] long documentId,
            [FromRoute] long refundRecordId,
            [FromBody] CancelRefundRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new CancelRefundCommand(
                    orderId,
                    documentId,
                    refundRecordId,
                    request.Reason,
                    IdempotencyKey.Require(Request),
                    request.ExpectedCommercialVersion,
                    request.ReasonDetail),
                cancellationToken));

        [HttpPost("{orderId:long}/Withdraw")]
        public async Task<IActionResult> Withdraw(
            [FromRoute] long orderId,
            [FromBody] WithdrawOrderRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new WithdrawOrderCommand(orderId, request.Reason, IdempotencyKey.Require(Request), request.ExpectedCommercialVersion),
                cancellationToken));

        private async Task<(long OperationId, int CommercialVersion)> AddServiceAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(
                new AddOrderServiceCommand(
                    orderId,
                    OrderChangeRequestMapper.ToSelections(request),
                    idempotencyKey,
                    request.ExpectedCommercialVersion),
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }

        private async Task<(long OperationId, int CommercialVersion)> CancelOrderItemAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(
                new CancelOrderItemCommand(
                    orderId,
                    request.CancelOrderItem!.OrderItemId,
                    request.CancelOrderItem.QuotedCancellationId,
                    idempotencyKey,
                    request.ExpectedCommercialVersion),
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }

        private async Task<(long OperationId, int CommercialVersion)> RemoveOrderServicesAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(
                new RemoveOrderServicesCommand(
                    orderId,
                    request.RemoveOrderServices!.OrderServiceIds,
                    request.RemoveOrderServices.QuotedCancellationId,
                    idempotencyKey,
                    request.ExpectedCommercialVersion),
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }

        private async Task<(long OperationId, int CommercialVersion)> AcceptQuotedChangeAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(
                new AcceptQuotedChangeCommand(
                    orderId,
                    request.AcceptQuotedChange!.OrderServiceId,
                    request.AcceptQuotedChange.QuotedChangeId,
                    idempotencyKey,
                    request.ExpectedCommercialVersion),
                cancellationToken);

            return (outcome.OperationId, outcome.CommercialVersion);
        }

        private async Task<OrderChangeResponse> AcceptExchangeAsync(
            long orderId,
            OrderChangeRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(
                new AcceptExchangeCommand(
                    orderId,
                    request.AcceptExchange!.ChangedOrderServiceIds,
                    request.AcceptExchange.QuotedExchangeId,
                    idempotencyKey,
                    request.ExpectedCommercialVersion),
                cancellationToken);

            var order = await _mediator.Send(new GetOrderDetailsQuery(orderId), cancellationToken);

            return new OrderChangeResponse(outcome.OperationId, outcome.CommercialVersion, order, outcome);
        }
    }
}
