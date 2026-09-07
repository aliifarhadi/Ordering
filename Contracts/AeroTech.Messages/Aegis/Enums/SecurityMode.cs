using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum SecurityMode
    {
        [Display(Name = "Anonymous")] Anonymous = 1,
        [Display(Name = "Authenticated")] Authenticated = 2,
        [Display(Name = "Permissioned")] Permissioned = 3
    }
}
