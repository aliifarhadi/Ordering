using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum AddressType
{
    [Display(Name = "Registered", Description = "Registered")]
    Registered = 1,

    [Display(Name = "Business", Description = "Business")]
    Business = 2,

    [Display(Name = "Mailing", Description = "Mailing")]
    Mailing = 3,

    [Display(Name = "Billing", Description = "Billing")]
    Billing = 4,

    [Display(Name = "Operational", Description = "Operational")]
    Operational = 5,

    [Display(Name = "Other", Description = "Other")]
    Other = 6
}
