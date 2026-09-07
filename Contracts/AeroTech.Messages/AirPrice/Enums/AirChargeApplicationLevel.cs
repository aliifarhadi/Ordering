using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;


public enum AirChargeApplicationLevel
{
    [Display(Name = "Per Segment")] Segment = 1, // per coupon
    [Display(Name = "Per Bound")] Bound,         // per direction
    [Display(Name = "Per Journey")] Journey,     // per priced itinerary
    [Display(Name = "Per Ticket")] Ticket,       // per ticket document
    [Display(Name = "Per Booking")] Booking      // per PNR / order
}
