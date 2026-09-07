using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum CreditKind
    {
        [Display(Name = "Refund")] Refund = 1,
        [Display(Name = "Goodwill")] Goodwill = 2,
        [Display(Name = "Promotion")] Promotion = 3,
        [Display(Name = "Compensation")] Compensation = 4,
        [Display(Name = "TopUp")] TopUp = 5
    }
}
