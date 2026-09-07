using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum ProjectionEntityStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Inactive")] Inactive = 2,
        [Display(Name = "Removed")] Removed = 3
    }
}
