using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum ReceivableDirection
    {
        [Display(Name = "Debit")] Debit,
        [Display(Name = "Credit")] Credit
    }
}
