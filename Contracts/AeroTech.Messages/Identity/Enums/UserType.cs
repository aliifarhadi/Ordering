using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum UserType
    {
        [Display(Name = "Service Account")]
        Service = 1,
        [Display(Name = "Internal API")]
        InternalAPI = 2,
        [Display(Name = "Staff")]
        Staff = 3,
        [Display(Name = "Bussiness Employee")]
        BussinessEmployee = 4,
        [Display(Name = "Individual Customer")]
        Individual = 5,
        [Display(Name = "Bussiness API")]
        BussinessApi = 6,
    }
}
