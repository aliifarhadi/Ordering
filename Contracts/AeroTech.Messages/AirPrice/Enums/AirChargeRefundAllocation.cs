using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;


public enum AirChargeRefundAllocation
{
    
    [Display(Name = "Auto")] Auto = 1,

    
    [Display(Name = "Point Anchored")] PointAnchored,

   
    [Display(Name = "Prorate")] Prorate,

    
    [Display(Name = "Whole Unit")] WholeUnit
}
