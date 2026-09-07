using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Notifier.Enums
{
    public enum SmsGateway
    {
        [Display(Name = "کاوه نگار")]
        Kavenegar = 1,

        [Display(Name = "نیک اس ام اس")]
        Niksms = 2
    }
}
