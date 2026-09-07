using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FormOfPayment
    {
        [Display(Name = "Cash")]
        Cash = 1,

        [Display(Name = "On Account")]
        OnAccount = 2,

        [Display(Name = "Credit Card")]
        CreditCard = 3,

        [Display(Name = "Online Payment")]
        OnlinePayment = 4
    }
}
