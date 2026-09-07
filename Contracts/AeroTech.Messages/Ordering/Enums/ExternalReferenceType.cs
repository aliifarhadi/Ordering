using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExternalReferenceType
    {
        [Display(Name = "Offer")]
        Offer = 1,

        [Display(Name = "Quote")]
        Quote = 2,

        [Display(Name = "Provider Reservation")]
        ProviderReservation = 3,

        [Display(Name = "Payment Intent")]
        PaymentIntent = 4,

        [Display(Name = "Document")]
        Document = 5,

        [Display(Name = "Other")]
        Other = 6
    }
}
