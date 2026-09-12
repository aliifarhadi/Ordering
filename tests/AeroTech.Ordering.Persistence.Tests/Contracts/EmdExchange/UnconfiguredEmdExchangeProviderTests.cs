using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Providers.Unconfigured;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdExchange
{
    public sealed class UnconfiguredEmdExchangeProviderTests
    {
        [Fact]
        public async Task An_unconfigured_exchange_authority_fails_closed_on_every_act()
        {
            var provider = new UnconfiguredEmdExchangeProvider();

            var exchange = await Assert.ThrowsAsync<BusinessException>(
                () => provider.ExchangeAsync(EmdExchangePortFixture.Request()));
            var recovery = await Assert.ThrowsAsync<BusinessException>(
                () => provider.RecoverAsync(EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key)));

            foreach (var refusal in new[] { exchange, recovery })
            {
                Assert.Equal(20313, refusal.Code);
                Assert.Equal(501, refusal.HttpStatus);
            }
        }
    }
}
