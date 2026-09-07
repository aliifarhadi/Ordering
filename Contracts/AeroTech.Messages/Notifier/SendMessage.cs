using AeroTech.Messages.Notifier.Enums;


namespace AeroTech.Messages.Notifier
{
    public class SendMessage
    {
        public Guid Id { get; set; }
        public DateTime? DueTime { get; set; }
        public string[]? Tags { get; set; }
        public SendSmsRequest? Sms { get; set; }
        public SendEmailRequest? Email { get; set; }
        public SendPushNotificationRequest? PushNotification { get; set; }
    }

    public class SendSmsRequest
    {
        public string? PhoneNumber { get; set; }
        public string? Body { get; set; }
        public string? Sender { get; set; }
        public SmsGateway? Gateway { get; set; }
    }

    public class SendEmailRequest
    {

        public string[]? Addresses { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public bool IsHtml { get; set; }
    }

    public class SendPushNotificationRequest
    {
        public SendPushNotificationDeviceRequest[]? Addresses { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? Url { get; set; }
        public string? Data { get; set; }
    }
    public class SendPushNotificationDeviceRequest
    {
        public string? Address { get; set; }
        public NotificationTokenType NotificationTokenType { get; set; }
    }
}
