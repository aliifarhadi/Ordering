using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum LifecycleIntentType
    {
        [Display(Name = "Freeze")] Freeze = 1,
        [Display(Name = "Suspend")] Suspend = 2,
        [Display(Name = "Close")] Close = 3,
        [Display(Name = "Policy Change")] PolicyChange = 4
    }
}
