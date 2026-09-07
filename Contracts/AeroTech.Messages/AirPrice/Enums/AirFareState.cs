using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum AirFareState
{
    [Display(Name = "Draft")] Draft = 1,// Fare is being prepared but not yet released.
    [Display(Name = "Released")] Released,  // Fare is officially released and available for booking.
    [Display(Name = "Suspended")] Suspended,  // Fare is temporarily unavailable.
    [Display(Name = "Revoked")] Revoked, // Fare release is canceled and no longer available.
    [Display(Name = "Archived")] Archived, // Fare is no longer active but stored for historical purposes.
}