using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeFundingState
    {
        [Display(Name = "Not Required")] NotRequired = 1,
        [Display(Name = "Guarantee Required")] GuaranteeRequired = 2,
        [Display(Name = "Guarantee Pending")] GuaranteePending = 3,
        [Display(Name = "Guaranteed")] Guaranteed = 4,
        [Display(Name = "Guarantee Rejected")] GuaranteeRejected = 5,
        [Display(Name = "Capture Pending")] CapturePending = 6,
        [Display(Name = "Captured")] Captured = 7,
        [Display(Name = "Capture Rejected")] CaptureRejected = 8,
        [Display(Name = "Release Pending")] ReleasePending = 9,
        [Display(Name = "Released")] Released = 10
    }
}
