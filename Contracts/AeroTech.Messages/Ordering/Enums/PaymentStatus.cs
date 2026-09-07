using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PaymentStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Captured")] Captured = 2,
        [Display(Name = "Declined")] Declined = 3,
        [Display(Name = "Unconfirmed")] Unconfirmed = 4,
        [Display(Name = "Voided")] Voided = 5
    }
}
