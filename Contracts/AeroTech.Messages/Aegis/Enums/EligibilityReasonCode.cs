using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum EligibilityReasonCode
    {
        [Display(Name = "Role Limit Exceeded")] RoleLimitExceeded = 1,
        [Display(Name = "Grantee Inactive")] GranteeInactive = 2,
        [Display(Name = "Context Inactive")] ContextInactive = 3,
        [Display(Name = "Office Membership Missing")] OfficeMembershipMissing = 4,
        [Display(Name = "No Effective Roles")] NoEffectiveRoles = 5,
        [Display(Name = "Source Hard Stale")] SourceHardStale = 6,
        [Display(Name = "Grant Revoked")] GrantRevoked = 7,
        [Display(Name = "Grant Suspended")] GrantSuspended = 8
    }
}
