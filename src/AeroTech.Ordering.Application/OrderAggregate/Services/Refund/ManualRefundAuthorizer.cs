using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed class ManualRefundAuthorizer : IManualRefundAuthorizer
    {
        private readonly IManualRefundAuthorizationPort _authorization;
        private readonly ICallerContext _callerContext;

        public ManualRefundAuthorizer(
            IManualRefundAuthorizationPort authorization,
            ICallerContext callerContext)
        {
            _authorization = authorization;
            _callerContext = callerContext;
        }

        public async Task AuthorizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ManualRefundAuthority authority,
            decimal approvedRefundAmount,
            CancellationToken cancellationToken = default)
        {
            var decision = await DecideAsync(
                order, operation, ticket, authority, approvedRefundAmount, cancellationToken);

            if (decision.Outcome == ManualRefundAuthorizationOutcome.Approved)
                return;

            throw decision.Outcome == ManualRefundAuthorizationOutcome.Denied
                ? ExceptionFactory.ManualRefundNotAuthorized(
                    order.Id, Detail(decision, "the request was refused"))
                : ExceptionFactory.ManualRefundAuthorizationUnavailable(
                    order.Id, Detail(decision, "the authority returned no decision"));
        }

        private async Task<ManualRefundAuthorizationDecision> DecideAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ManualRefundAuthority authority,
            decimal approvedRefundAmount,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _authorization.AuthorizeAsync(
                    new ManualRefundAuthorizationRequest(
                        order.Id,
                        ticket.Id,
                        operation.OperationId,
                        _callerContext.ActorId!.Value,
                        CallerScope.For(_callerContext),
                        _callerContext.ContextType!.Value,
                        _callerContext.AuthorizationSurface!.Value,
                        approvedRefundAmount,
                        order.CurrencyId,
                        authority.Reference,
                        authority.Reason),
                    cancellationToken);
            }
            catch (BusinessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new ManualRefundAuthorizationDecision(
                    ManualRefundAuthorizationOutcome.Unavailable, null, exception.Message);
            }
        }

        private static string Detail(ManualRefundAuthorizationDecision decision, string fallback)
            => string.IsNullOrWhiteSpace(decision.Detail) ? fallback : decision.Detail;
    }
}
