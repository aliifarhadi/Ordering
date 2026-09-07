using System.ComponentModel.DataAnnotations;
namespace AeroTech.Messages.StoredValue.Enums
{
    public enum PostingDirection
    {
        [Display(Name = "Debit")] Debit = 1,
        [Display(Name = "Credit")] Credit = 2
    }
}
