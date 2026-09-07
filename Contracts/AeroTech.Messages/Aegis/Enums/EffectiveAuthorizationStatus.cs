using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum EffectiveAuthorizationStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Suspended")] Suspended = 2,
        [Display(Name = "Revoked")] Revoked = 3,
        [Display(Name = "Ineligible")] Ineligible = 4
    }
}
