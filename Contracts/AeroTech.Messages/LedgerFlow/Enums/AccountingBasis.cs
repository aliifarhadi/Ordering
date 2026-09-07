using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountingBasis
    {
        [Display(Name = "Accrual")] Accrual = 1,
        [Display(Name = "Cash")] Cash = 2
    }
}
