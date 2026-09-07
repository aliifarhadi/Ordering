namespace AeroTech.Framework.Core.ServiceContracts
{
    public interface IInboxStore
    {
        Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default);

        Task MarkProcessedAsync(Guid messageId, string consumer, string messageType, CancellationToken cancellationToken = default);
    }
}
