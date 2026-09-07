namespace AeroTech.Messages.AirPrice.IntegrationEvents;

public record PointOfSaleDefined(
    string EventId,
    string AggregateId,
    string Title,
    long Number,
    DateTimeOffset TimeOfOccurrence)
{
   


};

