using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public sealed class TestClock : IClock
    {
        private DateTimeOffset _now = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

        public DateTimeOffset GetDateTime() => _now;

        public DateOnly GetDate() => DateOnly.FromDateTime(_now.DateTime);

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
