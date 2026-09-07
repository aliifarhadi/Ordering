namespace AeroTech.Framework.Infrastructure.Options
{
    public sealed class RedisOptions
    {
        public string Host { get; set; } = default!;

        public string Prefix { get; set; } = string.Empty;
    }
}
