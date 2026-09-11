using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ResidualInstrumentKind
    {
        [Display(Name = "Unknown")] Unknown = 1,
        [Display(Name = "Miscellaneous Charge Order")] Mco = 2,
        [Display(Name = "Electronic Miscellaneous Document")] Emd = 3,
        [Display(Name = "Voucher")] Voucher = 4,
        [Display(Name = "Travel Credit")] TravelCredit = 5,
        [Display(Name = "Other")] Other = 6
    }
}
