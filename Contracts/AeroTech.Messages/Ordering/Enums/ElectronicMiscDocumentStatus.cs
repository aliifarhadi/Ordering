using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ElectronicMiscDocumentStatus
    {
        [Display(Name = "Issued")] Issued = 1,
        [Display(Name = "Voided")] Voided = 2,
        [Display(Name = "Refunded")] Refunded = 3,
    }
}
