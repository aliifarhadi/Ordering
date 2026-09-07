namespace AeroTech.Messages
{
    public record BaseAcknowledgeCommand : BaseCommand
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
    }
}
