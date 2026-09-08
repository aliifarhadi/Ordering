using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingEffect
    {
        [Display(Name = "Customer Balance")] CustomerBalance = 1,
        [Display(Name = "Settlement Only")] SettlementOnly = 2,
        [Display(Name = "Informational")] Informational = 3,
    }
}
