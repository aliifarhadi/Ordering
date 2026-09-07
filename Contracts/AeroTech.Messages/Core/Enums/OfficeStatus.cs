using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum OfficeStatus
{
    [Display(Name = "Draft", Description = "Draft")]
    Draft = 1,

    [Display(Name = "Active", Description = "Active")]
    Active = 2,

    [Display(Name = "Suspended", Description = "Suspended")]
    Suspended = 3,

    [Display(Name = "Closed", Description = "Closed")]
    Closed = 4
}
