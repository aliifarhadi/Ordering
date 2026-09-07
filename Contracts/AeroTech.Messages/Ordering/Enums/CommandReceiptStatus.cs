using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum CommandReceiptStatus
    {
        [Display(Name = "PENDING")]
        Pending = 1,

        [Display(Name = "COMPLETED")]
        Completed = 2,

        [Display(Name = "UNKNOWN")]
        Unknown = 3,

        [Display(Name = "REJECTED")]
        Rejected = 4,

        [Display(Name = "NEEDS RECONCILIATION")]
        NeedsReconciliation = 5
    }
}
