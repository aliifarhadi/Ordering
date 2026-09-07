using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum PostingExecutionStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Posted")] Posted = 2,
        [Display(Name = "Failed")] Failed = 3,
        [Display(Name = "Reversed")] Reversed = 4
    }
}
