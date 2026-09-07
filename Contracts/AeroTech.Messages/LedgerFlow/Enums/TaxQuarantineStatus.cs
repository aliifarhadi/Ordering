using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum TaxQuarantineStatus
    {
        [Display(Name = "Open")] Open,
        [Display(Name = "Resolved")] Resolved
    }
}
