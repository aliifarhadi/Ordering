using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum RevenueMovementType
    {
        [Display(Name = "Recognition")] Recognition,
        [Display(Name = "Refund")] Refund,
        [Display(Name = "Breakage")] Breakage,
        [Display(Name = "Forfeiture")] Forfeiture,
        [Display(Name = "Transfer Out")] TransferOut,
        [Display(Name = "Transfer In")] TransferIn,

        // The order was cancelled and the obligation never came into effect. Distinct from Refund, which
        // relieves an obligation the customer is being paid back for.
        [Display(Name = "Cancellation")] Cancellation
    }
}
