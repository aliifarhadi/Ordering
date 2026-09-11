using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeAncillaryState
    {
        [Display(Name = "Not Required")] NotRequired = 1,
        [Display(Name = "Not Started")] NotStarted = 2,
        [Display(Name = "Pending")] Pending = 3,
        [Display(Name = "Confirmed")] Confirmed = 4,
        [Display(Name = "Rejected")] Rejected = 5,
        [Display(Name = "Needs Reconciliation")] NeedsReconciliation = 6
    }
}
