namespace AeroTech.Messages.FlightFlow.IntegrationEvents;

public record FlightVersionChangedEvent(long FlightId,long? DisruptionId,int Version);