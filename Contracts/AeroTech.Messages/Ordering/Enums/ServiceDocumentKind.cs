using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServiceDocumentKind
    {
        [Display(Name = "Electronic Ticket")] ElectronicTicket = 1,
        [Display(Name = "Electronic Miscellaneous Document")] ElectronicMiscDocument = 2,
        [Display(Name = "Provider Document")] ProviderDocument = 3,
    }
}
