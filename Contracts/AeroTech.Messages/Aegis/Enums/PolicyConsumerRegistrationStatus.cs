using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyConsumerRegistrationStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Suspended")] Suspended = 2,
        [Display(Name = "Retired")] Retired = 3
    }
}
