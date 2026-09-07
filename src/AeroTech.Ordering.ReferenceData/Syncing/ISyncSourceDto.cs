namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public interface ISyncSourceDto<TKey>
    {
        TKey Id { get; }
        DateTimeOffset LastUpdateTime { get; }
    }

    public interface ISoftDeletable
    {
        bool IsDeleted { get; }
    }
}
