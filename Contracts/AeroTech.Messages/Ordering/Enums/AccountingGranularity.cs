using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AccountingGranularity
    {
        [Display(Name = "Order")] Order = 1,
        [Display(Name = "Order Item")] OrderItem = 2,
        [Display(Name = "Service")] Service = 3,
        [Display(Name = "Service Unit")] ServiceUnit = 4,

        [Display(Name = "Passenger")] Passenger = 10,
        [Display(Name = "Passenger Segment")] PassengerSegment = 11,
        [Display(Name = "Passenger Journey")] PassengerJourney = 12,

        [Display(Name = "Segment")] Segment = 20,
        [Display(Name = "Journey")] Journey = 21,

        [Display(Name = "Voucher")] Voucher = 30,
        [Display(Name = "Unit")] Unit = 31,
        [Display(Name = "Room")] Room = 32,
        [Display(Name = "Room Night")] RoomNight = 33,
        [Display(Name = "Stay")] Stay = 34,
        [Display(Name = "Ride")] Ride = 35,
        [Display(Name = "Policy")] Policy = 36,

        [Display(Name = "None")] None = 99
    }
    }
