using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum CustomerType
{
    [Display(Name = "Individual")]
    Individual = 1,

    [Display(Name = "TravelAgency")]
    TravelAgency = 2,

    [Display(Name = "Organization")]
    Organization = 3
}