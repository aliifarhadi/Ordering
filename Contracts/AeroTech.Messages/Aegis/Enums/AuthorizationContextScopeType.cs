using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum AuthorizationContextScopeType
    {
        [Display(Name = "Travel Agency")] TravelAgency = 1,
        [Display(Name = "Travel Agency Office")] TravelAgencyOffice = 2,
        [Display(Name = "Travel Agency User")] TravelAgencyUser = 3,
        [Display(Name = "Airline Office")] AirlineOffice = 4,
        [Display(Name = "Airline User")] AirlineUser = 5,
        [Display(Name = "Individual")] Individual = 6,

        [Obsolete("No longer written. A customer fact is recorded under its subject scope - TravelAgency, Individual or Organization - keyed by SubjectId, so that the writer and the reconciler produce the same key.")]
        [Display(Name = "Customer")] Customer = 7,

        [Display(Name = "Partner Api Access Profile")] PartnerApiAccessProfile = 8,
        [Display(Name = "Organization")] Organization = 9
    }
}
