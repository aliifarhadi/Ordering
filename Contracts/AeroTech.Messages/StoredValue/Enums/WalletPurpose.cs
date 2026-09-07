using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum WalletPurpose
    {
        [Display(Name = "General Cash")] GeneralCash = 1,
        [Display(Name = "Agency Prepaid")] AgencyPrepaid = 2,
        [Display(Name = "Charter Advance")] CharterAdvance = 5
    }
}
