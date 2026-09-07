using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PrincipalType
    {
        [Display(Name = "Human")] Human = 1,
        [Display(Name = "Travel Agency API")] TravelAgencyApi = 2,
        [Display(Name = "Internal Service")] InternalService = 3,
        [Display(Name = "Platform Automation")] PlatformAutomation = 4
    }
}
