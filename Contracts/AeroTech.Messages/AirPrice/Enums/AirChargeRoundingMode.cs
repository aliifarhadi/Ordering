using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

/// <summary>
/// ATPCO-style rounding DIRECTION applied to the computed charge amount. The number
/// of decimals and the rounding step come from the target currency
/// (Currency.DecimalPlaces / Currency.RoundingFactor), not the charge.
/// </summary>
public enum AirChargeRoundingMode
{
    [Display(Name = "None")] None = 1,
    [Display(Name = "Round Up")] Up,
    [Display(Name = "Round Down")] Down,
    [Display(Name = "Nearest")] Nearest
}
