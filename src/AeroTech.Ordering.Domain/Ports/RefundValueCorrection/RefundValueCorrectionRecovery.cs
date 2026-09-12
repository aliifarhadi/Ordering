using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.RefundValueCorrection
{
    public sealed record RefundValueCorrectionRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null);
}
