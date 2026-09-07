using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FulfillmentFailureKind
    {
        [Display(Name = "Permanent")] Permanent = 1,
        [Display(Name = "Retriable")] Retriable = 2,
        [Display(Name = "Indeterminate")] Indeterminate = 3
    }
}
