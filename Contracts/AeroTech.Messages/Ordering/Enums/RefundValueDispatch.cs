using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum RefundValueDispatch
    {
        [Display(Name = "First Attempt")] FirstAttempt = 1,
        [Display(Name = "After Recovery")] AfterRecovery = 2,
    }
}
