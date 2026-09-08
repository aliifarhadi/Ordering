namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedAirTransportDetail(
        string SegmentRef,
        string? TransitionalFareBasis = null,
        string? RequestedSeat = null,
        AcceptedBaggageAllowance? TransitionalCheckedBaggage = null,
        AcceptedBaggageAllowance? TransitionalCabinBaggage = null) : AcceptedServiceDetail;
}
