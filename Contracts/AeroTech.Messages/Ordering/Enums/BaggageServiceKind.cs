using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum BaggageServiceKind
    {
        [Display(Name = "Allowance")] Allowance = 1,
        [Display(Name = "Prepaid Piece")] PrepaidPiece = 2,
        [Display(Name = "Excess Weight")] ExcessWeight = 3,
        [Display(Name = "Special Baggage")] SpecialBaggage = 4,
    }
}
