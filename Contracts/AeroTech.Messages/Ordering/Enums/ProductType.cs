using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ProductType
    {
        [Display(Name = "Air Fare")] AirFare = 1,
        [Display(Name = "Seat")] Seat ,
        [Display(Name = "Baggage")] Baggage ,
        [Display(Name = "Meal")] Meal ,
        [Display(Name = "Lounge")] Lounge ,
        [Display(Name = "Cip")] Cip ,
        [Display(Name = "Sim Card")] SimCard ,
        [Display(Name = "Hotel")] Hotel ,
        [Display(Name = "Transfer")] Transfer ,
        [Display(Name = "Insurance")] Insurance ,
        [Display(Name = "Penalty")] Penalty ,
        [Display(Name = "Service Fee")] ServiceFee ,
        [Display(Name = "Credit")] Credit ,
        [Display(Name = "Voucher")] Voucher ,
        [Display(Name = "Tax Adjustment")] TaxAdjustment ,
        [Display(Name = "Manual Adjustment")] ManualAdjustment 
    
    }
}