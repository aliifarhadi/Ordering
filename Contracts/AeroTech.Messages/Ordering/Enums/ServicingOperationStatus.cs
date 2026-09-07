using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServicingOperationStatus
    {
        [Display(Name = "PREPARED")]
        Prepared = 1,

        [Display(Name = "EXECUTING")]
        Executing = 2,

        [Display(Name = "AWAITING EXTERNAL")]
        AwaitingExternal = 3,

        [Display(Name = "READY TO FINALIZE")]
        ReadyToFinalize = 4,

        [Display(Name = "COMMITTED")]
        Committed = 5,

        [Display(Name = "COMPLETED")]
        Completed = 6,

        [Display(Name = "REJECTED")]
        Rejected = 7,

        [Display(Name = "COMPENSATING")]
        Compensating = 8,

        [Display(Name = "NEEDS RECONCILIATION")]
        NeedsReconciliation = 9
    }
}
