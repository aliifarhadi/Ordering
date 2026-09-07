using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum SaleComponentType
    {
        [Display(Name = "Fare")] Fare = 1,
        [Display(Name = "Tax")] Tax = 2,
        [Display(Name = "Ancillary")] Ancillary = 3
    }
}
