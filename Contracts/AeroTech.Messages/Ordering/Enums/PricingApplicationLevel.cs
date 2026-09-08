using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingApplicationLevel
    {
        [Display(Name = "Per Order")] PerOrder = 1,
        [Display(Name = "Per Traveler")] PerTraveler = 2,
        [Display(Name = "Per Segment")] PerSegment = 3,
        [Display(Name = "Per Bound")] PerBound = 4,
        [Display(Name = "Per Journey")] PerJourney = 5,
        [Display(Name = "Per Pricing Unit")] PerPricingUnit = 6,
        [Display(Name = "Per Service")] PerService = 7,
        [Display(Name = "Per Piece")] PerPiece = 8,
        [Display(Name = "Per Weight")] PerWeight = 9,
        [Display(Name = "Per Room")] PerRoom = 10,
        [Display(Name = "Per Night")] PerNight = 11,
        [Display(Name = "Per Room Night")] PerRoomNight = 12,
        [Display(Name = "Per Guest Night")] PerGuestNight = 13,
        [Display(Name = "Per Direction")] PerDirection = 14,
        [Display(Name = "Per Round Trip")] PerRoundTrip = 15,
        [Display(Name = "Per Document")] PerDocument = 16,
        [Display(Name = "Provider Defined")] ProviderDefined = 17,
    }
}
