using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum RecognitionTiming
    {
        [Display(Name = "On Fulfillment")] OnFulfillment,
        [Display(Name = "At Sale")] AtSale
    }
}
