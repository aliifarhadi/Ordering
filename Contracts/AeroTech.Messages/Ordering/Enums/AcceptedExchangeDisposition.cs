using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AcceptedExchangeDisposition
    {
        [Display(Name = "Executable")] Executable = 1,
        [Display(Name = "Deferred To Expanded Exchange")] DeferredToExpandedExchange = 2,
        [Display(Name = "Rejected")] Rejected = 3,
    }
}
