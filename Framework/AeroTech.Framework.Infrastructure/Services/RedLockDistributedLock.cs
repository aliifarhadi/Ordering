using AeroTech.Framework.Core.ServiceContracts;
using RedLockNet;

namespace AeroTech.Framework.Infrastructure.Services
{
    public sealed class RedLockDistributedLock : IDistributedLock
    {
        private readonly IDistributedLockFactory _factory;
        private readonly string _prefix;

        public RedLockDistributedLock(IDistributedLockFactory factory, string prefix)
        {
            _factory = factory;
            _prefix = prefix;
        }

        public async Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            var key = string.IsNullOrEmpty(_prefix) ? resource : $"{_prefix}:{resource}";
            var redLock = await _factory.CreateLockAsync(key, expiry);

            if (redLock.IsAcquired)
                return new Handle(redLock);

            redLock.Dispose();
            return null;
        }

        private sealed class Handle : IAsyncDisposable
        {
            private readonly IRedLock _redLock;

            public Handle(IRedLock redLock) => _redLock = redLock;

            public ValueTask DisposeAsync()
            {
                _redLock.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
