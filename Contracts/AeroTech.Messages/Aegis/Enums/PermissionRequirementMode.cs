using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PermissionRequirementMode
    {
        [Display(Name = "All Of")] AllOf = 1,
        [Display(Name = "Any Of")] AnyOf = 2
    }
}
