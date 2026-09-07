using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Messages.AirPrice.IntegrationEvents;


public class AirFareReleasedEvent
{
    public string AirFareId { get; set; } = null!;
    public AirFareType AirFareType { get; set; }
    public long FareNumber { get; set; }

    public CabinClassForAirFareReleasedEvent CabinClass { get; set; } = null!;
    public BookingClassForAirFareReleasedEvent BookingClass { get; set; } = null!;
    public FareFamilyForAirFareReleasedEvent FareFamily { get; set; } = null!;
    public FeaturesForAirFareReleasedEvent Features { get; set; } = null!;
    public string FareBasis { get; set; } = null!;

    public AirFareOfferVoluntaryRefundDto? VoluntaryRefunds { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; }

}

public class CabinClassForAirFareReleasedEvent
{
    public long CabinClassId { get; set; }
    public string CabinClassCode { get; set; } = null!;
    public string CabinClass { get; set; } = null!;
}

public class BookingClassForAirFareReleasedEvent
{
    public long RbdId { get; set; }
    public string Rbd { get; set; } = null!;
    public long? ParentRbdId { get; set; }
}

public class FareFamilyForAirFareReleasedEvent
{
    public long FareFamilyId { get; set; }
    public string FareFamily { get; set; } = null!;
}

public class FeaturesForAirFareReleasedEvent
{
    public BaggageForAirFareReleasedEvent Baggage { get; set; } = null!;
    public BaggageForAirFareReleasedEvent CabinBaggage { get; set; } = null!;
    public bool IsRefundable { get; set; }
    public bool IsChangeable { get; set; }
    public bool IsSeatSelectionFree { get; set; }
    public bool HasLoungeAccess { get; set; }
    public bool IsUpgradable { get; set; }
    public int NumberOfReIssuePermitted { get; set; }
}

public class BaggageForAirFareReleasedEvent
{
    public decimal Pieces { get; set; }
    public decimal Weight { get; set; }
    public WeightUnit Unit { get; set; }
}



public class AirFareOfferVoluntaryRefundDto
{
    public bool IsRefundable { get; set; }
    public bool IsRefundableForGroups { get; set; }
    public bool NoShowPermitted { get; set; }

    public List<RefundValueForAirFareReleasedEvent> RefundRules { get; set; } = [];
    public List<RefundValueForAirFareReleasedEvent> NoShowRules { get; set; } = [];


    public bool DeathOfPassengerWaiverApply { get; set; }
    public bool IllnessOfPassengerWaiverApply { get; set; }
    public bool DeathOfImmediateFamilyWaiverApply { get; set; }
    public bool IllnessOfImmediateFamilyWaiverApply { get; set; }
}

public class RefundValueForAirFareReleasedEvent
{
    public string Id { get; set; } = null!;
    public TimeSpan FromTimeDifference { get; set; }
    public RefundTimePoint FromTimePoint { get; set; }
    public TimeSpan ToTimeDifference { get; set; }
    public RefundTimePoint ToTimePoint { get; set; }
    public decimal Value { get; set; }
    public bool IsPercentage { get; set; }
    public PassengerTypeCode[] Passengers { get; set; } = [];
}
