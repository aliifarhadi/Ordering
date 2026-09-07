using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum RouteCoverageState
    {
        [Display(Name = "Mapped Permissioned")] MappedPermissioned = 1,
        [Display(Name = "Mapped Authenticated")] MappedAuthenticated = 2,
        [Display(Name = "Mapped Anonymous")] MappedAnonymous = 3,
        [Display(Name = "Unclassified")] Unclassified = 4,
        [Display(Name = "Stale")] Stale = 5
    }
}
