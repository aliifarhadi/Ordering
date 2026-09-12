using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AncillaryRetentionMode
    {
        [Display(Name = "Existing EMD Coupon Reusable")] ExistingEmdCouponReusable = 1,
        [Display(Name = "New Miscellaneous Document")] NewMiscellaneousDocument = 2,
        [Display(Name = "Voucher")] Voucher = 3,
        [Display(Name = "Stored Value")] StoredValue = 4,
        [Display(Name = "External Instrument")] ExternalInstrument = 5,
        [Display(Name = "Credit Shell")] CreditShell = 6,
    }
}
