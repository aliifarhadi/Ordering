using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum SettlementRecoveryOutcome
    {
       [Display(Name = "Unresolved")] Unresolved = 1,
       [Display(Name = "Protected Capture")] ProtectedCapture = 2,
       [Display(Name = "Void And Release")] VoidAndRelease = 3,
       [Display(Name = "Receivable Conversion")] ReceivableConversion = 4,
       [Display(Name = "Manual Exception")] ManualException = 5
    }
}
