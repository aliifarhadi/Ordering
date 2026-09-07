using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum FundLotLifecycleStatus
    {
       [Display(Name = "Pending")] Pending = 1,
       [Display(Name = "Active")] Active = 2,
       [Display(Name = "Cancelled")] Cancelled = 3
    }
}
