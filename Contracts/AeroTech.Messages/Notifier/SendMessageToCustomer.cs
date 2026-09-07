using AeroTech.Messages.Notifier.Enums;

namespace AeroTech.Messages.Notifier
{

    public class SendMessageToCustomer
    {
        public Guid Id { get; set; }
        public long CustomerId { get; set; }
        public DateTime? DueTime { get; set; }
        public string[]? Tags { get; set; }
        public SendSmsToCustomerRequest? Sms { get; set; }
        public SendEmailToCustomerRequest? Email { get; set; }
    }

    
    public class SendSmsToCustomerRequest
    {
        public string? Body { get; set; }
        public string? Sender { get; set; }
        public SmsGateway? Gateway { get; set; }
    }
    public class SendEmailToCustomerRequest
    {

        public string? Subject { get; set; }
        public string? Body { get; set; }
        public bool IsHtml { get; set; }
    }

}
