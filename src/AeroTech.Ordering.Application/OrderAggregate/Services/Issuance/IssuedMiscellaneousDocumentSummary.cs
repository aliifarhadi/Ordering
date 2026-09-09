using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record IssuedMiscellaneousDocumentSummary(
        long ElectronicMiscDocumentId,
        string DocumentNumber,
        ElectronicMiscDocumentType Type,
        string ReasonForIssuanceCode,
        int CouponCount);
}
