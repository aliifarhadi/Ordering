using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ProviderOperationOutcome
    {
        [Display(Name = "Confirmed")]
        Confirmed = 1,

        [Display(Name = "Rejected")]
        Rejected = 2,

        [Display(Name = "Pending")]
        Pending = 3,

        [Display(Name = "Unknown")]
        Unknown = 4
    }
}
