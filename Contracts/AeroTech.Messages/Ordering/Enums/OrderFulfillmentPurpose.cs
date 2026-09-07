using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderFulfillmentPurpose
    {
        [Display(Name = "Initial Reservation")] InitialReservation = 1,
        [Display(Name = "Initial Ticketing")] InitialTicketing = 2,
        [Display(Name = "Add Service")] AddService = 3,
        [Display(Name = "Remove Service")] RemoveService = 4,
        [Display(Name = "Rebook")] Rebook = 5,
        [Display(Name = "Reschedule")] Reschedule = 6,
        [Display(Name = "Exchange")] Exchange = 7,
        [Display(Name = "Refund")] Refund = 8,
        [Display(Name = "Cancellation")] Cancellation = 9,
        [Display(Name = "Provider Sync")] ProviderSync = 10
    }
}