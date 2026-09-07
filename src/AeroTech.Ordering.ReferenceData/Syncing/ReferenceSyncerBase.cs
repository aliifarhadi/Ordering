using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public abstract class ReferenceSyncerBase<TReadModel, TDto, TKey>
        where TReadModel : class, IReferenceReadModel<TKey>
        where TDto : ISyncSourceDto<TKey>
        where TKey : notnull
    {
        protected ReferenceDbContext Db { get; }

        private readonly TimeProvider _timeProvider;

        protected ReferenceSyncerBase(ReferenceDbContext db, TimeProvider timeProvider)
        {
            Db = db;
            _timeProvider = timeProvider;
        }

        protected abstract string Resource { get; }

        protected abstract Task<List<TDto>> FetchAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken);

        protected abstract TReadModel CreateNew(TDto dto);

        protected abstract void ApplyChanges(TDto dto, TReadModel model);

        protected virtual Task SyncChildrenAsync(TDto dto, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            var lastSync = await GetLastSyncAsync(cancellationToken);
            var dtos = await FetchAsync(lastSync, cancellationToken);
            var set = Db.Set<TReadModel>();

            foreach (var dto in dtos)
            {
                var existing = await set.FindAsync(new object?[] { dto.Id }, cancellationToken);

                if (existing is not null && existing.LastUpdateTime >= dto.LastUpdateTime)
                    continue;

                if (dto is ISoftDeletable { IsDeleted: true })
                {
                    if (existing is not null)
                        set.Remove(existing);
                    continue;
                }

                if (existing is null)
                    await set.AddAsync(CreateNew(dto), cancellationToken);
                else
                    ApplyChanges(dto, existing);

                await SyncChildrenAsync(dto, cancellationToken);
            }

            await StampLastSyncAsync(cancellationToken);
            await Db.SaveChangesAsync(cancellationToken);
        }

        private async Task<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken)
        {
            var state = await Db.SyncStates.FindAsync(new object?[] { Resource }, cancellationToken);
            return state?.LastSync;
        }

        private async Task StampLastSyncAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();
            var state = await Db.SyncStates.FindAsync(new object?[] { Resource }, cancellationToken);

            if (state is null)
                await Db.SyncStates.AddAsync(new ReferenceSyncState { Id = Resource, LastSync = now }, cancellationToken);
            else
                state.LastSync = now;
        }
    }
}
