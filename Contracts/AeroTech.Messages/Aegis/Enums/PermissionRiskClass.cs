using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PermissionRiskClass
    {
        [Display(Name = "Standard")] Standard = 1,
        [Display(Name = "Elevated")] Elevated = 2,
        [Display(Name = "Critical")] Critical = 3
    }
}
