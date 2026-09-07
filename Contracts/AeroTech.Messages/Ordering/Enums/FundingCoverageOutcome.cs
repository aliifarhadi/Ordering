using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FundingCoverageOutcome
    {
        [Display(Name = "Confirmed")]
        Confirmed = 1,

        [Display(Name = "Insufficient")]
        Insufficient = 2,

        [Display(Name = "Pending")]
        Pending = 3,

        [Display(Name = "Unknown")]
        Unknown = 4
    }
}
