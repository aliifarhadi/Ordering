using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderFulfillmentTargetAction
    {
        [Display(Name = "Reserve")] Reserve = 1,
        [Display(Name = "Cancel")] Cancel = 2,
        [Display(Name = "Issue")] Issue = 3,
        [Display(Name = "Void")] Void = 4,
        [Display(Name = "Refund")] Refund = 5,
        [Display(Name = "Exchange")] Exchange = 6,
        [Display(Name = "Modify")] Modify = 7,
        [Display(Name = "Confirm")] Confirm = 8,
        [Display(Name = "Sync")] Sync = 9
    }
}