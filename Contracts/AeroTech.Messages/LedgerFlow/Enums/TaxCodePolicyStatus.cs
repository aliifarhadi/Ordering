using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum TaxCodePolicyStatus
    {
        [Display(Name = "Draft")] Draft,
        [Display(Name = "Published")] Published
    }
}
