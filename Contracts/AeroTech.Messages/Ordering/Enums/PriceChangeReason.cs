using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PriceChangeReason
    {
        [Display(Name = "Original Sale")] OriginalSale = 1,
        [Display(Name = "Add Service")] AddProduct = 2,
        [Display(Name = "Reprice")] Reprice = 3,
        [Display(Name = "Voluntary Change")] VoluntaryChange = 4,
        [Display(Name = "Exchange")] Exchange = 5,
        [Display(Name = "Involuntary Change")] InvoluntaryChange = 6,
        [Display(Name = "Cancellation")] Cancellation = 7,
        [Display(Name = "Refund")] Refund = 8,
        [Display(Name = "Void")] Void = 9,
        [Display(Name = "Manual Adjustment")] ManualAdjustment = 10,
        [Display(Name = "Correction")] Correction = 11,
        [Display(Name = "Split Transfer")] SplitTransfer = 12,
    }
}
