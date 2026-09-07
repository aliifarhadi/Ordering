using AeroTech.Messages.Notifier.Enums;

namespace AeroTech.Messages.Notifier
{
    public class SendVerifyMessage
    {
        public DateTime ExpireTime { get; set; }
        public string? PhoneOrEmail { get; set; }
        public string? Code { get; set; }
        public VerifyMessageType Type { get; set; }
        public VerifyMessageTemplate Template { get; set; }
    }
}