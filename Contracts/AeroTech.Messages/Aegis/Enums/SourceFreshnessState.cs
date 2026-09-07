using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum SourceFreshnessState
    {
        [Display(Name = "Fresh")] Fresh = 1,
        [Display(Name = "Soft Stale")] SoftStale = 2,
        [Display(Name = "Hard Stale")] HardStale = 3,
        [Display(Name = "Uninitialized")] Uninitialized = 4
    }
}
