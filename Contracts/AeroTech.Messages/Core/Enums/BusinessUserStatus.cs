using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum BusinessUserStatus
{
    [Display(Name = "Pending", Description = "Pending")]
    Pending = 1,

    [Display(Name = "Active", Description = "Active")]
    Active = 2,

    [Display(Name = "Suspended", Description = "Suspended")]
    Suspended = 3,

    [Display(Name = "Terminated", Description = "Terminated")]
    Terminated = 4
}
