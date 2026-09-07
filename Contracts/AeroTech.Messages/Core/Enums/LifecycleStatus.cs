using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum LifecycleStatus
{
    [Display(Name = "Draft", Description = "Draft")]
    Draft = 1,

    [Display(Name = "Active", Description = "Active")]
    Active = 2,

    [Display(Name = "Suspended", Description = "Suspended")]
    Suspended = 3,

    [Display(Name = "Inactive", Description = "Inactive")]
    Inactive = 4,

    [Display(Name = "Closed", Description = "Closed")]
    Closed = 5
}
