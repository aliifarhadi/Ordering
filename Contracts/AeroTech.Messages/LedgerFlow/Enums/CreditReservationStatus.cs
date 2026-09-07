using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum CreditReservationStatus
    {
        [Display(Name = "Active")] Active,
        [Display(Name = "Consumed")] Consumed,
        [Display(Name = "Released")] Released,
        [Display(Name = "Expired")] Expired
    }
}
