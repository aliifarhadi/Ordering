using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum EmdCouponStatus
    {
        [Display(Name = "Open For Use")] OpenForUse = 1,
        [Display(Name = "Void")] Void = 2,
    }
}
