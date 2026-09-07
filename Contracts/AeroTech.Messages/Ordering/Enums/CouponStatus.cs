using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    // IATA-style coupon statuses. Legacy assigned Cancel and Voided the same numeric value (8);
    // these are given distinct values here.
    public enum CouponStatus
    {
        [Display(Name = "N", Description = "None")] None = 1,
        [Display(Name = "O", Description = "Open For Use")] OpenForUse = 2,
        [Display(Name = "B", Description = "Boarded")] Boarded = 3,
        [Display(Name = "F", Description = "Flown")] Flown = 4,
        [Display(Name = "S", Description = "Suspended")] Suspended = 5,
        [Display(Name = "M", Description = "Manifest")] Manifest = 6,
        [Display(Name = "R", Description = "Refunded")] Refunded = 7,
        [Display(Name = "C", Description = "Cancel")] Cancel = 8,
        [Display(Name = "V", Description = "Voided")] Voided = 9,
        [Display(Name = "E", Description = "Exchanged")] Exchanged = 10,
        [Display(Name = "X", Description = "Exchanged/Airport Control")] ExchangedAirportControl = 11,
        [Display(Name = "A", Description = "Airport Control")] AirportControl = 12,
        [Display(Name = "P", Description = "Printed")] Printed = 13,
        [Display(Name = "U", Description = "Used")] Used = 14,
        [Display(Name = "I", Description = "Checked In")] CheckedIn = 15,
        [Display(Name = "Z", Description = "Closed")] Closed = 100
    }
}
