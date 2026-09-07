using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum FlightApplicationDirection
{
    [Display(Name = "Outbound Only")] OutboundOnly =1,
    [Display(Name = "Inbound Only")] InboundOnly,
    //[Display(Name = "Either Not Both")]EitherNotBoth=3,
    [Display(Name = "Both")] Both,
}