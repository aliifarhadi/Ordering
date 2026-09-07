using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum TicketValidityType
{
    [Display(Name = "AnyTime")]AnyTime=1,
    [Display(Name = "WithinTicketValidity")] WithinTicketValidity
}