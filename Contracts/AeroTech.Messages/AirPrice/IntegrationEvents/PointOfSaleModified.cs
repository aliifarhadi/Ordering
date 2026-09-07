namespace AeroTech.Messages.AirPrice.IntegrationEvents;

public record PointOfSaleModified(
    string EventId,
    string AggregateId,
    string Title,
    DateTimeOffset TimeOfOccurrence);
