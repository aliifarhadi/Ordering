using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum BindingStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Revoked")] Revoked = 2
    }
}
