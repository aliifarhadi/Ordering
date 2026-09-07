using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum TopUpMethod
    {
        [Display(Name = "OnlinePayment")] OnlinePayment = 1,
        [Display(Name = "BankTransfer")] BankTransfer = 2,
        [Display(Name = "Cash")] Cash = 3,
        [Display(Name = "PosTerminal")] PosTerminal = 4
    }
}
