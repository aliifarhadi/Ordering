using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum TaxLiabilityStatus
    {
        [Display(Name = "Accruing")] Accruing,
        [Display(Name = "Fully Remitted")] FullyRemitted
    }
}
