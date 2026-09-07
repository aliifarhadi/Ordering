namespace AeroTech.Framework.Core.ServiceContracts
{
    public interface IDistributedLock
    {
        Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default);
    }
}
