using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderBaggageServiceDetail : Entity<long>
    {
        private OrderBaggageServiceDetail()
        {
        }

        internal OrderBaggageServiceDetail(
            long id,
            long orderServiceId,
            BaggageServiceKind kind,
            int? pieces,
            decimal? weight,
            BaggageWeightUnit? weightUnit,
            decimal? perPieceWeightLimit)
        {
            if (pieces is < 0)
                throw ExceptionFactory.BaggageQuantityMustBeNonNegative();

            if (weight is < 0m || perPieceWeightLimit is < 0m)
                throw ExceptionFactory.BaggageQuantityMustBeNonNegative();

            if ((weight.HasValue || perPieceWeightLimit.HasValue) && weightUnit is null)
                throw ExceptionFactory.BaggageWeightRequiresUnit();

            Id = id;
            OrderServiceId = orderServiceId;
            Kind = kind;
            Pieces = pieces;
            Weight = weight;
            WeightUnit = weightUnit;
            PerPieceWeightLimit = perPieceWeightLimit;
        }

        public long OrderServiceId { get; private set; }

        public BaggageServiceKind Kind { get; private set; }

        public int? Pieces { get; private set; }

        public decimal? Weight { get; private set; }

        public BaggageWeightUnit? WeightUnit { get; private set; }

        public decimal? PerPieceWeightLimit { get; private set; }

        public bool HasQuantity => Pieces is > 0 || Weight is > 0m;
    }
}
