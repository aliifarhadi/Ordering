using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryCancellationTerms(
        string CancellationReference,
        string SourceReference,
        AncillaryCancellationDocumentAction DocumentAction);
}
