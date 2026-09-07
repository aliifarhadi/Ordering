using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum ApiAccessStatus
{
    [Display(Name = "Draft", Description = "Draft")]
    Draft = 1,

    [Display(Name = "Active", Description = "Active")]
    Active = 2,

    [Display(Name = "Suspended", Description = "Suspended")]
    Suspended = 3,

    [Display(Name = "Revoked", Description = "Revoked")]
    Revoked = 4
}
