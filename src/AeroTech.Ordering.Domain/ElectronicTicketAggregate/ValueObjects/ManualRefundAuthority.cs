using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects
{
    public sealed class ManualRefundAuthority
    {
        private ManualRefundAuthority()
        {
        }

        public ManualRefundAuthority(string reference, string reason)
        {
            if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(reason))
                throw ExceptionFactory.ManualRefundRequiresAuthority();

            Reference = reference.Trim();
            Reason = reason.Trim();
        }

        public string Reference { get; private set; } = default!;

        public string Reason { get; private set; } = default!;
    }
}
