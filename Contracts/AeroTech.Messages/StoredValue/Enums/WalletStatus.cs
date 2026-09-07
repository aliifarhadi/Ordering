using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum WalletStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Active")] Active = 2,
        [Display(Name = "Frozen")] Frozen = 3,
        [Display(Name = "Suspended")] Suspended = 4,
        [Display(Name = "Closing")] Closing = 5,
        [Display(Name = "Closed")] Closed = 6
    }
}
