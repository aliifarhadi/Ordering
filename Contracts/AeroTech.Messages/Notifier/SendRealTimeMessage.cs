namespace AeroTech.Messages.Notifier
{
    
    public class SendRealTimeMessageToAddress
    {
        public string[]? Addresses { get; set; }
        public string? Type { get; set; }
        public string? Body { get; set; }
    }
}
