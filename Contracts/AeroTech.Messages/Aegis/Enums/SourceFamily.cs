using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum SourceFamily
    {
        [Display(Name = "Core Eligibility")] CoreEligibility = 1,
        [Display(Name = "Core Descriptive")] CoreDescriptive = 2,
        [Display(Name = "Identity Principal")] IdentityPrincipal = 3
    }
}
