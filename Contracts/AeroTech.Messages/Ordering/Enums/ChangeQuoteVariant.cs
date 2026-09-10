using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ChangeQuoteVariant
    {
        [Display(Name = "Voluntary Change")] VoluntaryChange = 1,
        [Display(Name = "Exchange")] Exchange = 2,
    }
}
