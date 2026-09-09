namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange
{
    public sealed record AcceptedChangeReplacement(
        string ServiceRef,
        string ServiceCode,
        string Name,
        AcceptedSegment Segment,
        AcceptedAirTransportDetail Detail,
        IReadOnlyList<long> BeneficiaryTravellerIds);
}
