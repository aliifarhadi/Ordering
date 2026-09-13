using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServicingEvidenceStage
    {
        [Display(Name = "Document Void")] DocumentVoid = 1,
        [Display(Name = "Document Refund")] DocumentRefund = 2,
        [Display(Name = "Refund Value")] RefundValue = 3,
        [Display(Name = "Refund Correction")] RefundCorrection = 4,
        [Display(Name = "Refund Value Correction")] RefundValueCorrection = 5,
        [Display(Name = "Reservation Release")] ReservationRelease = 6
    }
}
