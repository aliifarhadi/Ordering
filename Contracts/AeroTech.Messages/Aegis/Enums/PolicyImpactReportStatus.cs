using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyImpactReportStatus
    {
        [Display(Name = "Pending Approval")] PendingApproval = 1,
        [Display(Name = "Approved")] Approved = 2,
        [Display(Name = "Consumed")] Consumed = 3,
        [Display(Name = "Superseded")] Superseded = 4
    }
}
