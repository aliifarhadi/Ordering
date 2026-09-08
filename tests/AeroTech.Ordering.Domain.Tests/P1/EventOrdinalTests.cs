using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Versioning;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P1
{
    public sealed class EventOrdinalTests
    {
        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void Ordinals_within_one_mutation_are_sequential_from_one()
        {
            var sequence = new CommercialEventSequence();

            Assert.Equal(1, sequence.Next());
            Assert.Equal(2, sequence.Next());
            Assert.Equal(3, sequence.Next());
        }

        [Fact]
        public void The_next_commercial_mutation_restarts_the_ordinal_at_one()
        {
            var sequence = new CommercialEventSequence();

            sequence.Next();
            sequence.Next();
            sequence.Reset();

            Assert.Equal(1, sequence.Next());
        }

        [Fact]
        public void Several_facts_from_one_mutation_share_a_version_and_receive_sequential_ordinals()
        {
            var commercialVersion = 4;
            var sequence = new CommercialEventSequence();

            var facts = Enumerable.Range(0, 3)
                .Select(_ => new { CommercialVersion = commercialVersion, EventOrdinal = sequence.Next() })
                .ToList();

            Assert.All(facts, fact => Assert.Equal(commercialVersion, fact.CommercialVersion));
            Assert.Equal([1, 2, 3], facts.Select(fact => fact.EventOrdinal));
        }

        [Fact]
        public void Creation_emits_its_fact_at_commercial_version_one_ordinal_one()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var created = order.GetEvents().OfType<OrderCreated>().Single();

            Assert.Equal(1, created.CommercialVersion);
            Assert.Equal(1, created.EventOrdinal);
        }

        [Fact]
        public void The_next_commercial_mutation_advances_the_version_and_restarts_the_ordinal()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            order.ClearEvents();

            order.WithdrawBeforeTicketing(
                order.OrderServices.Select(service => service.Id).ToList(),
                VoidReason.CustomerRequest,
                7,
                _ids,
                _clock);

            var withdrawn = order.GetEvents().OfType<OrderWithdrawn>().Single();

            Assert.Equal(2, withdrawn.CommercialVersion);
            Assert.Equal(1, withdrawn.EventOrdinal);
        }

        [Fact]
        public void Non_commercial_evidence_emits_no_commercial_fact_and_does_not_advance_the_version()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            order.ClearEvents();

            var services = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(services, "PNR-1", null, _ids, _clock);
            order.ApplyIssuedDocuments(
                services.Select(id => new Domain.OrderAggregate.IssuedServiceDocument(id, 900 + id, 800 + id)).ToList(),
                _clock);

            Assert.Empty(order.GetEvents());
            Assert.Equal(1, order.CommercialVersion);
        }
    }
}
