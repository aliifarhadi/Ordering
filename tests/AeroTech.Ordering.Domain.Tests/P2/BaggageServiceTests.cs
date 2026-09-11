using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class BaggageServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_prepaid_piece_is_a_separately_priced_baggage_service()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Baggage(pieces: 2));

            var baggage = Baggage(order);

            Assert.Equal(BaggageServiceKind.PrepaidPiece, baggage.BaggageDetail!.Kind);
            Assert.Equal(2, baggage.BaggageDetail!.Pieces);
            Assert.Equal(ServicePriceTreatment.SeparatelyPriced, baggage.PriceTreatment);
        }

        [Fact]
        public void An_included_allowance_is_a_baggage_service_with_included_treatment()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(kind: BaggageServiceKind.Allowance, priceTreatment: ServicePriceTreatment.Included));

            var baggage = Baggage(order);

            Assert.Equal(BaggageServiceKind.Allowance, baggage.BaggageDetail!.Kind);
            Assert.Equal(ServicePriceTreatment.Included, baggage.PriceTreatment);
        }

        [Fact]
        public void A_weight_based_allowance_records_its_unit()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(kind: BaggageServiceKind.ExcessWeight, pieces: null, weight: 10m, unit: BaggageWeightUnit.Kg));

            var detail = Baggage(order).BaggageDetail!;

            Assert.Equal(10m, detail.Weight);
            Assert.Equal(BaggageWeightUnit.Kg, detail.WeightUnit);
            Assert.Null(detail.Pieces);
        }

        [Fact]
        public void A_weight_without_a_unit_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(() => AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(pieces: null, weight: 10m, unit: null)));

            Assert.Equal(20144, exception.Code);
        }

        [Fact]
        public void A_negative_piece_count_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(() => AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(pieces: -1)));

            Assert.Equal(20143, exception.Code);
        }

        [Fact]
        public void A_negative_weight_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(() => AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(pieces: null, weight: -1m, unit: BaggageWeightUnit.Kg)));

            Assert.Equal(20143, exception.Code);
        }

        [Fact]
        public void A_per_piece_weight_limit_is_recorded_when_stated()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(pieces: 2, unit: BaggageWeightUnit.Kg, perPieceWeightLimit: 23m));

            var detail = Baggage(order).BaggageDetail!;

            Assert.Equal(23m, detail.PerPieceWeightLimit);
            Assert.Equal(BaggageWeightUnit.Kg, detail.WeightUnit);
        }

        [Fact]
        public void A_baggage_service_states_what_it_covers()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Baggage());

            var baggage = Baggage(order);
            var covered = Assert.Single(baggage.CoveredServices);

            Assert.Contains(order.AirTransportServices, air => air.Id == covered.CoveredOrderServiceId);
        }

        [Fact]
        public void A_baggage_service_needs_no_reservation_and_no_document_by_default()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Baggage());

            var baggage = Baggage(order);

            Assert.False(baggage.RequiresReservation);
            Assert.False(baggage.RequiresDocument);
            Assert.Null(baggage.DocumentKind);
        }

        [Fact]
        public void A_special_baggage_service_keeps_its_kind()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage(kind: BaggageServiceKind.SpecialBaggage, pieces: 1));

            Assert.Equal(BaggageServiceKind.SpecialBaggage, Baggage(order).BaggageDetail!.Kind);
        }

        [Fact]
        public void A_baggage_service_with_a_quantity_reports_it()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Baggage(pieces: 1));

            Assert.True(Baggage(order).BaggageDetail!.HasQuantity);
        }

        private static OrderService Baggage(Order order)
            => order.OrderServices.Single(service => service.ServiceType == OrderServiceType.BaggageAllowance);
    }
}
