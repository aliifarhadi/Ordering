using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum DirectServiceEnforcementMode
    {
        [Display(Name = "Gateway Only")] GatewayOnly = 1,
        [Display(Name = "Direct PEP Required")] DirectPepRequired = 2,
        [Display(Name = "Either PEP")] EitherPep = 3
    }
}
