using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderItemFulfillmentStatus
    {
        [Display(Name = "Not Fulfilled")] NotFulfilled = 1,
        [Display(Name = "Partially Fulfilled")] PartiallyFulfilled = 2,
        [Display(Name = "Fully Fulfilled")] FullyFulfilled = 3,
    }
    }
