using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderPricingLineSubCategory
    {
        [Display(Name = "Base Fare")]
        BaseFare = 1,

        [Display(Name = "Tax")]
        Tax = 2,

        [Display(Name = "VAT")]
        VAT = 3,

        [Display(Name = "ServiceFee")]
        ServiceFee = 4,

        [Display(Name = "Promo")]
        Promo = 5,

    }
}
