using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Shared.Enums
{
    public enum SalesChannel
    {
        [Display(Name = "Back office")] BackOffice = 1,

        [Display(Name = "Internet Booking Engine")] IBE = 2,

        [Display(Name = "Partner API")] PartnerAPI = 3,

        [Display(Name = "Agency Panel")] AgencyPanel = 4,

        [Display(Name = "GDS")] GDS = 5,
        
        [Display(Name = "System")] System = 99,
    }
}
