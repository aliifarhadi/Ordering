using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum OrderSettlementMode
    {
        [Display(Name = "Prepaid")] Prepaid,
        [Display(Name = "On Account")] OnAccount
    }
}
