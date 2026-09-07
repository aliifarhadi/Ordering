using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum DeliveryModel
    {
      [Display(Name = "Per Order")] PerOrder = 1,
      [Display(Name = "Per Order Item")] PerOrderItem = 2,
      [Display(Name = "Per Passenger")] PerPassenger = 3,
      [Display(Name = "Per Passenger Segment")] PerPassengerSegment = 4,
      [Display(Name = "Per Passenger Journey")] PerPassengerJourney = 5,
      [Display(Name = "Per Segment")] PerSegment = 6,
      [Display(Name = "Per Journey")] PerJourney = 7,
      [Display(Name = "Per Group")] PerGroup = 8,
      [Display(Name = "Per Unit")] PerUnit = 9,
      [Display(Name = "Per Voucher")] PerVoucher = 10,
      [Display(Name = "Per Room")] PerRoom = 11,
      [Display(Name = "Per Room Night")] PerRoomNight = 12,
      [Display(Name = "Per Stay")] PerStay = 13,
      [Display(Name = "Per Ride")] PerRide = 14,
      [Display(Name = "Per Policy")] PerPolicy = 15,
      [Display(Name = "No Fulfillment Required")] NoFulfillmentRequired = 99
    }
    }
