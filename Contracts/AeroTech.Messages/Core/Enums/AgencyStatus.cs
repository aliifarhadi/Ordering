using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum AgencyStatus
{
    [Display(Name = "Draft", Description = "Draft")]
    Draft = 1,

    [Display(Name = "Pending Approval", Description = "Pending Approval")]
    PendingApproval = 2,

    [Display(Name = "Active", Description = "Active")]
    Active = 3,

    [Display(Name = "Suspended", Description = "Suspended")]
    Suspended = 4,

    [Display(Name = "Terminated", Description = "Terminated")]
    Terminated = 5
}
