using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderChangeVariant
    {
        [Display(Name = "Add Service")] AddService = 1,
        [Display(Name = "Cancel Order Item")] CancelOrderItem = 2,
        [Display(Name = "Remove Order Services")] RemoveOrderServices = 3,
        [Display(Name = "Accept Quoted Change")] AcceptQuotedChange = 4,
        [Display(Name = "Accept Exchange")] AcceptExchange = 5,
    }
}
