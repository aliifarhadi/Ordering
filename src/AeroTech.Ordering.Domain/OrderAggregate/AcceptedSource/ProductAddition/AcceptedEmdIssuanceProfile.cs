using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedEmdIssuanceProfile(
        ElectronicMiscDocumentType EmdType,
        string ReasonForIssuanceCode,
        string ReasonForIssuanceSubCode,
        long? AssociatedAirOrderServiceId = null,
        string? DocumentGroupReference = null,
        string? SourceSystem = null,
        string? SourceReference = null);
}
