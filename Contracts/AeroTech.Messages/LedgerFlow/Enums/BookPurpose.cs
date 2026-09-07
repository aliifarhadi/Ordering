using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum BookPurpose
    {
        [Display(Name = "IFRS Statutory")] IfrsStatutory = 1,
        [Display(Name = "Local Statutory")] LocalStatutory = 2,
        [Display(Name = "Tax")] Tax = 3,
        [Display(Name = "Management")] Management = 4
    }
}
