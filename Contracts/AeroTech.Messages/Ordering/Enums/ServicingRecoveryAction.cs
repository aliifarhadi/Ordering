using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServicingRecoveryAction
    {
        [Display(Name = "None Required")] NoneRequired = 1,
        [Display(Name = "Replay Command")] ReplayCommand = 2,
        [Display(Name = "Manual Resolution Required")] ManualResolutionRequired = 3
    }
}
