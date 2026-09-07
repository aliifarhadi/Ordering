using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    // Recorded is the arrival state; Posted and Rejected are terminal. A movement is never deleted or edited —
    // a wrong one is corrected by a later movement.
    public enum WalletMovementStatus
    {
        [Display(Name = "Recorded")] Recorded = 1,
        [Display(Name = "Posted")] Posted = 2,
        [Display(Name = "Rejected")] Rejected = 3
    }
}
