using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum ConfigurationReleaseStatus
    {
        [Display(Name = "Draft")] Draft,
        [Display(Name = "Validated")] Validated,
        [Display(Name = "Published")] Published,
        [Display(Name = "Retired")] Retired
    }
}
