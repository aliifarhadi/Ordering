namespace AeroTech.Messages.Notifier
{
    
    public class SendRealTimeMessageToUser
    {
        public Guid UserId { get; set; }
        public string? Type { get; set; }
        public string? Body { get; set; }
    }
}
