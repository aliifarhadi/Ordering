namespace AeroTech.Framework.Infrastructure.Options
{
    public sealed class IdGeneratorOptions
    {
        public const int MaxGeneratorId = 1023;

        public int? GeneratorId { get; set; }
    }
}
