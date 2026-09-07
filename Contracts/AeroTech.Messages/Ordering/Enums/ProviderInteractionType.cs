using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ProviderInteractionType
    {
        [Display(Name = "Create Hold")] CreateHold = 1,
        [Display(Name = "Release Hold")] ReleaseHold = 2,
        [Display(Name = "Confirm Hold")] ConfirmHold = 3,
        [Display(Name = "Cancel Confirmed")] CancelConfirmed = 4,
        [Display(Name = "Reverse Confirmed")] ReverseConfirmed = 5,
        [Display(Name = "Extend Hold")] ExtendHold = 6,
        [Display(Name = "Issue Ticket")] IssueTicket = 7,
        [Display(Name = "Issue Emd")] IssueEmd = 8
    }
}
