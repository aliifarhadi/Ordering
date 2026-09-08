using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderChangeType
    {
        [Display(Name = "Create")] Create = 1,
        [Display(Name = "Add Service")] AddProduct = 2,
        [Display(Name = "Cancel")] Cancel = 3,
        [Display(Name = "Voluntary Change")] VoluntaryChange = 4,
        [Display(Name = "Exchange")] Exchange = 5,
        [Display(Name = "Reaccommodation")] Reaccommodation = 6,
        [Display(Name = "Involuntary Change")] InvoluntaryChange = 7,
        [Display(Name = "Name Correction")] NameCorrection = 8,
        [Display(Name = "Split")] Split = 9,
        [Display(Name = "Manual Adjustment")] ManualAdjustment = 10,
        [Display(Name = "Close")] Close = 11,
    }
}
