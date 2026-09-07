using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum BusinessContextType
    {
        [Display(Name = "Airline")] Airline = 1,
        [Display(Name = "Travel Agency")] TravelAgency = 2,
        [Display(Name = "Individual")] Individual = 3,
        [Display(Name = "Partner API")] PartnerApi = 4,
        [Display(Name = "Service")] Service = 5,
        [Display(Name = "Global")] Global = 6
    }
}
