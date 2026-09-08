using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ElectronicMiscDocumentType
    {
        [Display(Name = "EMD-A (Associated)")] Associated = 1,
        [Display(Name = "EMD-S (Standalone)")] Standalone = 2,
    }
}
