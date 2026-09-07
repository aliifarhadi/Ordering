using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

/// <summary>
/// For a percentage charge, the fare amount the percentage is applied to.
/// (Renamed from AirChargeBaseComponent.)
/// </summary>
public enum AirChargePercentageBase
{
    [Display(Name = "Base Fare")] BaseFare = 1,
    [Display(Name = "Base Fare Plus Surcharges")] BaseFarePlusSurcharges,
    [Display(Name = "Total Before Taxes")] TotalBeforeTaxes
}
