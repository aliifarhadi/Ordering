using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum StockNumberState
    {
        [Display(Name = "Reserved")]
        Reserved = 1,

        [Display(Name = "Issued")]
        Issued = 2,

        [Display(Name = "Retired")]
        Retired = 3
    }
}
