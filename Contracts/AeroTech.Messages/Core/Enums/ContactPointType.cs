using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum ContactPointType
{
    [Display(Name = "Email", Description = "Email")]
    Email = 1,

    [Display(Name = "Phone", Description = "Phone")]
    Phone = 2,

    [Display(Name = "Mobile", Description = "Mobile")]
    Mobile = 3,

    [Display(Name = "Fax", Description = "Fax")]
    Fax = 4,

    [Display(Name = "Website", Description = "Website")]
    Website = 5,

    [Display(Name = "Other", Description = "Other")]
    Other = 6
}
