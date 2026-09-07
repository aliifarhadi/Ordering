using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountMappingVersionStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Published")] Published = 2,
        [Display(Name = "Retired")] Retired = 3
    }
}
