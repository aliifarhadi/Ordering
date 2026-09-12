using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public sealed record EmdExchangeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string ExchangeGroupRef,
        string SourceDocumentNumber,
        IReadOnlyList<int> SourceCouponNumbers,
        long? BeneficiaryTravellerId,
        ElectronicMiscDocumentType SuccessorType,
        string SuccessorReasonForIssuanceCode,
        int CurrencyId,
        IReadOnlyList<EmdExchangeSuccessorCouponRequest> SuccessorCoupons,
        string? SuccessorTicketDocumentNumber,
        string DecisionReference,
        string? SourcePricingReference,
        ExchangeCoupledResidualRequest? Residual = null);
}
