using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum AuthorizationContextStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Blocked")] Blocked = 2
    }
}
