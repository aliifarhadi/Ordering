using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FulfillmentMode
    {
        [Display(Name = "Sync")] Sync = 1,
        [Display(Name = "Async")] Async = 2
    }
}
