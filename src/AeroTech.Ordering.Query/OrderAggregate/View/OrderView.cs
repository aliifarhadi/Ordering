using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record OrderView(
        int SchemaVersion,
        long OrderId,
        Guid UniqueIdentifierId,
        string? RecordLocator,
        int CommercialVersion,
        long ObligationVersion,
        long ProjectionRevision,
        DateTimeOffset UpdatedAt,
        CommercialSummary CommercialSummary,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,
        long OwnerAirlineId,
        long CustomerId,
        long AirlineOfficeId,
        int CurrencyId,
        int Pax,
        DateTimeOffset CreationDate,
        DateTimeOffset? TimeToLive,
        OrderViewTotals Totals,
        OrderViewFacets Facets,
        IReadOnlyList<OrderViewTraveller> Travellers,
        IReadOnlyList<OrderViewJourney> Journeys,
        IReadOnlyList<OrderViewItem> Items,
        IReadOnlyList<OrderViewService> Services,
        IReadOnlyList<OrderViewFareConstruction> FareConstructions,
        IReadOnlyList<OrderViewChange> Changes,
        IReadOnlyList<OrderViewPriceChangeSet> PricingHistory,
        IReadOnlyList<OrderViewReservation> Reservations,
        IReadOnlyList<OrderViewElectronicTicket> ElectronicTickets,
        IReadOnlyList<OrderViewMiscellaneousDocument> MiscellaneousDocuments,
        IReadOnlyList<OrderViewTimeLimit> TimeLimits,
        IReadOnlyList<OrderViewExternalReference> ExternalReferences)
    {
        public const int CurrentSchemaVersion = 1;
    }

    public sealed record OrderViewTotals(
        decimal GrandTotal,
        decimal BaseFareTotal,
        decimal ProductChargeTotal,
        decimal TaxTotal,
        decimal SurchargeTotal,
        decimal FeeTotal,
        decimal DiscountTotal,
        decimal PenaltyTotal,
        decimal CommissionAmount,
        decimal CommissionRate);

    public sealed record OrderViewFacets(
        CommercialSummary Commercial,
        FulfillmentReservationStatus? Reservation,
        OrderViewDocumentFacet ElectronicTicket,
        OrderViewDocumentFacet MiscellaneousDocument);

    public sealed record OrderViewDocumentFacet(
        int RequiredServices,
        int DocumentedServices,
        bool IsComplete);

    public sealed record OrderViewTimeLimit(TimeLimitType Type, DateTimeOffset DueAt, TimeLimitStatus Status);

    public sealed record OrderViewExternalReference(ExternalReferenceType Type, string SourceSystem, string Reference);
}
