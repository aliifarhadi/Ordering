using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum EmdCouponAssociationChangeKind
    {
        [Display(Name = "Associated")] Associated = 1,
        [Display(Name = "Disassociated By Reissue")] DisassociatedByReissue = 2,
        [Display(Name = "Reassociated")] Reassociated = 3
    }
}
