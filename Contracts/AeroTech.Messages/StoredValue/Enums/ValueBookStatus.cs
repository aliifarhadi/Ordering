using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum ValueBookStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Administratively Locked")] AdministrativelyLocked = 2,
        [Display(Name = "Closed")] Closed = 3
    }
}
