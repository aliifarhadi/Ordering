using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum AccountingDispatchStatus
    {
       [Display(Name = "Pending")] Pending = 1,
       [Display(Name = "Published")] Published = 2,
       [Display(Name = "Accepted")] Accepted = 3,
       [Display(Name = "Posted")] Posted = 4,
       [Display(Name = "Rejected")] Rejected = 5,
       [Display(Name = "Quarantined")] Quarantined = 6
    }
}
