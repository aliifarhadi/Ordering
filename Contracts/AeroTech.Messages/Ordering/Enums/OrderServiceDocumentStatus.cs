using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderServiceDocumentStatus
    {
        [Display(Name = "Not Ready")] NotRequired = 0,
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Issued")] Issued = 2,
        [Display(Name = "Voided")] Voided = 3,
        [Display(Name = "Exchanged")] Exchanged = 4,
        [Display(Name = "Failed")] Failed = 5,
        [Display(Name = "Cancelled")] Cancelled = 6,
        [Display(Name = "Refunded")] Refunded = 7

    }
}
