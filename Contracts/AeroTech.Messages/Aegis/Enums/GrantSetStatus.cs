using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum GrantSetStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Suspended")] Suspended = 2,
        [Display(Name = "Revoked")] Revoked = 3
    }
}
