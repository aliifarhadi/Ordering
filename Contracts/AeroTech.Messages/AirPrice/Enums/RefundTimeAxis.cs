using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum RefundTimeAxis
{
    [Display(Name = "Reservation")] Reservation = 1,
    [Display(Name = "After Reservation")] AfterReservation,
    [Display(Name = "Before Departure")] BeforeDeparture,
    [Display(Name = "Departure")] Departure,
    [Display(Name = "After Departure")] AfterDeparture,

}