using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum DiscountType
{
    [Display(Name = "Percentage")] Percentage =1,
    [Display(Name = "Fixed")] Fixed,
    [Display(Name = "Discount")] Discount
}