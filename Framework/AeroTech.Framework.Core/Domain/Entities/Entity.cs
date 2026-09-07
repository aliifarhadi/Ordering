namespace AeroTech.Framework.Core.Domain.Entities
{
    public abstract class Entity<TId> : IEntity where TId : notnull
    {
        public TId Id { get; protected set; } = default!;

        public DateTimeOffset LastUpdateTime { get; private set; }

        public long? LastUpdatedBy { get; private set; }

        public void SetLastUpdated(long? userId, DateTimeOffset dateTime)
        {
            LastUpdateTime = dateTime;
            LastUpdatedBy = userId;
        }

        public bool Equals(Entity<TId>? other) => this == other;

        public override bool Equals(object? obj) => obj is Entity<TId> other && Id.Equals(other.Id);

        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        {
            if (left is null && right is null)
                return true;
            if (left is null || right is null)
                return false;
            return left.Equals(right);
        }

        public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(right == left);
    }
}
