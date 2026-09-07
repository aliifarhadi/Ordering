using AeroTech.Messages.FlightFlow.Enums;

namespace AeroTech.Messages.FlightFlow.IntegrationEvents;

public class CharterContractPriceQuoteBecameRunning
{
    public long CharterContractNumber { get; set; }
    public CharterContractStatus CharterContractStatus { get; set; }
    public long CharterContractCustomerId { get; set; }
    public DateOnly CharterContractDate { get; set; }
    public DateOnly CharterContractStartDate { get; set; }
    public DateOnly CharterContractStopDate { get; set; }
    public long CharterContractPriceQuoteId { get; set; }
    public int OriginAirportId { get; set; }
    public int DestinationAirportId { get; set; }
    public int CabinClassId { get; set; }
    public long RbdId { get; set; }
    public long FareFamilyId { get; set; }
    public decimal PricePerSeat { get; set; }
    public decimal PriceForExtraSeat { get; set; }
    public bool PriceForExtraSeatIsPercentage { get; set; }
    public decimal ChildPrice { get; set; }
    public int CurrencyId { get; set; }
    public int MinimumTimeLimitBeforeDeparture { get; set; }
}