namespace AeroTech.Messages.AirPrice.IntegrationEvents;

public record PointOfSaleRemoved(
    string EventId,
    string AggregateId,
    DateTimeOffset TimeOfOccurrence);