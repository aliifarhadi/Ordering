using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Providers.Unconfigured;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentRefund
{
    public sealed class UnconfiguredDocumentRefundProviderTests
    {
        [Fact]
        public async Task An_unconfigured_document_refund_authority_fails_closed_on_every_act()
        {
            var provider = new UnconfiguredDocumentRefundProvider();

            var eligibility = await Assert.ThrowsAsync<BusinessException>(
                () => provider.CheckEligibilityAsync(DocumentRefundPortFixture.Eligibility()));
            var refund = await Assert.ThrowsAsync<BusinessException>(
                () => provider.RefundAsync(DocumentRefundPortFixture.Request()));
            var recovery = await Assert.ThrowsAsync<BusinessException>(
                () => provider.RecoverAsync(DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key)));

            foreach (var refusal in new[] { eligibility, refund, recovery })
            {
                Assert.Equal(20224, refusal.Code);
                Assert.Equal(501, refusal.HttpStatus);
            }
        }
    }
}
