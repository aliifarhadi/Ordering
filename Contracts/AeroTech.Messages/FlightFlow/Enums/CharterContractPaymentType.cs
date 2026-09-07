using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum CharterContractPaymentType
{
    [Display(Name = "Pre Paid")] PrePaid = 1,
    [Display(Name = "Post Paid")] PostPaid = 2
}