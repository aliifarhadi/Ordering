using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServicingOperationKind
    {
        [Display(Name = "CREATE ORDER")]
        CreateOrder = 1,

        [Display(Name = "RESERVE")]
        Reserve = 2,

        [Display(Name = "REQUEST PAYMENT")]
        RequestPayment = 3,

        [Display(Name = "ISSUE")]
        Issue = 4,

        [Display(Name = "CANCEL")]
        Cancel = 5,

        [Display(Name = "VOID DOCUMENT")]
        VoidDocument = 6,

        [Display(Name = "SPLIT")]
        Split = 7,

        [Display(Name = "EXPIRE")]
        Expire = 8,

        [Display(Name = "ADD PRODUCT")]
        AddProduct = 9
    }
}
