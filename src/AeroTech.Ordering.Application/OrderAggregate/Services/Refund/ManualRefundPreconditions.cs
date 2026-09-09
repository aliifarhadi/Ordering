using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public static class ManualRefundPreconditions
    {
        public static ManualRefundAuthority EnsureContextIsEligible(
            ICallerContext caller,
            ManualRefundInstruction instruction,
            long orderId)
        {
            ArgumentNullException.ThrowIfNull(instruction);

            if (!caller.IsAuthenticated)
                throw ExceptionFactory.CallerContextUnavailable();

            if (caller.AuthorizationSurface != AuthorizationSurface.Backoffice
                || caller.ContextType != BusinessContextType.Airline)
                throw ExceptionFactory.ManualRefundContextNotEligible(orderId);

            if (caller.ActorId is null)
                throw ExceptionFactory.ManualRefundRequiresAuthority();

            return new ManualRefundAuthority(instruction.AuthorityReference, instruction.Reason);
        }
    }
}
