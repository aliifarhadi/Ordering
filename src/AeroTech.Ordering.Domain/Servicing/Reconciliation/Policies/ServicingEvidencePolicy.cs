using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies
{
    public static class ServicingEvidencePolicy
    {
        public static IReadOnlyList<ProviderOperationOutcome> OutcomesSupersededBy(ProviderOperationOutcome outcome)
            => outcome switch
            {
                ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected =>
                    [ProviderOperationOutcome.Pending, ProviderOperationOutcome.Unknown],
                ProviderOperationOutcome.Unknown => [ProviderOperationOutcome.Pending],
                _ => []
            };

        public static bool Contradicts(ServicingEvidenceRecording recording)
        {
            ArgumentNullException.ThrowIfNull(recording);

            var attempted = recording.Attempted;
            var durable = recording.Durable;

            if (recording.Applied || attempted.IsUnresolved)
                return false;

            return durable.Outcome != attempted.Outcome
                   || !string.Equals(durable.ProviderReference, attempted.ProviderReference, StringComparison.Ordinal)
                   || durable.DocumentKind != attempted.DocumentKind
                   || !string.Equals(durable.DocumentNumber, attempted.DocumentNumber, StringComparison.Ordinal);
        }
    }
}
