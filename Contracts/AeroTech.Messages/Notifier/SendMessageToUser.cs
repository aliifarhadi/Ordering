using AeroTech.Messages.Notifier.Enums;

namespace AeroTech.Messages.Notifier
{

    public class SendMessageToUser
    {
        public Guid Id { get; set; }
        public long UserId { get; set; }
        public DateTime? DueTime { get; set; }
        public string[]? Tags { get; set; }
        public SendInboxToUserRequest? Inbox { get; set; }
        public SendSmsToUserRequest? Sms { get; set; }
        public SendEmailToUserRequest? Email { get; set; }
        public SendPushNotificationToUserRequest? PushNotification { get; set; }
    }

    public class SendInboxToUserRequest
    {
        public string? Title { get; set; }
        public string? Icon { get; set; }
        public string? Body { get; set; }
    }
    public class SendSmsToUserRequest
    {
        public string? Body { get; set; }
        public string? Sender { get; set; }
        public SmsGateway? Gateway { get; set; }
    }
    public class SendEmailToUserRequest
    {

        public string? Subject { get; set; }
        public string? Body { get; set; }
        public bool IsHtml { get; set; }
    }

    public class SendPushNotificationToUserRequest
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? Url { get; set; }
        public string? Data { get; set; }
    }
}
