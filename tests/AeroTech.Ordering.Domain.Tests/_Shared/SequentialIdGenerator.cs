using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public sealed class SequentialIdGenerator : IIdGenerator
    {
        private long _next = 1_000_000_000_000;

        public long NewId() => Interlocked.Increment(ref _next);
    }
}
