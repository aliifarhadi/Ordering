using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum PostingMode
    {
        [Display(Name = "System Only")] SystemOnly,
        [Display(Name = "Adjustment Only")] AdjustmentOnly,
        [Display(Name = "Manual Allowed")] ManualAllowed,
        [Display(Name = "Non-Posting")] NonPosting
    }
}
