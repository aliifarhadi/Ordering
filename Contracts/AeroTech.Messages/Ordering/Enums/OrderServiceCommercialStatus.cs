using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderServiceCommercialStatus
    {
    [Display(Name = "Pending")] Pending = 1,
    [Display(Name = "Active")] Active = 2,
    [Display(Name = "Cancelled")] Cancelled = 3,
    [Display(Name = "Exchanged")] Exchanged = 4,
    [Display(Name = "Suspended")] Suspended = 5
    }
}
