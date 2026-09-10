using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public sealed class CancelRefundAuthorizer : ICancelRefundAuthorizer
    {
        private readonly ICancelRefundAuthorizationPort _authorization;
        private readonly ICallerContext _callerContext;

        public CancelRefundAuthorizer(
            ICancelRefundAuthorizationPort authorization,
            ICallerContext callerContext)
        {
            _authorization = authorization;
            _callerContext = callerContext;
        }

        public async Task AuthorizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancelRefundExecution execution,
            CancellationToken cancellationToken = default)
        {
            EnsureContextIsEligible(order.Id);

            var decision = await DecideAsync(order, operation, ticket, refund, execution, cancellationToken);

            if (decision.Outcome == ManualRefundAuthorizationOutcome.Approved)
                return;

            throw decision.Outcome == ManualRefundAuthorizationOutcome.Denied
                ? ExceptionFactory.CancelRefundNotAuthorized(
                    order.Id, Detail(decision, "the request was refused"))
                : ExceptionFactory.CancelRefundAuthorizationUnavailable(
                    order.Id, Detail(decision, "the authority returned no decision"));
        }

        private void EnsureContextIsEligible(long orderId)
        {
            if (!_callerContext.IsAuthenticated)
                throw ExceptionFactory.CallerContextUnavailable();

            if (_callerContext.AuthorizationSurface != AuthorizationSurface.Backoffice
                || _callerContext.ContextType != BusinessContextType.Airline
                || _callerContext.ActorId is null)
                throw ExceptionFactory.ManualRefundContextNotEligible(orderId);
        }

        private async Task<CancelRefundAuthorizationDecision> DecideAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancelRefundExecution execution,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _authorization.AuthorizeAsync(
                    new CancelRefundAuthorizationRequest(
                        order.Id,
                        ticket.Id,
                        refund.Id,
                        operation.OperationId,
                        refund.OperationId,
                        _callerContext.ActorId!.Value,
                        CallerScope.For(_callerContext),
                        _callerContext.ContextType!.Value,
                        _callerContext.AuthorizationSurface!.Value,
                        refund.ApprovedAmount,
                        refund.CurrencyId,
                        execution.Reason,
                        execution.ReasonDetail),
                    cancellationToken);
            }
            catch (BusinessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new CancelRefundAuthorizationDecision(
                    ManualRefundAuthorizationOutcome.Unavailable, null, exception.Message);
            }
        }

        private static string Detail(CancelRefundAuthorizationDecision decision, string fallback)
            => string.IsNullOrWhiteSpace(decision.Detail) ? fallback : decision.Detail;
    }
}
