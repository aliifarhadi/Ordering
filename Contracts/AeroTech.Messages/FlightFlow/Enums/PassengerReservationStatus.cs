using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum PassengerReservationStatus
{
    [Display(Name = "NoSeat")] NoSeat = 1,
    
    [Display(Name = "HK")] HoldConfirm, //رزرو 

    [Display(Name = "HX")] Canceled, //کنسل سیت

    [Display(Name = "TK")] Ticketed,// ایشو

    [Display(Name = "KK")] OverBooked,  //اور بوک، فعلا نداریم

    [Display(Name = "SA")] Standby,//فعلا نداریم

    [Display(Name = "UN")] UnConfirmed, // کنسلی کل پرواز
}