using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum WalletReconciliationRunStatus
    {
        [Display(Name = "Completed")] Completed = 1,
        [Display(Name = "Completed With Differences")] CompletedWithDifferences = 2,
        [Display(Name = "Superseded")] Superseded = 3
    }
}
