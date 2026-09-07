using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightDisruptionReason
{

    [Display(Name = "Schedule-Based")] ScheduleBased=1,
    [Display(Name = "Operational")] Operational,
    [Display(Name = "Technical")] Technical,
    [Display(Name = "Weather")] Weather,
    [Display(Name = "Airport-Closure")] AirportClosure,
    [Display(Name = "Security-Incident")] SecurityIncident,
    [Display(Name = "Natural-Disaster")] NaturalDisaster,
    [Display(Name = "Political-Restrictions")] PoliticalRestrictions
}