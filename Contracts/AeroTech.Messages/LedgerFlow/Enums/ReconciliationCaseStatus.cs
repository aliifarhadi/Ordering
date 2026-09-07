using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum ReconciliationCaseStatus
    {
        [Display(Name = "Open")] Open = 1,
        [Display(Name = "Under Investigation")] UnderInvestigation = 2,
        [Display(Name = "Resolved")] Resolved = 3,
        [Display(Name = "Written Off")] WrittenOff = 4
    }
}
