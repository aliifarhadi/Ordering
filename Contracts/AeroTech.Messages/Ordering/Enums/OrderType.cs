using System.ComponentModel.DataAnnotations;


namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderType
    {
        [Display(Name = "Normal")]
        Normal = 1,
        [Display(Name = "Charter")]
        Charter = 2,
        [Display(Name = "Mixed")]
        Mixed = 2,
    }
}