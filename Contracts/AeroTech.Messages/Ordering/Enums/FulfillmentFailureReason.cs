using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FulfillmentFailureReason
    {
        [Display(Name = "Business Rejected")] BusinessRejected = 1,
        [Display(Name = "Technical Failed")] TechnicalFailed = 2,
        [Display(Name = "Unknown Outcome")] UnknownOutcome = 3,
        [Display(Name = "Provider Rejected")] ProviderRejected = 4,
        [Display(Name = "Validation Failed")] ValidationFailed = 5,
        [Display(Name = "Hold Expired")] HoldExpired = 6
    }
}
