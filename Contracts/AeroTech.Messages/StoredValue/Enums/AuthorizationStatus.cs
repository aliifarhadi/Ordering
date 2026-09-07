using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum AuthorizationStatus
    {
       [Display(Name = "Authorized")] Authorized = 1,
       [Display(Name = "Partially Captured")] PartiallyCaptured = 2,
       [Display(Name = "Captured")] Captured = 3,
       [Display(Name = "Released")] Released = 4,
       [Display(Name = "Expired")] Expired = 5,
       [Display(Name = "Cancelled")] Cancelled = 6
    }
}
