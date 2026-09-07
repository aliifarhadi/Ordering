using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyConsumerConvergenceState
    {
        [Display(Name = "Unknown")] Unknown = 1,
        [Display(Name = "Converged")] Converged = 2,
        [Display(Name = "Behind")] Behind = 3,
        [Display(Name = "Incompatible")] Incompatible = 4,
        [Display(Name = "Runtime Too Old")] RuntimeTooOld = 5
    }
}
