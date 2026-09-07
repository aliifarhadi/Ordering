using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum SettlementRecoveryStatus
    {
        [Display(Name = "Open")] Open = 1,
        [Display(Name = "Resolved")] Resolved = 2
    }
}
