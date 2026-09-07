
using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderItemFinancialStatus
    {
        [Display(Name = "None")] None = 1,
        [Display(Name = "Authorized")] Authorized = 2,
        [Display(Name = "Captured")] Captured = 3,
        [Display(Name = "Refunded")] Refunded = 4,
    }
}
