using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AncillaryCancellationDocumentAction
    {
        [Display(Name = "Void Without Refund")] VoidWithoutRefund = 1
    }
}
