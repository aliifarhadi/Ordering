using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{ 
    public enum OrderPricingLineCategory 
    {
        [Display(Name = "FARE")]
        Fare = 1,

        [Display(Name = "TAX")]
        Tax = 2,

        [Display(Name = "FEE")]
        Fee = 3,

        [Display(Name = "PENALTY")]
        Penalty = 4,

        [Display(Name = "DISCOUNT")]
        Discount = 5,      
      
        [Display(Name = "CHARGE")]
        Charge = 6,

        [Display(Name = "CARRIER IMPOSED SURCHARGE")]
        CarrierImposedSurcharge = 7,

        [Display(Name = "ANCILLARY")]
        Ancillary= 8,

        [Display(Name = "COMMISSION")]
        Commission = 9,

        [Display(Name = "ROUNDING")]
        Rounding= 100,



    }
}
