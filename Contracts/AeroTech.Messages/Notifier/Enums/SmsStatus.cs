using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Notifier.Enums
{
    public enum SmsStatus
    {
        [Display(Name = "در انتظار ارسال")]
        Pending = 0,

        [Display(Name = "در حال ارسال")]
        Sending = 1,

        [Display(Name = "ارسال شده")]
        Sent = 2,
        
        [Display(Name = "خطا")]
        Failed = 3,

        [Display(Name = "تحویل شده")]
        Delivered = 4,

        [Display(Name = "تحویل نشد")]
        UnDelivered = 5,

        [Display(Name = "غیرقابل ارسال")]
        Invalid = 6,

    }
}
