using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderServiceType
    {
        [Display(Name = "Air Transportation")] AirTransportation = 1,
        [Display(Name = "Seat Assignment")] SeatAssignment,
        [Display(Name = "Baggage Allowance")] BaggageAllowance,
        [Display(Name = "Meal")] Meal,
        [Display(Name = "Lounge Access")] LoungeAccess,
        [Display(Name = "Cip")] Cip,
        [Display(Name = "Sim Card")] SimCard,
        [Display(Name = "Hotel Stay")] HotelStay,
        [Display(Name = "Transfer Ride")] TransferRide,
        [Display(Name = "Insurance Policy")] InsurancePolicy,
        [Display(Name = "Penalty")] Penalty,
        [Display(Name = "Service Fee")] ServiceFee,
        [Display(Name = "Credit")] Credit,
        [Display(Name = "Voucher")] Voucher,
        [Display(Name = "Tax Adjustment")] TaxAdjustment,
        [Display(Name = "Manual Adjustment")] ManualAdjustment,
        [Display(Name = "Notification")] Notification,
        [Display(Name = "Other")] Other,
        [Display(Name = "Ground Transport")] GroundTransport,
        [Display(Name = "Priority")] Priority,
        [Display(Name = "Wi-Fi")] WiFi,
        [Display(Name = "Extra Seat")] ExtraSeat

    }

}
