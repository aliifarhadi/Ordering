using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum BoundDirection
    {
        [Display(Name = "Outbound")] Outbound = 1,
        [Display(Name = "Inbound")] Inbound = 2
    }
}