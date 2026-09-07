namespace AeroTech.Ordering.Consumers
{
    public sealed class RabbitMqOptions
    {
        public string Server { get; set; } = default!;

        public ushort Port { get; set; } = 5672;

        public string VirtualHost { get; set; } = "/";

        public string UserName { get; set; } = default!;

        public string Password { get; set; } = default!;
    }
}
