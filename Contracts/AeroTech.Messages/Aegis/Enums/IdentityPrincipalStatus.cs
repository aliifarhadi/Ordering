using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum IdentityPrincipalStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Disabled")] Disabled = 2,
        [Display(Name = "Closed")] Closed = 3
    }
}
