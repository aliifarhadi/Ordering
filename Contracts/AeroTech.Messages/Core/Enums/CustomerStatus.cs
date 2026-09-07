using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum CustomerStatus
{
    [Display(Name = "Active", Description = "Active")]
    Active = 1,

    [Display(Name = "Suspended", Description = "Suspended")]
    Suspended = 2,

    [Display(Name = "Closed", Description = "Closed")]
    Closed = 3
}
