using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using AeroTech.Ordering.Providers.Deterministic;
using AeroTech.Ordering.Providers.Unconfigured;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.Contracts.AncillaryDisposition.AncillaryDispositionPortFixture;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.AncillaryDisposition
{
    public sealed class DeterministicAncillaryDispositionPortTests : AncillaryDispositionPortContract
    {
        protected override IAncillaryExchangeDispositionPort Port() => new DeterministicAncillaryDispositionAdapter();

        protected override IAncillaryExchangeDispositionPort PortAnswering(AncillaryExchangeDisposition disposition)
            => new DeterministicAncillaryDispositionAdapter { DefaultDisposition = disposition };

        [Fact]
        public async Task A_lookup_records_what_it_was_asked_and_changes_nothing()
        {
            var adapter = new DeterministicAncillaryDispositionAdapter();
            var request = Request();

            await adapter.DecideAsync(request);
            await adapter.DecideAsync(request);

            Assert.Equal(2, adapter.ObservedRequests.Count);
            Assert.All(adapter.ObservedRequests, observed => Assert.Equal(request.ContextFingerprint, observed.ContextFingerprint));
        }

        [Fact]
        public async Task An_unreachable_source_answers_nothing_rather_than_guessing()
        {
            var adapter = new DeterministicAncillaryDispositionAdapter { Throw = true };

            await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.DecideAsync(Request()));
        }

        [Fact]
        public async Task An_unconfigured_source_refuses_to_decide_for_the_airline()
        {
            var provider = new UnconfiguredAncillaryDispositionProvider();

            var failure = await Assert.ThrowsAsync<BusinessException>(() => provider.DecideAsync(Request()));

            Assert.Equal(20294, failure.Code);
            Assert.Equal(501, failure.HttpStatus);
        }

        [Fact]
        public async Task A_steered_answer_can_break_its_own_binding_so_the_consumer_can_be_tested()
        {
            var adapter = new DeterministicAncillaryDispositionAdapter
            {
                QuotedExchangeIdOverride = OtherQuotedExchangeId,
                ContextFingerprintOverride = "0000000000000000000000000000000000000000000000000000000000000000"
            };

            var request = Request();
            var result = await adapter.DecideAsync(request);

            Assert.NotEqual(request.QuotedExchangeId, result.QuotedExchangeId);
            Assert.NotEqual(request.ContextFingerprint, result.ContextFingerprint);
        }
    }
}
