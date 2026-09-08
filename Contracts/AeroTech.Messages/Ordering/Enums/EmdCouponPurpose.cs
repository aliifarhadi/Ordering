using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum EmdCouponPurpose
    {
        [Display(Name = "Service")] Service = 1,
        [Display(Name = "Fee")] Fee = 2,
        [Display(Name = "Deposit")] Deposit = 3,
        [Display(Name = "Residual Value")] ResidualValue = 4,
    }
}
