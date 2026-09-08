using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedBaggageDetail(
        BaggageServiceKind Kind,
        int? Pieces = null,
        decimal? Weight = null,
        BaggageWeightUnit? WeightUnit = null,
        decimal? PerPieceWeightLimit = null) : AcceptedServiceDetail;
}
