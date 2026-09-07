using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum CreditFacilityStatus
    {
        [Display(Name = "Proposed")] Proposed,
        [Display(Name = "Active")] Active,
        [Display(Name = "Suspended")] Suspended,
        [Display(Name = "Expired")] Expired,
        [Display(Name = "Closed")] Closed
    }
}
