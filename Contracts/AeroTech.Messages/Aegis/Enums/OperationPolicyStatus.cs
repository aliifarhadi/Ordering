using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum OperationPolicyStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Active")] Active = 2,
        [Display(Name = "Revoked")] Revoked = 3
    }
}
