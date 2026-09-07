using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderFulfillmentTaskType
    {
        [Display(Name = "Reserve")] ReserveInventory = 1,
        [Display(Name = "Release Reserved")] ReleaseReserved = 2,
        [Display(Name = "Confirm")] ConfirmInventory = 3,
        [Display(Name = "Cancel Confirmed")] CancelConfirmed = 4,

        [Display(Name = "Issue Ticket")] IssueTicket = 5,
        [Display(Name = "Issue Emd")] IssueEmd = 6,         
        [Display(Name = "Assign Seat")] AssignSeat = 7,    
        [Display(Name = "Confirm Cip Voucher")] ConfirmCipVoucher = 8,  
        [Display(Name = "Issue ESim")] IssueESim = 9,  
        [Display(Name = "Book Hotel")] BookHotel = 10,    

    }

}
