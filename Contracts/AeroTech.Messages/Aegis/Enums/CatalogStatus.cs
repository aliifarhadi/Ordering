using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum CatalogStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Active")] Active = 2,
        [Display(Name = "Deprecated")] Deprecated = 3,
        [Display(Name = "Retired")] Retired = 4
    }
}
