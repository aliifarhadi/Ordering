using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderItemUnitOfMeasure
    {
        [Display(Name = "Each")] Each = 1,
        [Display(Name = "Passenger Fare")] PassengerFare = 2,
    }
}
