using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum SegmentKind
    {
        [Display(Name = "Scheduled Air")]
        ScheduledAir = 1,

        [Display(Name = "Open Air")]
        OpenAir = 2,

        [Display(Name = "Surface")]
        Surface = 3
    }
}
