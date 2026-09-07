using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum IssuerSystem
    {
        [Display(Name = "ETBA", Description = "By Airline")]
        ElectronicTicketingByAirline = 1,

        [Display(Name = "ETDN", Description = "By Direct Number")]
        ElectronicTicketByDirectNumber = 2,

        [Display(Name = "ETAG", Description = "By Agent")]
        ElectronicTicketByAgent = 3,

        [Display(Name = "ETAF", Description = "By Airline to Travel Agent")]
        ElectronicTicketByAirlineTotravelAgent = 4,

        [Display(Name = "ETKL", Description = "Electronic Ticket List")]
        ElectronicTicketList = 5
    }
}
