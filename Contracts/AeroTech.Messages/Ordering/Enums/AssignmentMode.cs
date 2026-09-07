using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AssignmentMode
    {
        [Display(Name = "None")]  None = 0,

        [Display(Name = "Passenger Required")] PassengerRequired = 1,
        [Display(Name = "Passenger Optional")] PassengerOptional = 2,

        [Display(Name = "Group Required")] GroupRequired = 3,
        [Display(Name = "Contact Required")] ContactRequired = 4,
        [Display(Name = "Organization Required")]     OrganizationRequired = 5,
        [Display(Name = "Device Required")] DeviceRequired = 6,

        [Display(Name = "Unassigned Allowed")] UnassignedAllowed = 7,
        [Display(Name = "Assigned at Fulfillment")] AssignedAtFulfillment = 8,

        [Display(Name = "Mixed")] Mixed = 20
     }
 }
