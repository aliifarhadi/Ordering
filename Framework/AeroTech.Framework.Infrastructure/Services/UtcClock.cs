using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Framework.Infrastructure.Services
{
    public sealed class UtcClock : IClock
    {
        public DateTimeOffset GetDateTime() => DateTimeOffset.UtcNow;

        public DateOnly GetDate() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
    }
}
