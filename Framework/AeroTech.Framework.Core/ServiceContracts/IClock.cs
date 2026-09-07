namespace AeroTech.Framework.Core.ServiceContracts
{
    public interface IClock
    {
        DateTimeOffset GetDateTime();

        DateOnly GetDate();
    }
}
