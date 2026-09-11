using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeFundingReleaseReason
    {
        [Display(Name = "Reservation Rejected")] ReservationRejected = 1,
        [Display(Name = "Document Rejected")] DocumentRejected = 2,
        [Display(Name = "Document Denied")] DocumentDenied = 3
    }
}
