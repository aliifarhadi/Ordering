using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using AeroTech.Ordering.Providers.Unconfigured;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdAssociation
{
    public sealed class UnconfiguredAncillaryProviderTests
    {
        [Fact]
        public async Task An_unconfigured_association_source_refuses_to_move_an_ancillary()
        {
            var provider = new UnconfiguredEmdAssociationProvider();

            var failure = await Assert.ThrowsAsync<BusinessException>(
                () => provider.ReassociateAsync(EmdAssociationPortFixture.Request()));

            Assert.Equal(20295, failure.Code);
            Assert.Equal(501, failure.HttpStatus);
        }

        [Fact]
        public async Task An_unconfigured_association_source_refuses_to_invent_a_read_back()
        {
            var provider = new UnconfiguredEmdAssociationProvider();

            var failure = await Assert.ThrowsAsync<BusinessException>(
                () => provider.RecoverReassociationAsync(
                    EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key)));

            Assert.Equal(20295, failure.Code);
            Assert.Equal(501, failure.HttpStatus);
        }

        [Fact]
        public async Task An_unconfigured_disposition_source_refuses_to_decide_for_the_airline()
        {
            var provider = new UnconfiguredAncillaryDispositionProvider();

            var failure = await Assert.ThrowsAsync<BusinessException>(
                () => provider.DecideAsync(new AncillaryExchangeDispositionRequest(
                    EmdAssociationPortFixture.OrderId,
                    EmdAssociationPortFixture.OperationId,
                    "EXC-QUOTE-CONTRACT-1",
                    EmdAssociationPortFixture.PredecessorDocumentNumber,
                    [EmdAssociationPortFixture.PredecessorCouponNumber],
                    [])));

            Assert.Equal(20294, failure.Code);
            Assert.Equal(501, failure.HttpStatus);
        }
    }
}
