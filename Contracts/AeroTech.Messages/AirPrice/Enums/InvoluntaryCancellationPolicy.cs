using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum InvoluntaryCancellationPolicy
{
    [Display(Name = "Segment")] Segment =1,
    [Display(Name = "Itinerary")] Itinerary = 2
}