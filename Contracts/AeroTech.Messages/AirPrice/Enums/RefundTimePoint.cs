using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum RefundTimePoint
{
    [Display(Name = "Reservation")] Reservation =1,
    [Display(Name = "Departure")] Departure
}