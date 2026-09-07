using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    // Unified status for all traffic documents (tickets and EMDs).
    public enum TrafficDocumentStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Reserved")] Reserved = 2,
        [Display(Name = "Issued")] Issued = 3,
        [Display(Name = "Refunded")] Refunded = 4,
        [Display(Name = "Voided")] Voided = 5,
        [Display(Name = "Exchanged")] Exchanged = 6,
        [Display(Name = "Timeout")] Timeout = 7,
        [Display(Name = "Irregular")] Irregular = 8,
        [Display(Name = "Flown")] Flown = 9,
        [Display(Name = "Suspended")] Suspended = 10,
        [Display(Name = "On Hold")] OnHold = 11,
        [Display(Name = "Void Unconfirmed")] VoidUnconfirmed = 12,
        [Display(Name = "Cancelled")] Cancelled = 13,
        [Display(Name = "Cancel Unconfirmed")] CancelUnconfirmed = 14
    }
}
