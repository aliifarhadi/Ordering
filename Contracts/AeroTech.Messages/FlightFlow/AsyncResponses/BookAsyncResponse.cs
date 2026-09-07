namespace AeroTech.Messages.FlightFlow.AsyncResponses;

public record BookAsyncResponse(string ReferenceId, bool Success, string? Reason=null);
