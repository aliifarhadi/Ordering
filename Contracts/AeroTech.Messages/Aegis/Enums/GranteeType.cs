using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum GranteeType
    {
        [Display(Name = "Airline User")] AirlineUser = 1,
        [Display(Name = "Travel Agency User")] TravelAgencyUser = 2,
        [Display(Name = "Individual")] Individual = 3,
        [Display(Name = "Identity Subject")] IdentitySubject = 4,
        [Display(Name = "Partner API Access Profile")] PartnerApiAccessProfile = 5,
        [Display(Name = "Service Principal")] ServicePrincipal = 6
    }
}
