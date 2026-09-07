using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum OfficeLegalEntityAssignmentStatus
{
    [Display(Name = "Scheduled", Description = "Scheduled")]
    Scheduled = 1,

    [Display(Name = "Active", Description = "Active")]
    Active = 2,

    [Display(Name = "Ended", Description = "Ended")]
    Ended = 3,

    [Display(Name = "Cancelled", Description = "Cancelled")]
    Cancelled = 4
}
