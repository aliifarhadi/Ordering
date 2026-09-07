using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightCapacityAllotmentType
{
    [Display(Name = "PointOfSale")] PointOfSale = 1,
    [Display(Name = "Charter")] Charter = 2
}