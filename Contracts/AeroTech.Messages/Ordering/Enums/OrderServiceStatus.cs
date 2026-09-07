using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderServiceStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Active")] Active = 2,
        [Display(Name = "Partially Active")] PartiallyActive = 3,
        [Display(Name = "Fulfillment Pending")] FulfillmentPending = 4,
        [Display(Name = "Fulfilled")] Fulfilled = 5,
        [Display(Name = "Partially Fulfilled")] PartiallyFulfilled = 6,
        [Display(Name = "Cancelled")] Cancelled = 7,
        [Display(Name = "Partially Cancelled")] PartiallyCancelled = 8,
        [Display(Name = "Failed")] Failed = 9

    }

}
