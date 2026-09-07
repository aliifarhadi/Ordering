using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum EnforcementRuntimeMode
    {
        [Display(Name = "Monitor")] Monitor = 1,
        [Display(Name = "Enforce")] Enforce = 2
    }
}
