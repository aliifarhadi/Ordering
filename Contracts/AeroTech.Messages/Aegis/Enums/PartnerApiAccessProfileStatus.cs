using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PartnerApiAccessProfileStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Active")] Active = 2,
        [Display(Name = "Suspended")] Suspended = 3,
        [Display(Name = "Revoked")] Revoked = 4
    }
}
