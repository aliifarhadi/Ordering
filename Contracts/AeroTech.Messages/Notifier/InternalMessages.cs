using AeroTech.Messages.Notifier.Enums;

namespace AeroTech.Messages.Notifier
{
    public class SendSmsMessage
    {
        public Guid MessageId { get; set; }
    }

    public class SendEmailMessage
    {
        public Guid MessageId { get; set; }
    }

    public class SendPushMessage
    {
        public Guid MessageId { get; set; }
        public NotificationTokenType TokenType { get; set; }
    }
    
   

}
