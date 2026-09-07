namespace AeroTech.Framework.Core.Domain.Entities
{
    public interface IEntity
    {
        void SetLastUpdated(long? userId, DateTimeOffset dateTime);
    }
}
