using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public sealed class SequentialIdGenerator : IIdGenerator
    {
        private long _next;

        public SequentialIdGenerator() : this(1_000_000_000_000)
        {
        }

        public SequentialIdGenerator(long seed) => _next = seed;

        public static SequentialIdGenerator Unique()
            => new(DateTime.UtcNow.Ticks + Random.Shared.NextInt64(1, 1_000_000_000));

        public long NewId() => Interlocked.Increment(ref _next);
    }
}
