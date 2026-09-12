using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryRetentionTerms(
        string RetentionReference,
        string SourceReference,
        AncillaryRetentionMode RetentionMode);
}
