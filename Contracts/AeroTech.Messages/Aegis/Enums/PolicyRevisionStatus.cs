using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyRevisionStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Published")] Published = 2,
        [Display(Name = "Superseded")] Superseded = 3,
        [Display(Name = "Rolled Back")] RolledBack = 4
    }
}
