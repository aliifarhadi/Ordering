using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public sealed record VoluntaryChangeOutcome(
        long OrderId,
        long OperationId,
        OrderChangeType CommercialResult,
        ServicingOperationKind TechnicalOperation,
        long ElectronicTicketId,
        string DocumentNumber,
        int DocumentVersion,
        long? TicketCouponId,
        long? OrderChangeId,
        long? ReplacedOrderServiceId,
        long? ReplacementOrderServiceId,
        int CommercialVersion,
        long FinancialSequence,
        long ObligationVersion,
        decimal CustomerTotal,
        ProviderOperationOutcome ReservationChangeOutcome,
        ProviderOperationOutcome RevalidationOutcome,
        ChangeDocumentOutcome DocumentOutcome,
        ServicingOperationStatus OperationStatus,
        ChangeMonetaryOutcome MonetaryOutcome,
        bool DeferredToExchange,
        bool IsReplay);
}
