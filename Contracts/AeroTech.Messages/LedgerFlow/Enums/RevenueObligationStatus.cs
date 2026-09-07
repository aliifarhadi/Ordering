using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum RevenueObligationStatus
    {
        [Display(Name = "Deferred")] Deferred,
        [Display(Name = "Partially Recognized")] PartiallyRecognized,
        [Display(Name = "Recognized")] Recognized,
        [Display(Name = "Partially Refunded")] PartiallyRefunded,
        [Display(Name = "Refunded")] Refunded,
        [Display(Name = "Expired")] Expired,
        [Display(Name = "Forfeited")] Forfeited,
        [Display(Name = "Exchanged")] Exchanged,
        [Display(Name = "Cancelled")] Cancelled
    }
}
