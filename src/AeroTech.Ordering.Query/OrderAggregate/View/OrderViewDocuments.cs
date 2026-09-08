using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record OrderViewReservation(
        long ReservationId,
        FulfillmentReservationStatus Status,
        string? ExternalReservationReference,
        DateTimeOffset? ExpiresAt,
        IReadOnlyList<OrderViewReservationMember> Members);

    public sealed record OrderViewReservationMember(
        long OrderServiceId,
        ReservationMemberStatus ObservedStatus,
        string? ExternalServiceReference);

    public sealed record OrderViewElectronicTicket(
        long TicketId,
        string DocumentNumber,
        long TravelerId,
        ElectronicTicketStatus Status,
        DateTimeOffset IssuedAt,
        decimal IssuedTotal,
        int CurrencyId,
        string? ProviderReference,
        int DocumentVersion,
        IReadOnlyList<OrderViewTicketCoupon> Coupons);

    public sealed record OrderViewTicketCoupon(
        long CouponId,
        int CouponNumber,
        long OrderServiceId,
        long JourneySegmentId,
        TicketCouponFinancialStatus FinancialStatus,
        TicketCouponControlStatus ControlStatus,
        string? FareBasis,
        decimal IssuanceValue);

    public sealed record OrderViewMiscellaneousDocument(
        long ElectronicMiscDocumentId,
        string DocumentNumber,
        ElectronicMiscDocumentType Type,
        string ReasonForIssuanceCode,
        ElectronicMiscDocumentStatus Status,
        long? TravelerId,
        DateTimeOffset IssuedAt,
        decimal IssuedTotal,
        int CurrencyId,
        string? ProviderReference,
        int DocumentVersion,
        IReadOnlyList<OrderViewMiscellaneousCoupon> Coupons);

    public sealed record OrderViewMiscellaneousCoupon(
        long CouponId,
        int CouponNumber,
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        long? OrderServiceId,
        long? PricingLineId,
        long? AssociatedTicketCouponId,
        string? ExternalValueReference,
        decimal IssuanceValue,
        EmdCouponStatus Status);
}
