using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Messages.Aegis.IntegrationEvents.V1
{
    public sealed record AuthorizationPolicyPublished(
        long PolicyVersion,
        int BundleFormatVersion,
        int EnforcementContractVersion,
        string MinimumEnforcementRuntimeVersion,
        int SecurityEnvelopeVersion,
        string SourceModelHash,
        DateTimeOffset PublishedAt,
        long? RollbackOfPolicyVersion,
        long? RestoredFromPolicyVersion,
        IReadOnlyList<PublishedPolicyBundle> Bundles) : BaseIntegrationEvent;

    public sealed record PublishedPolicyBundle(
        PolicyConsumerClass ConsumerClass,
        string? ConsumerCode,
        string BundleHash);
}
