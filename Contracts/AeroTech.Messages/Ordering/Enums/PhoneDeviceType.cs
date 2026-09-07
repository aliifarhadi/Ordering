using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PhoneDeviceType
    {
        [Display(Name = "SMS")]
        SMS = 1,

        [Display(Name = "WhatApp")]
        WAP ,

        [Display(Name = "Call")]
        CAL,

        [Display(Name = "Telegram")]
        TLG 

    }
}
