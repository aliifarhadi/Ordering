using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum IssuanceProtectionStatus
    {
        [Display(Name = "None")] None = 1,
        [Display(Name = "Committed")] Committed = 2,
        [Display(Name = "Cleared")] Cleared = 3
    }
}
