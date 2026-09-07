using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyHighRiskChange
    {
        [Display(Name = "Permission Removed From Role")] PermissionRemovedFromRole = 1,
        [Display(Name = "System Role Changed")] SystemRoleChanged = 2,
        [Display(Name = "Operation Security Mode Changed")] OperationSecurityModeChanged = 3,
        [Display(Name = "Operation Permission Removed")] OperationPermissionRemoved = 4,
        [Display(Name = "Role Removed From Policy")] RoleRemovedFromPolicy = 5,
        [Display(Name = "Authority Loss Above Threshold")] AuthorityLossAboveThreshold = 6
    }
}
